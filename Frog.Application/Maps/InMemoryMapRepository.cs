using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Repository carte en mémoire (éditeur hors PostgreSQL, tests unitaires).</summary>
public sealed class InMemoryMapRepository : IMapRepository
{
    private readonly Dictionary<Guid, StoredMap> _maps = new();
    private readonly object _gate = new();

    public Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.Map.Validate(out var error))
        {
            return Task.FromResult<SaveMapResult>(new SaveMapResult.ValidationFailed(error ?? "Carte invalide."));
        }

        lock (_gate)
        {
            var mapId = request.MapId ?? Guid.Empty;
            if (mapId == Guid.Empty)
            {
                mapId = Guid.NewGuid();
            }

            if (!_maps.TryGetValue(mapId, out var existing))
            {
                if (request.ExpectedRevision != 0)
                {
                    return Task.FromResult<SaveMapResult>(new SaveMapResult.Conflict(0));
                }

                var created = new StoredMap
                {
                    MapId = mapId,
                    Map = CloneMap(request.Map),
                    Revision = 1,
                    Status = request.Status,
                };
                _maps[mapId] = created;
                return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(1, mapId));
            }

            if (existing.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveMapResult>(new SaveMapResult.Conflict(existing.Revision));
            }

            var updated = new StoredMap
            {
                MapId = mapId,
                Map = CloneMap(request.Map),
                Revision = existing.Revision + 1,
                Status = request.Status,
            };
            _maps[mapId] = updated;
            return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(updated.Revision, mapId));
        }
    }

    public Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_maps.TryGetValue(mapId, out var stored))
        {
            return Task.FromResult<StoredMap?>(null);
        }

        return Task.FromResult<StoredMap?>(new StoredMap
        {
            MapId = stored.MapId,
            Map = CloneMap(stored.Map),
            Revision = stored.Revision,
            Status = stored.Status,
        });
    }

    public Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MapCatalogEntry> list = _maps.Values
            .OrderBy(m => m.Map.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.MapId)
            .Select(m => new MapCatalogEntry
            {
                MapId = m.MapId,
                Name = m.Map.Name,
                Width = m.Map.Width,
                Height = m.Map.Height,
                Revision = m.Revision,
                Status = m.Status,
            })
            .ToList();
        return Task.FromResult(list);
    }

    private static Map CloneMap(Map source)
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
