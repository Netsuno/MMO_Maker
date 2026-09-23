using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Catalogue d'événements carte en mémoire (tests du raccourci PNJ, sans PostgreSQL).</summary>
public class InMemoryMapEventRepository : IMapEventRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, StoredMapEvent> _items = new();

    public InMemoryMapEventRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public virtual Task<SaveMapEventResult> SaveAsync(SaveMapEventRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveMapEventResult>(
                new SaveMapEventResult.ValidationFailed(error ?? "Événement invalide."));
        }

        lock (_sync)
        {
            if (request.EventId is not Guid eventId || eventId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return Task.FromResult<SaveMapEventResult>(new SaveMapEventResult.Conflict(0));
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                if (_items.ContainsKey(id))
                {
                    return Task.FromResult<SaveMapEventResult>(
                        new SaveMapEventResult.ValidationFailed("Identifiant d'événement déjà utilisé."));
                }

                if (IsSlugTaken(request.Definition.CatalogSlug, except: null))
                {
                    return SlugConflict();
                }

                var publishedRevision = request.Intent == SaveContentIntent.Publish ? 1L : (long?)null;
                _items[id] = new StoredMapEvent
                {
                    EventId = id,
                    Definition = Clone(request.Definition, id),
                    Revision = 1,
                    Status = request.Intent == SaveContentIntent.Publish
                        ? ContentPublishStatus.Published
                        : ContentPublishStatus.Draft,
                    PublishedRevision = publishedRevision,
                };
                return Task.FromResult<SaveMapEventResult>(
                    new SaveMapEventResult.Success(1, id, publishedRevision));
            }

            if (!_items.TryGetValue(eventId, out var current))
            {
                return Task.FromResult<SaveMapEventResult>(
                    new SaveMapEventResult.ValidationFailed("Événement introuvable."));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveMapEventResult>(new SaveMapEventResult.Conflict(current.Revision));
            }

            if (IsSlugTaken(request.Definition.CatalogSlug, eventId))
            {
                return SlugConflict();
            }

            var newRevision = current.Revision + 1;
            var published = current.PublishedRevision;
            var status = ContentPublishStatus.Draft;
            if (request.Intent == SaveContentIntent.Publish)
            {
                published = newRevision;
                status = ContentPublishStatus.Published;
            }

            _items[eventId] = new StoredMapEvent
            {
                EventId = eventId,
                Definition = Clone(request.Definition, eventId),
                Revision = newRevision,
                Status = status,
                PublishedRevision = published,
            };
            return Task.FromResult<SaveMapEventResult>(
                new SaveMapEventResult.Success(newRevision, eventId, published));
        }
    }

    public Task<StoredMapEvent?> LoadByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_items.TryGetValue(eventId, out var stored) ? CloneStored(stored) : null);
        }
    }

    public Task<StoredMapEvent?> LoadPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_items.TryGetValue(eventId, out var stored) || stored.Status != ContentPublishStatus.Published)
            {
                return Task.FromResult<StoredMapEvent?>(null);
            }

            return Task.FromResult<StoredMapEvent?>(CloneStored(stored));
        }
    }

    public Task<IReadOnlyList<MapEventCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IEnumerable<StoredMapEvent> rows = _items.Values;
            if (statusFilter is ContentPublishStatus status)
            {
                rows = rows.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                rows = rows.Where(r =>
                    r.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (r.Definition.CatalogSlug?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var list = rows
                .OrderBy(r => r.Definition.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => new MapEventCatalogEntry
                {
                    EventId = r.EventId,
                    Name = r.Definition.Name,
                    CatalogSlug = r.Definition.CatalogSlug,
                    Revision = r.Revision,
                    Status = r.Status,
                    PublishedRevision = r.PublishedRevision,
                    EditorAliasId = r.Definition.EditorAliasId,
                    PageCount = r.Definition.Pages.Count,
                })
                .ToList();
            return Task.FromResult<IReadOnlyList<MapEventCatalogEntry>>(list);
        }
    }

    public Task<DeleteMapEventResult> DeleteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_items.Remove(eventId))
            {
                return Task.FromResult<DeleteMapEventResult>(new DeleteMapEventResult.NotFound());
            }

            return Task.FromResult<DeleteMapEventResult>(new DeleteMapEventResult.Success());
        }
    }

    public Task<bool> IsReferencedByMapPlacementsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    private bool IsSlugTaken(string? slug, Guid? except)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        return _items.Values.Any(item =>
            item.EventId != except
            && string.Equals(item.Definition.CatalogSlug, slug, StringComparison.Ordinal));
    }

    private static Task<SaveMapEventResult> SlugConflict() =>
        Task.FromResult<SaveMapEventResult>(
            new SaveMapEventResult.PersistenceFailed("Slug catalogue déjà utilisé."));

    private static StoredMapEvent CloneStored(StoredMapEvent stored) =>
        new()
        {
            EventId = stored.EventId,
            Definition = Clone(stored.Definition, stored.EventId),
            Revision = stored.Revision,
            Status = stored.Status,
            PublishedRevision = stored.PublishedRevision,
        };

    private static MapEventDefinition Clone(MapEventDefinition source, Guid id)
    {
        var json = MapEventPagesCodec.SerializePages(source.Pages);
        if (!MapEventPagesCodec.TryDeserializePages(json, out var pages, out var error))
        {
            throw new InvalidOperationException(error ?? "Pages invalides.");
        }

        return new MapEventDefinition
        {
            Id = id,
            Name = source.Name,
            CatalogSlug = source.CatalogSlug,
            EditorAliasId = source.EditorAliasId,
            Pages = pages,
        };
    }
}
