using Frog.Core.Models;

namespace Frog.Application.Gameplay;

public interface ICharacterQuestRepository
{
    Task<IReadOnlyList<CharacterQuestProgress>> GetAllAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task<CharacterQuestProgress?> TryGetAsync(
        Guid characterId,
        Guid questId,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(CharacterQuestProgress progress, CancellationToken cancellationToken = default);
}

public interface ICharacterProfessionRepository
{
    Task<IReadOnlyList<CharacterProfessionProgress>> GetAllAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task<CharacterProfessionProgress?> TryGetAsync(
        Guid characterId,
        Guid professionId,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(CharacterProfessionProgress progress, CancellationToken cancellationToken = default);
}

public interface IEventCraftRepository
{
    /// <summary>Craft idempotent par requestId (Phase 8 — P8-4).</summary>
    Task<EventCraftResult> TryCraftAsync(
        Guid characterId,
        Guid recipeId,
        Guid requestId,
        CancellationToken cancellationToken = default);
}

public enum EventCraftStatus
{
    Crafted,
    IdempotentReplay,
    RecipeNotFound,
    InsufficientLevel,
    InsufficientIngredients,
    InventoryFull,
    Failed,
}

public sealed record EventCraftResult(
    EventCraftStatus Status,
    string? Message = null,
    InventorySnapshot? Inventory = null);
