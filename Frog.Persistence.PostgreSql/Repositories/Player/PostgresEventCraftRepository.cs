using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresEventCraftRepository(
    FrogDbContextGate gate,
    IPublishedRecipeCatalog recipes,
    IPublishedItemCatalog items,
    IPublishedProfessionCatalog? professions = null,
    TimeProvider? clock = null) : IEventCraftRepository
{
    private readonly FrogDbContextGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly IPublishedRecipeCatalog _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    private readonly IPublishedItemCatalog _items = items ?? throw new ArgumentNullException(nameof(items));
    private readonly IPublishedProfessionCatalog? _professions = professions;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public Task<EventCraftResult> TryCraftAsync(
        Guid characterId,
        Guid recipeId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || recipeId == Guid.Empty || requestId == Guid.Empty)
            {
                return new EventCraftResult(EventCraftStatus.Failed, "Paramètres invalides.");
            }

            var existing = await db.PlayerEventCraftRequests.AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId && r.RequestId == requestId, ct)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.RecipeId != recipeId)
                {
                    return new EventCraftResult(EventCraftStatus.Failed, "RequestId réutilisé avec recette différente.");
                }

                var replayInv = await db.PlayerInventorySlots.AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .OrderBy(s => s.SlotIndex)
                    .Select(s => new InventorySlotRecord(s.SlotIndex, s.ItemId, s.Quantity))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var replayGold = await db.PlayerCharacters.AsNoTracking()
                    .Where(c => c.Id == characterId)
                    .Select(c => (int?)c.Gold)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
                return new EventCraftResult(
                    EventCraftStatus.IdempotentReplay,
                    "Craft déjà effectué.",
                    new InventorySnapshot(characterId, replayInv),
                    RemainingGold: replayGold);
            }

            var recipe = await _recipes.TryGetPublishedByIdAsync(recipeId, ct).ConfigureAwait(false);
            if (recipe is null)
            {
                return new EventCraftResult(EventCraftStatus.RecipeNotFound, "Recette inconnue.");
            }

            var outputItem = await _items.LoadPublishedByIdAsync(recipe.OutputItemId, ct).ConfigureAwait(false);
            if (outputItem is null)
            {
                return new EventCraftResult(EventCraftStatus.Failed, "Objet produit inconnu.");
            }

            ProfessionDefinition? profession = null;
            if (_professions is not null)
            {
                profession = await _professions.TryGetPublishedByIdAsync(recipe.ProfessionId, ct).ConfigureAwait(false);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var character = await db.PlayerCharacters
                    .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);
                if (character is null)
                {
                    return new EventCraftResult(EventCraftStatus.Failed, "Personnage introuvable.");
                }

                var goldSpent = 0;
                if (recipe.GoldCost > 0)
                {
                    if (character.Gold < recipe.GoldCost)
                    {
                        return new EventCraftResult(EventCraftStatus.InsufficientGold, "Or insuffisant.");
                    }

                    character.Gold = checked(character.Gold - recipe.GoldCost);
                    goldSpent = recipe.GoldCost;
                }

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var slots = PostgresEconomyTransactionRepository.InventorySlotsFromRows(invRows);

                foreach (var ing in recipe.Ingredients)
                {
                    var have = slots.Where(s => s.ItemId == ing.ItemId).Sum(s => s.Quantity);
                    if (have < ing.Quantity)
                    {
                        return new EventCraftResult(EventCraftStatus.InsufficientIngredients, "Ingrédients insuffisants.");
                    }
                }

                foreach (var ing in recipe.Ingredients)
                {
                    var remaining = ing.Quantity;
                    for (var i = 0; i < slots.Length && remaining > 0; i++)
                    {
                        var slot = slots[i];
                        if (slot.ItemId != ing.ItemId || slot.Quantity <= 0)
                        {
                            continue;
                        }

                        var take = Math.Min(remaining, slot.Quantity);
                        slots[i] = slot with { Quantity = slot.Quantity - take };
                        if (slots[i].Quantity == 0)
                        {
                            slots[i] = slot with { ItemId = null, Quantity = 0 };
                        }

                        remaining -= take;
                    }
                }

                if (!PostgresEconomyTransactionRepository.TryAddToInventory(
                        slots,
                        recipe.OutputItemId,
                        recipe.OutputQuantity,
                        outputItem.MaxStack))
                {
                    return new EventCraftResult(EventCraftStatus.InventoryFull, "Inventaire plein.");
                }

                await PostgresEconomyTransactionRepository.PersistInventorySlotsAsync(db, characterId, invRows, slots, ct)
                    .ConfigureAwait(false);

                if (recipe.ProfessionExperienceReward > 0 || recipe.ProfessionId != Guid.Empty)
                {
                    var prog = await db.PlayerCharacterProfessionProgress
                        .FirstOrDefaultAsync(
                            p => p.CharacterId == characterId && p.ProfessionId == recipe.ProfessionId,
                            ct)
                        .ConfigureAwait(false);
                    if (prog is null || prog.Level < recipe.RequiredProfessionLevel)
                    {
                        return new EventCraftResult(
                            EventCraftStatus.InsufficientLevel,
                            $"Niveau métier insuffisant ({prog?.Level ?? 0}/{recipe.RequiredProfessionLevel}).");
                    }

                    var oldXp = prog.Experience;
                    var oldLevel = prog.Level;
                    var newXp = checked(oldXp + Math.Max(0L, recipe.ProfessionExperienceReward));
                    var maxLevel = profession?.MaxLevel > 0 ? profession.MaxLevel : 100;
                    var computedLevel = Math.Min(maxLevel, 1 + (int)(newXp / 100));
                    var newLevel = Math.Max(oldLevel, computedLevel);
                    prog.Experience = newXp;
                    prog.Level = newLevel;
                }

                db.PlayerEventCraftRequests.Add(new EventCraftRequestEntity
                {
                    CharacterId = characterId,
                    RequestId = requestId,
                    RecipeId = recipeId,
                    CompletedAtUtc = _clock.GetUtcNow(),
                });

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();

                var snapshot = new InventorySnapshot(
                    characterId,
                    slots.Select(s => new InventorySlotRecord(s.SlotIndex, s.ItemId, s.Quantity)).ToList());
                return new EventCraftResult(
                    EventCraftStatus.Crafted,
                    "Craft réussi.",
                    snapshot,
                    goldSpent > 0 ? goldSpent : null,
                    character.Gold);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return new EventCraftResult(EventCraftStatus.Failed, ex.Message);
            }
        }, cancellationToken);
}
