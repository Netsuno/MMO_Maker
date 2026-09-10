using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryResourceRepository :
    IResourceRepository,
    IPublishedResourceCatalog,
    IResourceItemReferenceCatalog
{
    private readonly IPublishedItemCatalog _items;
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _publishedTips = new();
    private readonly ConcurrentDictionary<(Guid ResourceId, long Revision), PublishedRecord> _snapshots =
        new();
    private IResourceSpawnReferenceCatalog? _spawnReferences;

    public InMemoryResourceRepository(
        IPublishedItemCatalog items,
        ContentRepositoryCapabilities? capabilities = null)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
        if (items is InMemoryItemRepository itemRepository)
        {
            itemRepository.RegisterResourceReferences(this);
        }
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    internal void RegisterSpawnReferences(IResourceSpawnReferenceCatalog spawnReferences)
    {
        _spawnReferences = spawnReferences ?? throw new ArgumentNullException(nameof(spawnReferences));
    }

    public async Task<SaveResourceResult> SaveAsync(
        SaveResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return new SaveResourceResult.NotDurable("Persistance mémoire démo désactivée.");
        }

        if (!request.Definition.Validate(out var error))
        {
            return new SaveResourceResult.ValidationFailed(error!);
        }

        if (await _items.LoadPublishedByIdAsync(request.Definition.YieldItemId, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            return new SaveResourceResult.ValidationFailed(
                $"L’objet produit {request.Definition.YieldItemId:N} doit être publié.");
        }

        if (request.Definition.ToolItemId is Guid toolItemId
            && await _items.LoadPublishedByIdAsync(toolItemId, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            return new SaveResourceResult.ValidationFailed(
                $"L’outil {toolItemId:N} doit exister dans le catalogue publié.");
        }

        Guid id;
        long newRevision;
        if (request.ResourceId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveResourceResult.Conflict(0);
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = ResourceWorkspaceSession.Clone(request.Definition);
            definition.Id = id;
            _drafts[id] = new DraftRecord(
                id,
                definition,
                newRevision,
                ContentPublishStatus.Draft,
                null);
        }
        else
        {
            if (!_drafts.TryGetValue(existing, out var current))
            {
                return new SaveResourceResult.Conflict(0);
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return new SaveResourceResult.Conflict(current.Revision);
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = ResourceWorkspaceSession.Clone(request.Definition);
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
            var snapshot = new PublishedRecord(
                id,
                ResourceWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _snapshots[(id, publishedRevision.Value)] = snapshot;
            _publishedTips[id] = snapshot;
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return new SaveResourceResult.Success(newRevision, id, publishedRevision);
    }

    public Task<StoredResource?> LoadByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(resourceId, out var draft))
        {
            return Task.FromResult<StoredResource?>(null);
        }

        return Task.FromResult<StoredResource?>(ToStored(draft));
    }

    public Task<StoredResource?> LoadPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        if (!_publishedTips.TryGetValue(resourceId, out var published))
        {
            return Task.FromResult<StoredResource?>(null);
        }

        return Task.FromResult<StoredResource?>(new StoredResource
        {
            ResourceId = published.Id,
            Definition = ResourceWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<ResourceCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.SpriteLogicalPath.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (d.Definition.Description?.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new ResourceCatalogEntry
            {
                ResourceId = d.Id,
                Name = d.Definition.Name,
                SpriteLogicalPath = d.Definition.SpriteLogicalPath,
                RespawnSeconds = d.Definition.RespawnSeconds,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ResourceCatalogEntry>>(list);
    }

    public async Task<DeleteResourceResult> DeleteAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        if (_spawnReferences is not null
            && await _spawnReferences.IsResourceReferencedAsync(resourceId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new DeleteResourceResult.Referenced(
                "La ressource est référencée par un brouillon ou un snapshot publié de spawn.");
        }

        if (!_drafts.TryRemove(resourceId, out _))
        {
            return new DeleteResourceResult.NotFound();
        }

        _publishedTips.TryRemove(resourceId, out _);
        foreach (var key in _snapshots.Keys.Where(key => key.ResourceId == resourceId))
        {
            _snapshots.TryRemove(key, out _);
        }

        return new DeleteResourceResult.Success();
    }

    public Task<IReadOnlyList<ResourceDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _publishedTips.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => ResourceWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<ResourceDefinition>>(list);
    }

    async Task<ResourceDefinition?> IPublishedResourceCatalog.LoadPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var stored = await LoadPublishedByIdAsync(resourceId, cancellationToken).ConfigureAwait(false);
        return stored is null ? null : ResourceWorkspaceSession.Clone(stored.Definition);
    }

    public Task<bool> IsItemReferencedAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var referenced = _drafts.Values.Any(draft =>
                draft.Definition.YieldItemId == itemId || draft.Definition.ToolItemId == itemId)
            || _snapshots.Values.Any(snapshot =>
                snapshot.Definition.YieldItemId == itemId || snapshot.Definition.ToolItemId == itemId);
        return Task.FromResult(referenced);
    }

    private static StoredResource ToStored(DraftRecord draft) => new()
    {
        ResourceId = draft.Id,
        Definition = ResourceWorkspaceSession.Clone(draft.Definition),
        Revision = draft.Revision,
        Status = draft.Status,
        PublishedRevision = draft.PublishedRevision,
    };

    private sealed record DraftRecord(
        Guid Id,
        ResourceDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, ResourceDefinition Definition, long Revision);
}
