using System.Collections.Concurrent;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemorySystemFlagRepository : ISystemFlagRepository, IPublishedSystemFlagCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();

    public InMemorySystemFlagRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveSystemFlagResult> SaveAsync(
        SaveSystemFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveSystemFlagResult>(
                new SaveSystemFlagResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveSystemFlagResult>(new SaveSystemFlagResult.ValidationFailed(error!));
        }

        var definition = Normalize(request.Definition);
        Guid id;
        long newRevision;
        if (request.FlagId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return Task.FromResult<SaveSystemFlagResult>(new SaveSystemFlagResult.Conflict(0));
            }

            id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
            if (KeyTaken(definition.Kind, definition.Key, exceptId: null))
            {
                return Task.FromResult<SaveSystemFlagResult>(new SaveSystemFlagResult.ValidationFailed(DuplicateKeyMessage(definition.Kind)));
            }

            newRevision = 1;
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
                return Task.FromResult<SaveSystemFlagResult>(new SaveSystemFlagResult.Conflict(0));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveSystemFlagResult>(new SaveSystemFlagResult.Conflict(current.Revision));
            }

            if (KeyTaken(definition.Kind, definition.Key, existing))
            {
                return Task.FromResult<SaveSystemFlagResult>(new SaveSystemFlagResult.ValidationFailed(DuplicateKeyMessage(definition.Kind)));
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
            _published[id] = new PublishedRecord(
                id,
                SystemFlagWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return Task.FromResult<SaveSystemFlagResult>(new SaveSystemFlagResult.Success(newRevision, id, publishedRevision));
    }

    public Task<StoredSystemFlag?> LoadByIdAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(flagId, out var draft))
        {
            return Task.FromResult<StoredSystemFlag?>(null);
        }

        return Task.FromResult<StoredSystemFlag?>(new StoredSystemFlag
        {
            FlagId = draft.Id,
            Definition = SystemFlagWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredSystemFlag?> LoadPublishedByIdAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(flagId, out var published))
        {
            return Task.FromResult<StoredSystemFlag?>(null);
        }

        return Task.FromResult<StoredSystemFlag?>(new StoredSystemFlag
        {
            FlagId = published.Id,
            Definition = SystemFlagWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<SystemFlagCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        SystemFlagKind? kindFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (kindFilter is SystemFlagKind kind)
        {
            query = query.Where(d => d.Definition.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.Key.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (d.Definition.Note?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Definition.Key, StringComparer.Ordinal)
            .Select(d => new SystemFlagCatalogEntry
            {
                FlagId = d.Id,
                Kind = d.Definition.Kind,
                Key = d.Definition.Key,
                Label = d.Definition.Label,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<SystemFlagCatalogEntry>>(list);
    }

    public Task<DeleteSystemFlagResult> DeleteAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryRemove(flagId, out _))
        {
            return Task.FromResult<DeleteSystemFlagResult>(new DeleteSystemFlagResult.NotFound());
        }

        _published.TryRemove(flagId, out _);
        return Task.FromResult<DeleteSystemFlagResult>(new DeleteSystemFlagResult.Success());
    }

    public Task<IReadOnlyList<SystemFlagDefinition>> ListPublishedAsync(
        SystemFlagKind? kindFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<PublishedRecord> query = _published.Values;
        if (kindFilter is SystemFlagKind kind)
        {
            query = query.Where(p => p.Definition.Kind == kind);
        }

        var list = query
            .OrderBy(p => p.Definition.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Definition.Key, StringComparer.Ordinal)
            .Select(p => SystemFlagWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<SystemFlagDefinition>>(list);
    }

    private bool KeyTaken(SystemFlagKind kind, string key, Guid? exceptId)
        => _drafts.Values.Any(draft =>
            draft.Id != exceptId
            && draft.Definition.Kind == kind
            && string.Equals(draft.Definition.Key, key, StringComparison.Ordinal));

    private static string DuplicateKeyMessage(SystemFlagKind kind)
        => kind == SystemFlagKind.Variable
            ? "Cette clé de variable existe déjà."
            : "Cette clé d'interrupteur existe déjà.";

    private static SystemFlagDefinition Normalize(SystemFlagDefinition source)
    {
        var copy = SystemFlagWorkspaceSession.Clone(source);
        copy.Key = copy.Key.Trim();
        copy.Label = copy.Label.Trim();
        copy.Note = string.IsNullOrWhiteSpace(copy.Note) ? null : copy.Note.Trim();
        return copy;
    }

    private sealed record DraftRecord(
        Guid Id,
        SystemFlagDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(
        Guid Id,
        SystemFlagDefinition Definition,
        long Revision);
}
