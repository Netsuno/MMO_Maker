using Frog.Core.Protocol;

namespace Frog.Server.Database;

/// <summary>Lecture des événements placés (<c>frog_map_event</c>) pour synchronisation client.</summary>
public interface IMapEventStore
{
    /// <summary>JSON tableau <see cref="MapEventWireEntry"/> UTF-8 (souvent <c>[]</c>).</summary>
    bool TryGetEventsWireJson(int mapId, out string json);

    /// <summary>Placements courants (cache serveur invalidé si la base change).</summary>
    bool TryGetPlacements(int mapId, out IReadOnlyList<MapEventWireEntry> placements);

    /// <summary>Version async préférée (remplit le cache sans bloquer sur sync-over-async).</summary>
    Task<(bool Ok, IReadOnlyList<MapEventWireEntry> Placements)> GetPlacementsAsync(
        int mapId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalide tout le cache placements. À appeler après republish carte / événement carte
    /// (hook serveur) pour forcer un rechargement depuis le catalogue publié.
    /// </summary>
    void InvalidateAll();
}
