namespace Frog.Server.Database;

/// <summary>Adaptateur serveur : placements publiés PostgreSQL via port applicatif.</summary>
internal sealed class PublishedMapEventStoreAdapter(
    Frog.Application.Content.IPublishedMapEventPlacementCatalog catalog) : IMapEventStore
{
    private readonly Frog.Application.Content.IPublishedMapEventPlacementCatalog _catalog = catalog;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, CacheEntry> _cache = new();

    private sealed record CacheEntry(string Json, IReadOnlyList<Frog.Core.Protocol.MapEventWireEntry> Placements);

    public bool TryGetEventsWireJson(int mapId, out string json)
    {
        json = "[]";
        if (mapId < 1)
        {
            return true;
        }

        if (!TryEnsureCached(mapId, out var entry) || entry is null)
        {
            return false;
        }

        json = entry.Json;
        return true;
    }

    public bool TryGetPlacements(int mapId, out IReadOnlyList<Frog.Core.Protocol.MapEventWireEntry> placements)
    {
        placements = Array.Empty<Frog.Core.Protocol.MapEventWireEntry>();
        if (mapId < 1)
        {
            return true;
        }

        if (!TryEnsureCached(mapId, out var entry) || entry is null)
        {
            return false;
        }

        placements = entry.Placements;
        return true;
    }

    private bool TryEnsureCached(int mapId, out CacheEntry? entry)
    {
        entry = null;
        try
        {
            if (_cache.TryGetValue(mapId, out entry))
            {
                return true;
            }

            var placements = _catalog.GetPlacementsForRuntimeMapAsync(mapId).GetAwaiter().GetResult();
            var json = System.Text.Json.JsonSerializer.Serialize(placements);
            entry = new CacheEntry(json, placements);
            _cache[mapId] = entry;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
