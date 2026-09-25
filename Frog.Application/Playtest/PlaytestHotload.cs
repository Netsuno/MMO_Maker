using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>Choix avant lancement : enregistrer la carte sale, ou annuler.</summary>
public enum PlaytestSaveChoice
{
    Save = 0,
    Cancel = 1,
}

/// <summary>Entrée pure du hotload (carte courante, spawn mémorisé, sale ou non).</summary>
public sealed class PlaytestHotloadRequest
{
    public bool IsDirty { get; init; }

    public PlaytestSaveChoice SaveChoice { get; init; } = PlaytestSaveChoice.Save;

    /// <summary>Identifiant catalogue de la carte ouverte. Vide tant que le premier enregistrement n’a pas eu lieu.</summary>
    public Guid? CurrentMapId { get; init; }

    public required Map Map { get; init; }

    public int? RememberedTileX { get; init; }

    public int? RememberedTileY { get; init; }

    public int FallbackTileX { get; init; }

    public int FallbackTileY { get; init; }
}

public abstract record PlaytestHotloadDecision
{
    /// <summary>
    /// Lancer sur la carte ouverte. <see cref="CanonicalMapId"/> est celui de la session,
    /// jamais un identifiant démo substitué.
    /// </summary>
    public sealed record Ready(
        bool SaveBeforeLaunch,
        Guid? CanonicalMapId,
        int TileX,
        int TileY,
        bool UsedRememberedSpawn) : PlaytestHotloadDecision;

    public sealed record Cancelled(string Reason) : PlaytestHotloadDecision;
}

/// <summary>
/// Décide l’enregistrement et le spawn avant le lancement.
/// Le snapshot publié (PostgreSQL puis manifeste <c>.fmap</c>) est produit ensuite par
/// <see cref="PlaytestMapPreparer"/> — pas d’injection dans un processus déjà lancé.
/// </summary>
public static class PlaytestHotload
{
    public const string DirtySavePrompt =
        "La carte a des modifications non enregistrées. Les enregistrer et lancer le test ?";

    public const string CancelledDirtyMessage =
        "Test annulé : la carte modifiée n’a pas été enregistrée.";

    public static PlaytestHotloadDecision Decide(PlaytestHotloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Map);

        if (request.IsDirty && request.SaveChoice != PlaytestSaveChoice.Save)
        {
            return new PlaytestHotloadDecision.Cancelled(CancelledDirtyMessage);
        }

        if (request.Map.Width <= 0 || request.Map.Height <= 0)
        {
            return new PlaytestHotloadDecision.Cancelled("Carte sans dimensions valides.");
        }

        var remembered = request.RememberedTileX is int && request.RememberedTileY is int;
        var (x, y) = MapPlaytestSpawn.ResolvePreferred(
            request.Map,
            request.RememberedTileX,
            request.RememberedTileY,
            request.FallbackTileX,
            request.FallbackTileY);

        Guid? mapId = request.CurrentMapId is Guid id && id != Guid.Empty ? id : null;
        return new PlaytestHotloadDecision.Ready(
            request.IsDirty,
            mapId,
            x,
            y,
            remembered);
    }
}
