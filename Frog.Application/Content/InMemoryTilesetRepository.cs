using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryTilesetRepository : ITilesetRepository, IPublishedTilesetCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();
    private readonly Func<int, bool>? _paletteReferenced;

    public InMemoryTilesetRepository(
        ContentRepositoryCapabilities? capabilities = null,
        Func<int, bool>? paletteReferenced = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
        _paletteReferenced = paletteReferenced;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveTilesetResult> SaveAsync(SaveTilesetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveTilesetResult>(
                new SaveTilesetResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveTilesetResult>(new SaveTilesetResult.ValidationFailed(error!));
        }

        var pathTaken = _drafts.Values.Any(d =>
            string.Equals(d.Definition.LogicalPath, request.Definition.LogicalPath, StringComparison.OrdinalIgnoreCase)
            && d.Id != (request.TilesetId ?? Guid.Empty));
        if (pathTaken)
        {
            return Task.FromResult<SaveTilesetResult>(
                new SaveTilesetResult.ValidationFailed("Chemin logique déjà utilisé."));
        }

        if (request.Definition.EditorPaletteId is int paletteId)
        {
            var paletteTaken = _drafts.Values.Any(d =>
                d.Definition.EditorPaletteId == paletteId && d.Id != (request.TilesetId ?? Guid.Empty));
            if (paletteTaken)
            {
                return Task.FromResult<SaveTilesetResult>(
                    new SaveTilesetResult.ValidationFailed("EditorPaletteId déjà utilisé."));
            }
        }

        Guid id;
        long newRevision;
        if (request.TilesetId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return Task.FromResult<SaveTilesetResult>(new SaveTilesetResult.Conflict(0));
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
                return Task.FromResult<SaveTilesetResult>(new SaveTilesetResult.Conflict(0));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveTilesetResult>(new SaveTilesetResult.Conflict(current.Revision));
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

        return Task.FromResult<SaveTilesetResult>(
            new SaveTilesetResult.Success(newRevision, id, publishedRevision));
    }

    public Task<StoredTileset?> LoadByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(tilesetId, out var d))
        {
            return Task.FromResult<StoredTileset?>(null);
        }

        return Task.FromResult<StoredTileset?>(new StoredTileset
        {
            TilesetId = d.Id,
            Definition = Clone(d.Definition),
            Revision = d.Revision,
            Status = d.Status,
            PublishedRevision = d.PublishedRevision,
        });
    }

    public Task<StoredTileset?> LoadPublishedByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(tilesetId, out var p))
        {
            return Task.FromResult<StoredTileset?>(null);
        }

        return Task.FromResult<StoredTileset?>(new StoredTileset
        {
            TilesetId = p.Id,
            Definition = Clone(p.Definition),
            Revision = p.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = p.Revision,
        });
    }

    public Task<IReadOnlyList<TilesetCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> q = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.LogicalPath.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter is { } st)
        {
            q = q.Where(d => d.Status == st);
        }

        var list = q
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new TilesetCatalogEntry
            {
                TilesetId = d.Id,
                Name = d.Definition.Name,
                LogicalPath = d.Definition.LogicalPath,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
                EditorPaletteId = d.Definition.EditorPaletteId,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<TilesetCatalogEntry>>(list);
    }

    public Task<DeleteTilesetResult> DeleteAsync(Guid tilesetId, CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(tilesetId, out var d))
        {
            return Task.FromResult<DeleteTilesetResult>(new DeleteTilesetResult.NotFound());
        }

        if (d.Definition.EditorPaletteId is int palette
            && (_paletteReferenced?.Invoke(palette) ?? false))
        {
            return Task.FromResult<DeleteTilesetResult>(
                new DeleteTilesetResult.Referenced(
                    $"Tileset référencé par des cartes (palette {palette})."));
        }

        _drafts.TryRemove(tilesetId, out _);
        _published.TryRemove(tilesetId, out _);
        return Task.FromResult<DeleteTilesetResult>(new DeleteTilesetResult.Success());
    }

    public Task<bool> IsPaletteIdReferencedByMapsAsync(
        int editorPaletteId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_paletteReferenced?.Invoke(editorPaletteId) ?? false);

    public async Task<IReadOnlyList<TilesetDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => Clone(p.Definition))
            .ToList();
        return await Task.FromResult<IReadOnlyList<TilesetDefinition>>(list).ConfigureAwait(false);
    }

    private static TilesetDefinition Clone(TilesetDefinition src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        LogicalPath = src.LogicalPath,
        TileSizePixels = src.TileSizePixels,
        WidthPixels = src.WidthPixels,
        HeightPixels = src.HeightPixels,
        Sha256Hex = src.Sha256Hex,
        EditorPaletteId = src.EditorPaletteId,
    };

    private sealed record DraftRecord(
        Guid Id,
        TilesetDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, TilesetDefinition Definition, long Revision);
}
