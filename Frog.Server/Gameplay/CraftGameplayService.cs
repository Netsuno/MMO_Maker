using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Craft idempotent par recette (P8-4).</summary>
public sealed class CraftGameplayService(
    IPublishedRecipeCatalog recipes,
    IPublishedProfessionCatalog professions,
    ICharacterProfessionRepository professionProgress,
    IEventCraftRepository craftRepo,
    IInventoryRepository? inventory = null,
    ITradeHoldQuery? tradeHolds = null)
{
    private readonly IPublishedRecipeCatalog _recipes = recipes;
    private readonly IPublishedProfessionCatalog _professions = professions;
    private readonly ICharacterProfessionRepository _professionProgress = professionProgress;
    private readonly IEventCraftRepository _craftRepo = craftRepo;
    private readonly IInventoryRepository? _inventory = inventory;
    private readonly ITradeHoldQuery _tradeHolds = tradeHolds ?? NullTradeHoldQuery.Instance;

    public async Task<EventCraftResult> TryCraftAsync(
        Guid characterId,
        Guid recipeId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var replay = await _craftRepo.TryGetReplayAsync(characterId, recipeId, requestId, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            return replay;
        }

        var recipe = await _recipes.TryGetPublishedByIdAsync(recipeId, cancellationToken).ConfigureAwait(false);
        if (recipe is null)
        {
            return new EventCraftResult(EventCraftStatus.RecipeNotFound, "Recette inconnue.");
        }

        var profession = await _professions.TryGetPublishedByIdAsync(recipe.ProfessionId, cancellationToken)
            .ConfigureAwait(false);
        if (profession is null)
        {
            return new EventCraftResult(EventCraftStatus.RecipeNotFound, "Métier inconnu.");
        }

        var prog = await _professionProgress.TryGetAsync(characterId, recipe.ProfessionId, cancellationToken)
            .ConfigureAwait(false);
        var level = prog?.Level ?? 0;
        if (level < recipe.RequiredProfessionLevel)
        {
            return new EventCraftResult(
                EventCraftStatus.InsufficientLevel,
                $"Niveau {profession.Name} insuffisant ({level}/{recipe.RequiredProfessionLevel}).");
        }

        if (_inventory is not null)
        {
            var inv = await _inventory.GetAsync(characterId, cancellationToken).ConfigureAwait(false);
            foreach (var ing in recipe.Ingredients)
            {
                if (_tradeHolds.CanSpendItem(characterId, ing.ItemId, ing.Quantity, inv))
                {
                    continue;
                }

                replay = await _craftRepo.TryGetReplayAsync(characterId, recipeId, requestId, cancellationToken)
                    .ConfigureAwait(false);
                if (replay is not null)
                {
                    return replay;
                }

                return new EventCraftResult(
                    EventCraftStatus.InsufficientIngredients,
                    "Objets reserves pour un echange.");
            }
        }

        return await _craftRepo.TryCraftAsync(characterId, recipeId, requestId, cancellationToken).ConfigureAwait(false);
    }
}
