using Frog.Core.Models;

namespace Frog.Application.Gameplay;

public enum QuestTurnInStatus
{
    TurnedIn,
    IdempotentReplay,
    QuestNotFound,
    NotReady,
    PrerequisitesNotMet,
    InventoryFull,
    Failed,
}

public sealed record QuestTurnInResult(
    QuestTurnInStatus Status,
    string? Message = null,
    CharacterQuestProgress? Progress = null,
    int? GoldGranted = null,
    Guid? ItemGranted = null,
    int? ItemQuantityGranted = null);

/// <summary>Mutation atomique de quête (turn-in exactly-once — P8-R2).</summary>
public interface IQuestMutationRepository
{
    Task<QuestTurnInResult> TryTurnInAsync(
        Guid characterId,
        Guid questId,
        Guid requestId,
        CancellationToken cancellationToken = default);
}
