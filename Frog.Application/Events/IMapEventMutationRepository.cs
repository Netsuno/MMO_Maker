using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Application.Events;

/// <summary>Exécution atomique PostgreSQL des effets persistants d'un plan événement (J4-PG).</summary>
public interface IMapEventMutationRepository
{
    /// <summary>
    /// Applique <paramref name="plan"/> dans une seule transaction PostgreSQL.
    /// La clé ledger est <see cref="MapEventExecutionIdentity.LedgerKey"/> (CharacterId + RequestId).
    /// Les branches et common-events doivent déjà être résolus dans le plan.
    /// </summary>
    Task<MapEventMutationResult> TryExecutePlanAsync(
        MapEventExecutionPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compatibilité tests / anciens appelants. Le chemin public serveur (J4-SERVER)
    /// planifie via Core puis appelle <see cref="TryExecutePlanAsync"/>.
    /// </summary>
    Task<MapEventMutationResult> TryExecutePageAsync(
        Guid characterId,
        Guid requestId,
        long placementId,
        int catalogAliasId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        CancellationToken cancellationToken = default) =>
        TryExecutePlanAsync(
            MapEventExecutionPlan.Ok(
                new MapEventExecutionIdentity(requestId, characterId, placementId, catalogAliasId),
                commands),
            cancellationToken);
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

    public bool QuestsChanged { get; set; }

    public bool ProfessionsChanged { get; set; }

    public bool RecipesChanged { get; set; }

    public string? QuestSummary { get; set; }

    public int? ResultGold { get; set; }

    public bool Waiting { get; set; }

    public DateTimeOffset? WaitUntilUtc { get; set; }

    public IReadOnlyList<MapEventCommandDefinition>? PendingCommands { get; set; }
}
