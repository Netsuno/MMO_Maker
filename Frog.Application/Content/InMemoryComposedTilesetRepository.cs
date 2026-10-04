using System.Collections.Concurrent;

using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryComposedTilesetRepository : IComposedTilesetRepository, IPublishedComposedTilesetCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();

    public InMemoryComposedTilesetRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveComposedTilesetResult> SaveAsync(
        SaveComposedTilesetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveComposedTilesetResult>(
                new SaveComposedTilesetResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        var definition = Clone(request.Definition);
        if (definition.Id == Guid.Empty)
        {
            definition.Id = Guid.NewGuid();
        }

        definition.Normalize();
        if (!definition.Validate(out var error))
        {
            return Task.FromResult<SaveComposedTilesetResult>(new SaveComposedTilesetResult.ValidationFailed(error!));
        }

        var exclude = request.TilesetId ?? Guid.Empty;
        if (_drafts.Values.Any(d =>
                string.Equals(d.Definition.LogicalPath, definition.LogicalPath, StringComparison.OrdinalIgnoreCase)
                && d.Id != exclude))
        {
            return Task.FromResult<SaveComposedTilesetResult>(
                new SaveComposedTilesetResult.ValidationFailed("Chemin logique déjà utilisé."));
        }

        Guid id;
        long newRevision;
        if (request.TilesetId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return Task.FromResult<SaveComposedTilesetResult>(new SaveComposedTilesetResult.Conflict(0));
            }

            id = definition.Id;
            newRevision = 1;
            definition.Id = id;
            _drafts[id] = new DraftRecord(id, definition, newRevision, ContentPublishStatus.Draft, null);
        }
        else
        {
            if (!_drafts.TryGetValue(existing, out var current))
            {
                return Task.FromResult<SaveComposedTilesetResult>(new SaveComposedTilesetResult.Conflict(0));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveComposedTilesetResult>(new SaveComposedTilesetResult.Conflict(current.Revision));
            }

            id = existing;
            newRevision = current.Revision + 1;
            definition.Id = id;
            _drafts[id] = current with
            {
                Definition = definition,
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

        return Task.FromResult<SaveComposedTilesetResult>(
            new SaveComposedTilesetResult.Success(newRevision, id, publishedRevision));
    }

    public Task<StoredComposedTileset?> LoadByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(tilesetId, out var draft))
        {
            return Task.FromResult<StoredComposedTileset?>(null);
        }

        return Task.FromResult<StoredComposedTileset?>(new StoredComposedTileset
        {
            TilesetId = draft.Id,
            Definition = Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<IReadOnlyList<ComposedTilesetCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> rows = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            rows = rows.Where(row =>
                row.Definition.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.Definition.LogicalPath.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter is { } status)
        {
            rows = rows.Where(row => row.Status == status);
        }

        var list = rows
            .OrderBy(row => row.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row => new ComposedTilesetCatalogEntry
            {
                TilesetId = row.Id,
                Name = row.Definition.Name,
                LogicalPath = row.Definition.LogicalPath,
                Revision = row.Revision,
                Status = row.Status,
                PublishedRevision = row.PublishedRevision,
                TileCount = row.Definition.Tiles.Count,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ComposedTilesetCatalogEntry>>(list);
    }

    public Task<DeleteComposedTilesetResult> DeleteAsync(Guid tilesetId, CancellationToken cancellationToken = default)
    {
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<DeleteComposedTilesetResult>(
                new DeleteComposedTilesetResult.PersistenceFailed("Persistance mémoire démo désactivée."));
        }

        var removed = _drafts.TryRemove(tilesetId, out _);
        _published.TryRemove(tilesetId, out _);
        return Task.FromResult<DeleteComposedTilesetResult>(
            removed ? new DeleteComposedTilesetResult.Success() : new DeleteComposedTilesetResult.NotFound());
    }

    public Task<IReadOnlyList<ComposedTilesetDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(row => row.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row => Clone(row.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<ComposedTilesetDefinition>>(list);
    }

    private static ComposedTilesetDefinition Clone(ComposedTilesetDefinition src)
    {
        var copy = new ComposedTilesetDefinition
        {
            Id = src.Id,
            Name = src.Name,
            LogicalPath = src.LogicalPath,
        };
        foreach (var tile in src.Tiles)
        {
            if (tile is null)
            {
                continue;
            }

            copy.Tiles.Add(new ComposedTileRef
            {
                TileAssetId = tile.TileAssetId,
                DisplayName = tile.DisplayName,
                NormalizedRgba = tile.NormalizedRgba is { Length: > 0 } bytes ? bytes.ToArray() : Array.Empty<byte>(),
            });
        }

        return copy;
    }

    private sealed record DraftRecord(
        Guid Id,
        ComposedTilesetDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, ComposedTilesetDefinition Definition, long Revision);
}
