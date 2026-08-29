using System.Collections.Concurrent;
using Frog.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Database;

/// <summary>Adaptateur serveur : placements publiés PostgreSQL (cache async + TTL + InvalidateAll).</summary>
internal sealed class PublishedMapEventStoreAdapter : IMapEventStore
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1);

    private readonly Frog.Application.Content.IPublishedMapEventPlacementCatalog _catalog;
    private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();
    private readonly ILogger<PublishedMapEventStoreAdapter>? _logger;
    private readonly TimeProvider _clock;

    public PublishedMapEventStoreAdapter(
        Frog.Application.Content.IPublishedMapEventPlacementCatalog catalog,
        ILogger<PublishedMapEventStoreAdapter>? logger = null,
        TimeProvider? clock = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    private sealed record CacheEntry(
        string Json,
        IReadOnlyList<MapEventWireEntry> Placements,
        DateTimeOffset FetchedAtUtc);

    public bool TryGetEventsWireJson(int mapId, out string json)
    {
        json = "[]";
        if (mapId < 1)
        {
            return true;
        }

        if (_cache.TryGetValue(mapId, out var entry) && !IsStale(entry))
        {
            json = entry.Json;
            return true;
        }

        return false;
    }

    public bool TryGetPlacements(int mapId, out IReadOnlyList<MapEventWireEntry> placements)
    {
        placements = Array.Empty<MapEventWireEntry>();
        if (mapId < 1)
        {
            return true;
        }

        if (_cache.TryGetValue(mapId, out var entry) && !IsStale(entry))
        {
            placements = entry.Placements;
            return true;
        }

        return false;
    }

    public async Task<(bool Ok, IReadOnlyList<MapEventWireEntry> Placements)> GetPlacementsAsync(
        int mapId,
        CancellationToken cancellationToken = default)
    {
        if (mapId < 1)
        {
            return (true, Array.Empty<MapEventWireEntry>());
        }

        if (_cache.TryGetValue(mapId, out var cached) && !IsStale(cached))
        {
            return (true, cached.Placements);
        }

        var gate = _locks.GetOrAdd(mapId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(mapId, out cached) && !IsStale(cached))
            {
                return (true, cached.Placements);
            }

            var placements = await _catalog.GetPlacementsForRuntimeMapAsync(mapId, cancellationToken)
                .ConfigureAwait(false);
            var json = System.Text.Json.JsonSerializer.Serialize(placements);
            var entry = new CacheEntry(json, placements, _clock.GetUtcNow());
            _cache[mapId] = entry;
            return (true, placements);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Échec chargement placements map events pour map {MapId}", mapId);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateAll() => _cache.Clear();

    private bool IsStale(CacheEntry entry) =>
        _clock.GetUtcNow() - entry.FetchedAtUtc > CacheTtl;
}
