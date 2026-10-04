using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryMapObjectRepository : IMapObjectRepository, IPublishedMapObjectCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();

    public InMemoryMapObjectRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveMapObjectResult> SaveAsync(
        SaveMapObjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveMapObjectResult>(
                new SaveMapObjectResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveMapObjectResult>(new SaveMapObjectResult.ValidationFailed(error!));
        }

        var exclude = request.MapObjectId ?? Guid.Empty;
        if (_drafts.Values.Any(d =>
                string.Equals(d.Definition.LogicalPath, request.Definition.LogicalPath, StringComparison.OrdinalIgnoreCase)
                && d.Id != exclude))
        {
            return Task.FromResult<SaveMapObjectResult>(
                new SaveMapObjectResult.ValidationFailed("Chemin logique déjà utilisé."));
        }

        if (_drafts.Values.Any(d =>
                string.Equals(d.Definition.PlacementId, request.Definition.PlacementId, StringComparison.Ordinal)
                && d.Id != exclude))
        {
            return Task.FromResult<SaveMapObjectResult>(
                new SaveMapObjectResult.ValidationFailed("Identifiant de placement déjà utilisé."));
        }

        Guid id;
        long newRevision;
        if (request.MapObjectId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return Task.FromResult<SaveMapObjectResult>(new SaveMapObjectResult.Conflict(0));
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var def = Clone(request.Definition);
            def.Id = id;
            _drafts[id] = new DraftRecord(id, def, newRevision, ContentPublishStatus.Draft, null);
        }
        else
        {
            if (!_drafts.TryGetValue(existing, out var current))
            {
                return Task.FromResult<SaveMapObjectResult>(new SaveMapObjectResult.Conflict(0));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveMapObjectResult>(new SaveMapObjectResult.Conflict(current.Revision));
            }

            id = existing;
            newRevision = current.Revision + 1;
            var def = Clone(request.Definition);
            def.Id = id;
            _drafts[id] = current with
            {
                Definition = def,
                Revision = newRevision,
                Status = ContentPublishStatus.Draft,
            };
        }

        long? publishedRevision = null;
        if (request.Intent == SaveContentIntent.Publish)
        {
            var draft = _drafts[id];
            publishedRevision = newRevision;
            _published[id] = new PublishedRecord(id, Clone(draft.Definition), publishedRevision.Value);
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return Task.FromResult<SaveMapObjectResult>(
            new SaveMapObjectResult.Success(newRevision, id, publishedRevision));
    }

    public Task<StoredMapObject?> LoadByIdAsync(Guid mapObjectId, CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(mapObjectId, out var d))
        {
            return Task.FromResult<StoredMapObject?>(null);
        }

        return Task.FromResult<StoredMapObject?>(new StoredMapObject
        {
            MapObjectId = d.Id,
            Definition = Clone(d.Definition),
            Revision = d.Revision,
            Status = d.Status,
            PublishedRevision = d.PublishedRevision,
        });
    }

    public Task<StoredMapObject?> LoadPublishedByIdAsync(Guid mapObjectId, CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(mapObjectId, out var p))
        {
            return Task.FromResult<StoredMapObject?>(null);
        }

        return Task.FromResult<StoredMapObject?>(new StoredMapObject
        {
            MapObjectId = p.Id,
            Definition = Clone(p.Definition),
            Revision = p.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = p.Revision,
        });
    }

    public Task<IReadOnlyList<MapObjectCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> q = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.LogicalPath.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.PlacementId.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter is { } st)
        {
            q = q.Where(d => d.Status == st);
        }

        var list = q
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new MapObjectCatalogEntry
            {
                MapObjectId = d.Id,
                Name = d.Definition.Name,
                LogicalPath = d.Definition.LogicalPath,
                PlacementId = d.Definition.PlacementId,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<MapObjectCatalogEntry>>(list);
    }

    public Task<DeleteMapObjectResult> DeleteAsync(Guid mapObjectId, CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryRemove(mapObjectId, out _))
        {
            return Task.FromResult<DeleteMapObjectResult>(new DeleteMapObjectResult.NotFound());
        }

        _published.TryRemove(mapObjectId, out _);
        return Task.FromResult<DeleteMapObjectResult>(new DeleteMapObjectResult.Success());
    }

    public Task<IReadOnlyList<MapObjectDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<MapObjectDefinition>>(list);
    }

    private static MapObjectDefinition Clone(MapObjectDefinition src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        LogicalPath = src.LogicalPath,
        PlacementId = src.PlacementId,
        FootprintWidthTiles = src.FootprintWidthTiles,
        FootprintHeightTiles = src.FootprintHeightTiles,
        WidthPixels = src.WidthPixels,
        HeightPixels = src.HeightPixels,
        Sha256Hex = src.Sha256Hex,
        PngBytes = src.PngBytes is { Length: > 0 } png ? png.ToArray() : src.PngBytes,
    };

    private sealed record DraftRecord(
        Guid Id,
        MapObjectDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, MapObjectDefinition Definition, long Revision);
}
