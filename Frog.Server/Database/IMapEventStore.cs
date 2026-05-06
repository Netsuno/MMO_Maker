namespace Frog.Server.Database;

/// <summary>Lecture des événements placés (<c>frog_map_event</c>) pour synchronisation client.</summary>
public interface IMapEventStore
{
    /// <summary>JSON tableau <see cref="Frog.Core.Protocol.MapEventWireEntry"/> UTF-8 (souvent <c>[]</c>).</summary>
    bool TryGetEventsWireJson(int mapId, out string json);
}
