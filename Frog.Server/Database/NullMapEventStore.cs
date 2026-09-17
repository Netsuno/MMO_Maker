using Frog.Core.Protocol;

namespace Frog.Server.Database;

public sealed class NullMapEventStore : IMapEventStore
{
    public static NullMapEventStore Instance { get; } = new();

    public bool TryGetEventsWireJson(int mapId, out string json)
    {
        _ = mapId;
        json = "[]";
        return true;
    }

    public bool TryGetPlacements(int mapId, out IReadOnlyList<MapEventWireEntry> placements)
    {
        _ = mapId;
        placements = Array.Empty<MapEventWireEntry>();
        return true;
    }

    public Task<(bool Ok, IReadOnlyList<MapEventWireEntry> Placements)> GetPlacementsAsync(
        int mapId,
        CancellationToken cancellationToken = default)
    {
        _ = mapId;
        _ = cancellationToken;
        return Task.FromResult((true, (IReadOnlyList<MapEventWireEntry>)Array.Empty<MapEventWireEntry>()));
    }

    public void InvalidateAll()
    {
    }
}
