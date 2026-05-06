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
}
