using System.Collections.Concurrent;
using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryResourceSpawnRepository :
    IResourceSpawnRepository,
    IPublishedResourceSpawnCatalog,
    IResourceSpawnReferenceCatalog
{
    private readonly IMapRepository _maps;
    private readonly IPublishedResourceCatalog _resources;
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _publishedTips = new();
    private readonly ConcurrentDictionary<(Guid SpawnId, long Revision), PublishedRecord> _snapshots =
        new();

    public InMemoryResourceSpawnRepository(
        IMapRepository maps,
        IPublishedResourceCatalog resources,
        ContentRepositoryCapabilities? capabilities = null)
    {
        _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
        if (resources is InMemoryResourceRepository resourceRepository)
        {
            resourceRepository.RegisterSpawnReferences(this);
        }
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public async Task<SaveResourceSpawnResult> SaveAsync(
        SaveResourceSpawnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return new SaveResourceSpawnResult.NotDurable(
                "Persistance mémoire démo désactivée.");
        }

        if (!request.Definition.Validate(out var error))
        {
            return new SaveResourceSpawnResult.ValidationFailed(error!);
        }

        if (await _maps.LoadByIdAsync(request.Definition.MapId, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                $"La carte {request.Definition.MapId:N} doit exister.");
        }

        if (await _resources.LoadPublishedByIdAsync(
                request.Definition.ResourceId,
                cancellationToken).ConfigureAwait(false) is null)
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                $"La ressource {request.Definition.ResourceId:N} doit être publiée.");
        }

        Guid id;
        long newRevision;
        if (request.SpawnId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveResourceSpawnResult.Conflict(0);
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = ResourceSpawnWorkspaceSession.Clone(request.Definition);
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
                return new SaveResourceSpawnResult.Conflict(0);
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return new SaveResourceSpawnResult.Conflict(current.Revision);
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = ResourceSpawnWorkspaceSession.Clone(request.Definition);
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
                ResourceSpawnWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _snapshots[(id, publishedRevision.Value)] = snapshot;
            _publishedTips[id] = snapshot;
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return new SaveResourceSpawnResult.Success(newRevision, id, publishedRevision);
    }

    public Task<StoredResourceSpawn?> LoadByIdAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(spawnId, out var draft))
        {
            return Task.FromResult<StoredResourceSpawn?>(null);
        }

        return Task.FromResult<StoredResourceSpawn?>(ToStored(draft));
    }

    public Task<StoredResourceSpawn?> LoadPublishedByIdAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default)
    {
        if (!_publishedTips.TryGetValue(spawnId, out var published))
        {
            return Task.FromResult<StoredResourceSpawn?>(null);
        }

        return Task.FromResult<StoredResourceSpawn?>(new StoredResourceSpawn
        {
            SpawnId = published.Id,
            Definition = ResourceSpawnWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<ResourceSpawnCatalogEntry>> ListSummariesAsync(
        Guid? mapId = null,
        Guid? resourceId = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (mapId is Guid map)
        {
            query = query.Where(d => d.Definition.MapId == map);
        }

        if (resourceId is Guid resource)
        {
            query = query.Where(d => d.Definition.ResourceId == resource);
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.MapId)
            .ThenBy(d => d.Definition.TileY)
            .ThenBy(d => d.Definition.TileX)
            .Select(d => new ResourceSpawnCatalogEntry
            {
                SpawnId = d.Id,
                MapId = d.Definition.MapId,
                ResourceId = d.Definition.ResourceId,
                TileX = d.Definition.TileX,
                TileY = d.Definition.TileY,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ResourceSpawnCatalogEntry>>(list);
    }

    public Task<DeleteResourceSpawnResult> DeleteAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryRemove(spawnId, out _))
        {
            return Task.FromResult<DeleteResourceSpawnResult>(
                new DeleteResourceSpawnResult.NotFound());
        }

        _publishedTips.TryRemove(spawnId, out _);
        foreach (var key in _snapshots.Keys.Where(key => key.SpawnId == spawnId))
        {
            _snapshots.TryRemove(key, out _);
        }

        return Task.FromResult<DeleteResourceSpawnResult>(
            new DeleteResourceSpawnResult.Success());
    }

    public Task<IReadOnlyList<ResourceSpawnDefinition>> ListPublishedAsync(
        Guid? mapId = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<PublishedRecord> query = _publishedTips.Values;
        if (mapId is Guid map)
        {
            query = query.Where(p => p.Definition.MapId == map);
        }

        var list = query
            .OrderBy(p => p.Definition.MapId)
            .ThenBy(p => p.Definition.TileY)
            .ThenBy(p => p.Definition.TileX)
            .Select(p => ResourceSpawnWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<ResourceSpawnDefinition>>(list);
    }

    public Task<bool> IsResourceReferencedAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        var referenced = _drafts.Values.Any(d => d.Definition.ResourceId == resourceId)
            || _snapshots.Values.Any(s => s.Definition.ResourceId == resourceId);
        return Task.FromResult(referenced);
    }

    private static StoredResourceSpawn ToStored(DraftRecord draft) => new()
    {
        SpawnId = draft.Id,
        Definition = ResourceSpawnWorkspaceSession.Clone(draft.Definition),
        Revision = draft.Revision,
        Status = draft.Status,
        PublishedRevision = draft.PublishedRevision,
    };

    private sealed record DraftRecord(
        Guid Id,
        ResourceSpawnDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, ResourceSpawnDefinition Definition, long Revision);
}
