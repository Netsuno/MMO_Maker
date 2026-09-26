using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Application.Events;

/// <summary>Exécution atomique PostgreSQL d'une unité transactionnelle d'événement (R2-4 / J4-PG).</summary>
public interface IMapEventMutationRepository
{
    /// <summary>
    /// Applique <paramref name="plan"/> comme <see cref="MapEventTransactionalUnit.FromPlan"/>
    /// dans une seule transaction PostgreSQL (mutations persistantes + intents
    /// <c>start_dialogue</c> / <c>teleport</c> / <c>open_shop</c> dans le snapshot). Les effets de session
    /// ne sont pas appliqués ici : le serveur les rejoue après commit.
    /// La clé ledger est <see cref="MapEventExecutionIdentity.LedgerKey"/> (CharacterId + RequestId).
    /// Une reprise wait utilise <see cref="MapEventExecutionIdentity.ForWaitResume"/> (nouvelle ligne).
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

    /// <summary>Activation persistée pour <see cref="MapEventExecutionIdentity.Restore"/>.</summary>
    public Guid ActivationId { get; set; }

    public int WaitOrdinal { get; set; }

    /// <summary>Intent <c>start_dialogue</c> enregistré dans la TX (appliqué côté session après commit).</summary>
    public Guid? DialogueId { get; set; }

    /// <summary>Intent <c>open_shop</c> enregistré dans la TX (boutique publiée ouverte après commit).</summary>
    public Guid? ShopId { get; set; }

    /// <summary>Intent <c>teleport</c> enregistré dans la TX (appliqué côté session après commit).</summary>
    public int? TeleportMapId { get; set; }

    public int? TeleportTileX { get; set; }

    public int? TeleportTileY { get; set; }

    /// <summary>Intent <c>set_weather</c> enregistré dans la TX (appliqué sur la session après commit).</summary>
    public string? WeatherKind { get; set; }

    /// <summary>Opérations <c>show_picture</c> / <c>erase_picture</c> de cette TX, dans l'ordre.</summary>
    public List<MapEventPictureOp> PictureOps { get; set; } = [];

    public (int MapId, int TileX, int TileY)? Teleport =>
        TeleportMapId is int mapId && TeleportTileX is int tileX && TeleportTileY is int tileY
            ? (mapId, tileX, tileY)
            : null;

    public void RecordDialogue(Guid dialogueId) => DialogueId = dialogueId;

    public void RecordShop(Guid shopId) => ShopId = shopId;

    public void RecordTeleport(int mapId, int tileX, int tileY)
    {
        TeleportMapId = mapId;
        TeleportTileX = tileX;
        TeleportTileY = tileY;
    }

    public void RecordWeather(string weatherKind) => WeatherKind = weatherKind;

    public void RecordPicture(MapEventPictureOp op)
    {
        PictureOps ??= [];
        PictureOps.Add(op);
    }

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
