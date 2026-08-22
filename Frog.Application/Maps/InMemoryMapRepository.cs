using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Repository carte en mémoire (démo et tests — non durable).</summary>
public sealed class InMemoryMapRepository : IMapRepository
{
    private readonly Dictionary<Guid, StoredMap> _drafts = new();
    private readonly Dictionary<(Guid MapId, long Revision), StoredMap> _publishedSnapshots = new();
    private readonly Dictionary<Guid, List<MapPublicationRecord>> _history = new();
    private readonly object _gate = new();

    public InMemoryMapRepository(MapRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? MapRepositoryCapabilities.InMemoryDemo;
    }

    public MapRepositoryCapabilities Capabilities { get; }

    public Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.Map.Validate(out var error))
        {
            return Task.FromResult<SaveMapResult>(new SaveMapResult.ValidationFailed(error ?? "Carte invalide."));
        }

        var targetMaps = BuildTargetMapIndex(request);
        if (!MapWarpValidator.ValidateWarpTargets(request.Map, targetMaps, out var warpError))
        {
            return Task.FromResult<SaveMapResult>(new SaveMapResult.ValidationFailed(warpError ?? "Warp invalide."));
        }

        lock (_gate)
        {
            var mapId = request.MapId ?? Guid.Empty;
            if (mapId == Guid.Empty)
            {
                mapId = Guid.NewGuid();
            }

            if (!_drafts.TryGetValue(mapId, out var existing))
            {
                if (request.ExpectedRevision != 0)
                {
                    return Task.FromResult<SaveMapResult>(new SaveMapResult.Conflict(0));
                }

                var revision = 1L;
                var draft = CreateStored(mapId, request.Map, revision, MapPublishStatus.Draft, null);
                _drafts[mapId] = draft;

                if (request.Intent == SaveMapIntent.Publish)
                {
                    PublishSnapshotLocked(mapId, draft);
                    return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(revision, mapId, revision));
                }

                return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(revision, mapId));
            }

            if (existing.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveMapResult>(new SaveMapResult.Conflict(existing.Revision));
            }

            var newRevision = existing.Revision + 1;
            var updatedDraft = CreateStored(
                mapId,
                request.Map,
                newRevision,
                MapPublishStatus.Draft,
                existing.PublishedRevision);
            _drafts[mapId] = updatedDraft;

            if (request.Intent == SaveMapIntent.Publish)
            {
                PublishSnapshotLocked(mapId, updatedDraft);
                var publishedDraft = CreateStored(
                    mapId,
                    request.Map,
                    newRevision,
                    MapPublishStatus.Published,
                    newRevision);
                _drafts[mapId] = publishedDraft;
                return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(newRevision, mapId, newRevision));
            }

            return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(newRevision, mapId, updatedDraft.PublishedRevision));
        }
    }

    public Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_drafts.TryGetValue(mapId, out var stored))
            {
                return Task.FromResult<StoredMap?>(null);
            }

            return Task.FromResult<StoredMap?>(CloneStored(stored));
        }
    }

    public Task<StoredMap?> LoadPublishedByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_drafts.TryGetValue(mapId, out var draft) || draft.PublishedRevision is not long pubRev)
            {
                return Task.FromResult<StoredMap?>(null);
            }

            if (!_publishedSnapshots.TryGetValue((mapId, pubRev), out var snapshot))
            {
                return Task.FromResult<StoredMap?>(null);
            }

            return Task.FromResult<StoredMap?>(CloneStored(snapshot));
        }
    }

    public Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IReadOnlyList<MapCatalogEntry> list = _drafts.Values
                .OrderBy(m => m.Map.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.MapId)
                .Select(m => new MapCatalogEntry
                {
                    MapId = m.MapId,
                    Name = m.Map.Name,
                    Width = m.Map.Width,
                    Height = m.Map.Height,
                    Revision = m.Revision,
                    Status = m.PublishedRevision is not null ? MapPublishStatus.Published : MapPublishStatus.Draft,
                    PublishedRevision = m.PublishedRevision,
                })
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<IReadOnlyList<MapPublicationRecord>> ListPublicationHistoryAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_history.TryGetValue(mapId, out var list))
            {
                return Task.FromResult<IReadOnlyList<MapPublicationRecord>>(Array.Empty<MapPublicationRecord>());
            }

            return Task.FromResult<IReadOnlyList<MapPublicationRecord>>(list.ToList());
        }
    }

    private void PublishSnapshotLocked(Guid mapId, StoredMap draft)
    {
        var snapshot = CreateStored(mapId, draft.Map, draft.Revision, MapPublishStatus.Published, draft.Revision);
        _publishedSnapshots[(mapId, draft.Revision)] = snapshot;
        if (!_history.TryGetValue(mapId, out var records))
        {
            records = new List<MapPublicationRecord>();
            _history[mapId] = records;
        }

        records.Add(new MapPublicationRecord
        {
            MapId = mapId,
            Revision = draft.Revision,
            PublishedAtUtc = DateTimeOffset.UtcNow,
        });
    }

    private Dictionary<Guid, (int Width, int Height)> BuildTargetMapIndex(SaveMapRequest request)
    {
        lock (_gate)
        {
            var dict = _drafts.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.Map.Width, kvp.Value.Map.Height));
            if (request.MapId is Guid mapId && mapId != Guid.Empty)
            {
                dict[mapId] = (request.Map.Width, request.Map.Height);
            }

            return dict;
        }
    }

    private static StoredMap CreateStored(Guid mapId, Map map, long revision, MapPublishStatus status, long? publishedRevision)
        => new()
        {
            MapId = mapId,
            Map = CloneMap(map),
            Revision = revision,
            Status = status,
            PublishedRevision = publishedRevision,
        };

    private static StoredMap CloneStored(StoredMap stored)
        => new()
        {
            MapId = stored.MapId,
            Map = CloneMap(stored.Map),
            Revision = stored.Revision,
            Status = stored.Status,
            PublishedRevision = stored.PublishedRevision,
        };

    internal static Map CloneMap(Map source)
    {
        var clone = new Map
        {
            Name = source.Name,
            Width = source.Width,
            Height = source.Height,
            AllowPlayerOverlap = source.AllowPlayerOverlap,
        };
        foreach (var layer in source.Layers)
        {
            var layerClone = new Layer
            {
                LayerType = layer.LayerType,
                DisplayName = layer.DisplayName,
                Visible = layer.Visible,
                Locked = layer.Locked,
            };
            foreach (var tile in layer.Tiles)
            {
                layerClone.Tiles.Add(new Tile
                {
                    X = tile.X,
                    Y = tile.Y,
                    Type = tile.Type,
                    SrcX = tile.SrcX,
                    SrcY = tile.SrcY,
                    TilesetId = tile.TilesetId,
                    WarpTargetMapId = tile.WarpTargetMapId,
                    WarpTargetX = tile.WarpTargetX,
                    WarpTargetY = tile.WarpTargetY,
                    ScriptId = tile.ScriptId,
                });
            }

            clone.Layers.Add(layerClone);
        }

        return clone;
    }
}
