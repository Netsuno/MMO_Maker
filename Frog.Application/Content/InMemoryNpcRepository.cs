using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryNpcRepository : INpcRepository, IPublishedNpcCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();
    private readonly Func<int, bool>? _aliasReferenced;

    public InMemoryNpcRepository(
        ContentRepositoryCapabilities? capabilities = null,
        Func<int, bool>? aliasReferenced = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
        _aliasReferenced = aliasReferenced;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveNpcResult> SaveAsync(
        SaveNpcRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveNpcResult>(
                new SaveNpcResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveNpcResult>(new SaveNpcResult.ValidationFailed(error!));
        }

        if (request.Definition.EditorAliasId is int aliasId)
        {
            var aliasTaken = _drafts.Values.Any(d =>
                d.Definition.EditorAliasId == aliasId && d.Id != (request.NpcId ?? Guid.Empty));
            if (aliasTaken)
            {
                return Task.FromResult<SaveNpcResult>(
                    new SaveNpcResult.ValidationFailed("EditorAliasId déjà utilisé."));
            }
        }

        Guid id;
        long newRevision;
        if (request.NpcId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return Task.FromResult<SaveNpcResult>(new SaveNpcResult.Conflict(0));
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = Clone(request.Definition);
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
                return Task.FromResult<SaveNpcResult>(new SaveNpcResult.Conflict(0));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveNpcResult>(new SaveNpcResult.Conflict(current.Revision));
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = Clone(request.Definition);
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

        return Task.FromResult<SaveNpcResult>(
            new SaveNpcResult.Success(newRevision, id, publishedRevision));
    }

    public Task<StoredNpc?> LoadByIdAsync(Guid npcId, CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(npcId, out var draft))
        {
            return Task.FromResult<StoredNpc?>(null);
        }

        return Task.FromResult<StoredNpc?>(new StoredNpc
        {
            NpcId = draft.Id,
            Definition = Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredNpc?> LoadPublishedByIdAsync(
        Guid npcId,
        CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(npcId, out var published))
        {
            return Task.FromResult<StoredNpc?>(null);
        }

        return Task.FromResult<StoredNpc?>(new StoredNpc
        {
            NpcId = published.Id,
            Definition = Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<NpcCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.SpriteLogicalPath.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new NpcCatalogEntry
            {
                NpcId = d.Id,
                Name = d.Definition.Name,
                Kind = d.Definition.Kind,
                SpriteLogicalPath = d.Definition.SpriteLogicalPath,
                Level = d.Definition.Level,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
                EditorAliasId = d.Definition.EditorAliasId,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<NpcCatalogEntry>>(list);
    }

    public Task<DeleteNpcResult> DeleteAsync(Guid npcId, CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(npcId, out var draft))
        {
            return Task.FromResult<DeleteNpcResult>(new DeleteNpcResult.NotFound());
        }

        if (draft.Definition.EditorAliasId is int alias
            && (_aliasReferenced?.Invoke(alias) ?? false))
        {
            return Task.FromResult<DeleteNpcResult>(
                new DeleteNpcResult.Referenced($"NPC référencé par des cartes (alias {alias})."));
        }

        _drafts.TryRemove(npcId, out _);
        _published.TryRemove(npcId, out _);
        return Task.FromResult<DeleteNpcResult>(new DeleteNpcResult.Success());
    }

    public Task<bool> IsAliasIdReferencedByMapsAsync(
        int editorAliasId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_aliasReferenced?.Invoke(editorAliasId) ?? false);

    public async Task<IReadOnlyList<NpcDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => Clone(p.Definition))
            .ToList();
        return await Task.FromResult<IReadOnlyList<NpcDefinition>>(list).ConfigureAwait(false);
    }

    private static NpcDefinition Clone(NpcDefinition source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Kind = source.Kind,
        SpriteLogicalPath = source.SpriteLogicalPath,
        Level = source.Level,
        Notes = source.Notes,
        EditorAliasId = source.EditorAliasId,
    };

    private sealed record DraftRecord(
        Guid Id,
        NpcDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, NpcDefinition Definition, long Revision);
}
