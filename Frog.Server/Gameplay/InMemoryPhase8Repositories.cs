using System.Collections.Concurrent;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

public sealed class InMemoryCharacterQuestRepository : ICharacterQuestRepository
{
    private readonly ConcurrentDictionary<(Guid CharacterId, Guid QuestId), CharacterQuestProgress> _store = new();

    public Task<IReadOnlyList<CharacterQuestProgress>> GetAllAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var list = _store
            .Where(kv => kv.Key.CharacterId == characterId)
            .Select(kv => kv.Value)
            .ToList();
        return Task.FromResult<IReadOnlyList<CharacterQuestProgress>>(list);
    }

    public Task<CharacterQuestProgress?> TryGetAsync(
        Guid characterId,
        Guid questId,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((characterId, questId), out var progress);
        return Task.FromResult(progress);
    }

    public Task UpsertAsync(CharacterQuestProgress progress, CancellationToken cancellationToken = default)
    {
        _store[(progress.CharacterId, progress.QuestId)] = progress;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryCharacterProfessionRepository : ICharacterProfessionRepository
{
    private readonly ConcurrentDictionary<(Guid CharacterId, Guid ProfessionId), CharacterProfessionProgress> _store = new();

    public Task<IReadOnlyList<CharacterProfessionProgress>> GetAllAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var list = _store
            .Where(kv => kv.Key.CharacterId == characterId)
            .Select(kv => kv.Value)
            .ToList();
        return Task.FromResult<IReadOnlyList<CharacterProfessionProgress>>(list);
    }

    public Task<CharacterProfessionProgress?> TryGetAsync(
        Guid characterId,
        Guid professionId,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((characterId, professionId), out var progress);
        return Task.FromResult(progress);
    }

    public Task UpsertAsync(CharacterProfessionProgress progress, CancellationToken cancellationToken = default)
    {
        _store[(progress.CharacterId, progress.ProfessionId)] = progress;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryEventCraftRepository(
    IPublishedRecipeCatalog recipes,
    IInventoryRepository inventory,
    IPublishedItemCatalog items,
    ICharacterRepository? characters = null,
    ICharacterProfessionRepository? professions = null,
    IPublishedProfessionCatalog? professionCatalog = null) : IEventCraftRepository
{
    private readonly ConcurrentDictionary<(Guid CharacterId, Guid RequestId), EventCraftResult> _completed = new();

    public async Task<EventCraftResult> TryCraftAsync(
        Guid characterId,
        Guid recipeId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (_completed.TryGetValue((characterId, requestId), out var replay))
        {
            return replay with { Status = EventCraftStatus.IdempotentReplay };
        }

        var recipe = await recipes.TryGetPublishedByIdAsync(recipeId, cancellationToken).ConfigureAwait(false);
        if (recipe is null)
        {
            return new EventCraftResult(EventCraftStatus.RecipeNotFound);
        }

        int? remainingGold = null;
        int? goldSpent = null;
        if (recipe.GoldCost > 0)
        {
            if (characters is null)
            {
                return new EventCraftResult(EventCraftStatus.Failed, "Dépôt personnage indisponible.");
            }

            var record = await characters.FindByIdAsync(characterId, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return new EventCraftResult(EventCraftStatus.Failed, "Personnage introuvable.");
            }

            if (record.Gold < recipe.GoldCost)
            {
                return new EventCraftResult(EventCraftStatus.InsufficientGold, "Or insuffisant.");
            }

            var newGold = checked(record.Gold - recipe.GoldCost);
            await characters.SaveAsync(record with { Gold = newGold }, cancellationToken).ConfigureAwait(false);
            goldSpent = recipe.GoldCost;
            remainingGold = newGold;
        }
        else if (characters is not null)
        {
            var record = await characters.FindByIdAsync(characterId, cancellationToken).ConfigureAwait(false);
            remainingGold = record?.Gold;
        }

        var snapshot = await inventory.GetAsync(characterId, cancellationToken).ConfigureAwait(false);
        foreach (var ing in recipe.Ingredients)
        {
            var have = snapshot.Slots.Where(s => s.ItemId == ing.ItemId).Sum(s => s.Quantity);
            if (have < ing.Quantity)
            {
                return new EventCraftResult(EventCraftStatus.InsufficientIngredients, "Ingrédients insuffisants.");
            }
        }

        foreach (var ing in recipe.Ingredients)
        {
            var remaining = ing.Quantity;
            foreach (var slot in snapshot.Slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (slot.ItemId != ing.ItemId || slot.Quantity <= 0)
                {
                    continue;
                }

                var take = Math.Min(remaining, slot.Quantity);
                var remove = await inventory.TryRemoveAsync(characterId, slot.SlotIndex, take, cancellationToken)
                    .ConfigureAwait(false);
                if (remove.Status != InventoryMutationStatus.Ok)
                {
                    return new EventCraftResult(EventCraftStatus.Failed, remove.ErrorMessage);
                }

                remaining -= take;
            }
        }

        var outputItem = await items.LoadPublishedByIdAsync(recipe.OutputItemId, cancellationToken).ConfigureAwait(false);
        if (outputItem is null)
        {
            return new EventCraftResult(EventCraftStatus.Failed, "Objet produit inconnu.");
        }

        var add = await inventory.TryAddAsync(
                characterId,
                recipe.OutputItemId,
                recipe.OutputQuantity,
                outputItem.MaxStack,
                cancellationToken)
            .ConfigureAwait(false);
        if (add.Status != InventoryMutationStatus.Ok)
        {
            return new EventCraftResult(EventCraftStatus.InventoryFull, add.ErrorMessage);
        }

        if (professions is not null && recipe.ProfessionId != Guid.Empty)
        {
            var prog = await professions.TryGetAsync(characterId, recipe.ProfessionId, cancellationToken)
                .ConfigureAwait(false);
            var oldXp = prog?.Experience ?? 0L;
            var oldLevel = prog?.Level ?? 0;
            var newXp = checked(oldXp + Math.Max(0L, recipe.ProfessionExperienceReward));
            var maxLevel = 100;
            if (professionCatalog is not null)
            {
                var def = await professionCatalog.TryGetPublishedByIdAsync(recipe.ProfessionId, cancellationToken)
                    .ConfigureAwait(false);
                if (def is not null && def.MaxLevel > 0)
                {
                    maxLevel = def.MaxLevel;
                }
            }

            var computedLevel = Math.Min(maxLevel, 1 + (int)(newXp / 100));
            await professions.UpsertAsync(
                    new CharacterProfessionProgress
                    {
                        CharacterId = characterId,
                        ProfessionId = recipe.ProfessionId,
                        Level = Math.Max(oldLevel, computedLevel),
                        Experience = newXp,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var result = new EventCraftResult(
            EventCraftStatus.Crafted,
            "Craft réussi.",
            add.Snapshot,
            goldSpent,
            remainingGold);
        _completed[(characterId, requestId)] = result;
        return result;
    }
}
