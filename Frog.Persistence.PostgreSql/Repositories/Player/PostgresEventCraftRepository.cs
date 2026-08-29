using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresEventCraftRepository(
    FrogDbContextGate gate,
    IPublishedRecipeCatalog recipes,
    IPublishedItemCatalog items,
    TimeProvider? clock = null) : IEventCraftRepository
{
    private readonly FrogDbContextGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly IPublishedRecipeCatalog _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    private readonly IPublishedItemCatalog _items = items ?? throw new ArgumentNullException(nameof(items));
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
                return new EventCraftResult(EventCraftStatus.IdempotentReplay, "Craft déjà effectué.", new InventorySnapshot(characterId, replayInv));
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

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
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
                return new EventCraftResult(EventCraftStatus.Crafted, "Craft réussi.", snapshot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return new EventCraftResult(EventCraftStatus.Failed, ex.Message);
            }
        }, cancellationToken);
}
