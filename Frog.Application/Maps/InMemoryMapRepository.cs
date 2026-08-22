using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Repository carte en mémoire (éditeur hors PostgreSQL, tests unitaires).</summary>
public sealed class InMemoryMapRepository : IMapRepository
{
    private readonly ConcurrentDictionary<int, StoredMap> _maps = new();
    private readonly object _gate = new();

    public Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.LegacyId < 0)
        {
            return Task.FromResult<SaveMapResult>(new SaveMapResult.ValidationFailed("LegacyId doit être >= 0."));
        }

        if (!request.Map.Validate(out var error))
        {
            return Task.FromResult<SaveMapResult>(new SaveMapResult.ValidationFailed(error ?? "Carte invalide."));
        }

        lock (_gate)
        {
            if (!_maps.TryGetValue(request.LegacyId, out var existing))
            {
                if (request.ExpectedRevision != 0)
                {
                    return Task.FromResult<SaveMapResult>(new SaveMapResult.Conflict(0));
                }

                var created = new StoredMap
                {
                    LegacyId = request.LegacyId,
                    Map = CloneMap(request.Map),
                    Revision = 1,
                    Status = request.Status,
                };
                _maps[request.LegacyId] = created;
                return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(1));
            }

            if (existing.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveMapResult>(new SaveMapResult.Conflict(existing.Revision));
            }

            var updated = new StoredMap
            {
                LegacyId = request.LegacyId,
                Map = CloneMap(request.Map),
                Revision = existing.Revision + 1,
                Status = request.Status,
            };
            _maps[request.LegacyId] = updated;
            return Task.FromResult<SaveMapResult>(new SaveMapResult.Success(updated.Revision));
        }
    }

    public Task<StoredMap?> LoadByLegacyIdAsync(int legacyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_maps.TryGetValue(legacyId, out var stored))
        {
            return Task.FromResult<StoredMap?>(null);
        }

        return Task.FromResult<StoredMap?>(new StoredMap
        {
            LegacyId = stored.LegacyId,
            Map = CloneMap(stored.Map),
            Revision = stored.Revision,
            Status = stored.Status,
        });
    }

    public Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MapCatalogEntry> list = _maps.Values
            .OrderBy(m => m.LegacyId)
            .Select(m => new MapCatalogEntry
            {
                LegacyId = m.LegacyId,
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
        // Sérialisation binaire du modèle déjà testée ; clone défensif simple pour l’in-memory.
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
