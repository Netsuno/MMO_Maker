using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;

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

    /// <summary>Interrupteurs mutés par <c>set_switch</c> dans cette exécution (dernier write gagne).</summary>
    public List<WorldSwitchWire> SwitchChanges { get; set; } = [];

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

    public void RecordSwitch(string switchId, bool value)
    {
        SwitchesChanged = true;
        SwitchChanges ??= [];
        for (var i = 0; i < SwitchChanges.Count; i++)
        {
            if (string.Equals(SwitchChanges[i].SwitchId, switchId, StringComparison.Ordinal))
            {
                SwitchChanges[i].Value = value;
                return;
            }
        }

        SwitchChanges.Add(new WorldSwitchWire { SwitchId = switchId, Value = value });
    }
}
