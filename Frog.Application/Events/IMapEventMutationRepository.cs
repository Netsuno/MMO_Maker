using Frog.Core.Models;

namespace Frog.Application.Events;

/// <summary>Exécution atomique PostgreSQL des effets persistants d'une page événement (P8-I4).</summary>
public interface IMapEventMutationRepository
{
    Task<MapEventMutationResult> TryExecutePageAsync(
        Guid characterId,
        Guid requestId,
        long placementId,
        int catalogAliasId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        CancellationToken cancellationToken = default);
}

public enum MapEventMutationStatus
{
    Executed,
    IdempotentReplay,
    NoOp,
    Failed,
}

public sealed record MapEventMutationResult(
    MapEventMutationStatus Status,
    string? ErrorMessage,
    MapEventExecutionSnapshot? Snapshot = null);

/// <summary>État post-commit pour effets client (inventaire, or, switches, etc.).</summary>
public sealed class MapEventExecutionSnapshot
{
    public string? ShowText { get; set; }

    public bool SwitchesChanged { get; set; }

    public bool VariablesChanged { get; set; }

    public bool InventoryChanged { get; set; }

    public bool GoldChanged { get; set; }

    public int? ResultGold { get; set; }

    public bool Waiting { get; set; }

    public DateTimeOffset? WaitUntilUtc { get; set; }

    public IReadOnlyList<MapEventCommandDefinition>? PendingCommands { get; set; }
}
