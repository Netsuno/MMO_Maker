using Frog.Core.Protocol;

namespace Frog.Server.Database;

/// <summary>Lecture des événements placés (<c>frog_map_event</c>) pour synchronisation client.</summary>
public interface IMapEventStore
{
    /// <summary>JSON tableau <see cref="MapEventWireEntry"/> UTF-8 (souvent <c>[]</c>).</summary>
    bool TryGetEventsWireJson(int mapId, out string json);

    /// <summary>Placements courants (cache serveur invalidé si la base change).</summary>
    bool TryGetPlacements(int mapId, out IReadOnlyList<MapEventWireEntry> placements);
}
