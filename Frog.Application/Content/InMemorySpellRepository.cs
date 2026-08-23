using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemorySpellRepository : ISpellRepository, IPublishedSpellCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();

    public InMemorySpellRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveSpellResult> SaveAsync(
        SaveSpellRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveSpellResult>(
                new SaveSpellResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveSpellResult>(new SaveSpellResult.ValidationFailed(error!));
        }

        Guid id;
        long newRevision;
        if (request.SpellId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return Task.FromResult<SaveSpellResult>(new SaveSpellResult.Conflict(0));
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = SpellWorkspaceSession.Clone(request.Definition);
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
                return Task.FromResult<SaveSpellResult>(new SaveSpellResult.Conflict(0));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveSpellResult>(
                    new SaveSpellResult.Conflict(current.Revision));
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = SpellWorkspaceSession.Clone(request.Definition);
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
            _published[id] = new PublishedRecord(
                id,
                SpellWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return Task.FromResult<SaveSpellResult>(
            new SaveSpellResult.Success(newRevision, id, publishedRevision));
    }

    public Task<StoredSpell?> LoadByIdAsync(
        Guid spellId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(spellId, out var draft))
        {
            return Task.FromResult<StoredSpell?>(null);
        }

        return Task.FromResult<StoredSpell?>(new StoredSpell
        {
            SpellId = draft.Id,
            Definition = SpellWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredSpell?> LoadPublishedByIdAsync(
        Guid spellId,
        CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(spellId, out var published))
        {
            return Task.FromResult<StoredSpell?>(null);
        }

        return Task.FromResult<StoredSpell?>(new StoredSpell
        {
            SpellId = published.Id,
            Definition = SpellWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<SpellCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.IconLogicalPath.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.Kind.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.TargetType.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new SpellCatalogEntry
            {
                SpellId = d.Id,
                Name = d.Definition.Name,
                Kind = d.Definition.Kind,
                ManaCost = d.Definition.ManaCost,
                CooldownMs = d.Definition.CooldownMs,
                TargetType = d.Definition.TargetType,
                IconLogicalPath = d.Definition.IconLogicalPath,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<SpellCatalogEntry>>(list);
    }

    public Task<DeleteSpellResult> DeleteAsync(
        Guid spellId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryRemove(spellId, out _))
        {
            return Task.FromResult<DeleteSpellResult>(new DeleteSpellResult.NotFound());
        }

        _published.TryRemove(spellId, out _);
        return Task.FromResult<DeleteSpellResult>(new DeleteSpellResult.Success());
    }

    public Task<IReadOnlyList<SpellDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => SpellWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<SpellDefinition>>(list);
    }

    private sealed record DraftRecord(
        Guid Id,
        SpellDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, SpellDefinition Definition, long Revision);
}
