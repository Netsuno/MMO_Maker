using Frog.Core.Constants;

namespace Frog.Core.Protocol;

/// <summary>
/// Indice client « touche d'interaction » aligné sur
/// <c>PacketDispatcher.HandleInteractRequestAsync</c>.
/// La tuile est celle du centre joueur (<c>pixel / tailleTuile</c>, comme
/// <c>Session.PositionX/Y</c>). Seuls les placements dont le trigger se normalise
/// en <see cref="MapEventTriggerKinds.Interact"/> comptent — c'est le fil de la
/// page Phase 8 <c>action</c> (<c>ToWireTriggerKind</c>). Le premier placement
/// gagne : plus petit <c>catalogId</c>, puis <c>placementId</c>.
/// Les pages et leurs conditions ne voyagent pas dans <c>MapEventsResult</c> ;
/// l'indice suit donc le filtre placement du serveur, pas la page résolue.
/// </summary>
public static class MapEventInteractHint
{
    public const string TalkVerb = "Parler";

    public const string InteractVerb = "Interagir";

    public static (int TileX, int TileY) TileOfCenter(
        int centerPixelX,
        int centerPixelY,
        int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        if (tileSizePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSizePixels));
        }

        return (centerPixelX / tileSizePixels, centerPixelY / tileSizePixels);
    }

    /// <summary>Même prédicat que le filtre InteractRequest serveur.</summary>
    public static bool IsActionTrigger(string? triggerKind) =>
        MapEventTriggerNormalization.NormalizeTriggerKind(triggerKind) == MapEventTriggerKinds.Interact;

    public static MapEventWireEntry? Select(int tileX, int tileY, IEnumerable<MapEventWireEntry>? placements)
    {
        if (placements is null)
        {
            return null;
        }

        MapEventWireEntry? best = null;
        foreach (var placement in placements)
        {
            if (placement.TileX != tileX || placement.TileY != tileY || !IsActionTrigger(placement.TriggerKind))
            {
                continue;
            }

            if (best is null
                || placement.CatalogId < best.CatalogId
                || (placement.CatalogId == best.CatalogId && placement.PlacementId < best.PlacementId))
            {
                best = placement;
            }
        }

        return best;
    }

    /// <summary>
    /// Texte HUD ou <c>null</c> si rien à montrer.
    /// <paramref name="dialogueOpen"/> et <paramref name="inputBlocked"/> masquent l'indice
    /// (dialogue en cours, saisie, menus).
    /// </summary>
    public static string? Resolve(
        int tileX,
        int tileY,
        IEnumerable<MapEventWireEntry>? placements,
        string? interactKeyDisplay,
        bool playing,
        bool dialogueOpen,
        bool inputBlocked)
    {
        if (!playing || dialogueOpen || inputBlocked)
        {
            return null;
        }

        var target = Select(tileX, tileY, placements);
        return target is null ? null : FormatCue(interactKeyDisplay, target);
    }

    public static string FormatCue(string? interactKeyDisplay, MapEventWireEntry target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var key = string.IsNullOrWhiteSpace(interactKeyDisplay) ? "E" : interactKeyDisplay.Trim();
        var verb = string.IsNullOrWhiteSpace(target.DisplayName) ? InteractVerb : TalkVerb;
        return $"[{key}] {verb}";
    }
}
