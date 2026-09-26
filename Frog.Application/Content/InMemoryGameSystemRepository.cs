using System.Collections.Concurrent;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryGameSystemRepository : IGameSystemRepository, IPublishedGameSystemCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();
    private readonly object _gate = new();

    public InMemoryGameSystemRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveGameSystemResult> SaveAsync(
        SaveGameSystemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveGameSystemResult>(
                new SaveGameSystemResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        var definition = GameSystemWorkspaceSession.Clone(request.Definition);
        Normalize(definition);
        if (!definition.Validate(out var error))
        {
            return Task.FromResult<SaveGameSystemResult>(new SaveGameSystemResult.ValidationFailed(error!));
        }

        lock (_gate)
        {
            Guid id;
            long newRevision;
            if (request.EntryId is not Guid existing || existing == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return Task.FromResult<SaveGameSystemResult>(new SaveGameSystemResult.Conflict(0));
                }

                id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
                if (DuplicateMessage(definition, id) is { } duplicate)
                {
                    return Task.FromResult<SaveGameSystemResult>(
                        new SaveGameSystemResult.ValidationFailed(duplicate));
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
                    return Task.FromResult<SaveGameSystemResult>(new SaveGameSystemResult.Conflict(0));
                }

                if (current.Revision != request.ExpectedRevision)
                {
                    return Task.FromResult<SaveGameSystemResult>(
                        new SaveGameSystemResult.Conflict(current.Revision));
                }

                if (DuplicateMessage(definition, existing) is { } duplicate)
                {
                    return Task.FromResult<SaveGameSystemResult>(
                        new SaveGameSystemResult.ValidationFailed(duplicate));
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
                    GameSystemWorkspaceSession.Clone(draft.Definition),
                    publishedRevision.Value);
                _drafts[id] = draft with
                {
                    Status = ContentPublishStatus.Published,
                    PublishedRevision = publishedRevision,
                };
            }

            return Task.FromResult<SaveGameSystemResult>(
                new SaveGameSystemResult.Success(newRevision, id, publishedRevision));
        }
    }

    public Task<StoredGameSystemEntry?> LoadByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(entryId, out var draft))
        {
            return Task.FromResult<StoredGameSystemEntry?>(null);
        }

        return Task.FromResult<StoredGameSystemEntry?>(new StoredGameSystemEntry
        {
            EntryId = draft.Id,
            Definition = GameSystemWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredGameSystemEntry?> LoadPublishedByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(entryId, out var published))
        {
            return Task.FromResult<StoredGameSystemEntry?>(null);
        }

        return Task.FromResult<StoredGameSystemEntry?>(new StoredGameSystemEntry
        {
            EntryId = published.Id,
            Definition = GameSystemWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<GameSystemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        GameSystemEntryKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (kind is GameSystemEntryKind expected)
        {
            query = query.Where(d => d.Definition.Kind == expected);
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
            .Select(d => new GameSystemCatalogEntry
            {
                EntryId = d.Id,
                Kind = d.Definition.Kind,
                Key = d.Definition.Key,
                Label = d.Definition.Label,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<GameSystemCatalogEntry>>(list);
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(
        GameSystemEntryKind kind,
        CancellationToken cancellationToken = default)
    {
        var keys = _drafts.Values
            .Where(d => d.Definition.Kind == kind)
            .Select(d => d.Definition.Key)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    public Task<DeleteGameSystemResult> DeleteAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_drafts.TryRemove(entryId, out _))
            {
                return Task.FromResult<DeleteGameSystemResult>(new DeleteGameSystemResult.NotFound());
            }

            _published.TryRemove(entryId, out _);
            return Task.FromResult<DeleteGameSystemResult>(new DeleteGameSystemResult.Success());
        }
    }

    public Task<IReadOnlyList<GameSystemEntryDefinition>> ListPublishedAsync(
        GameSystemEntryKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<PublishedRecord> query = _published.Values;
        if (kind is GameSystemEntryKind expected)
        {
            query = query.Where(p => p.Definition.Kind == expected);
        }

        var list = query
            .OrderBy(p => p.Definition.Label, StringComparer.OrdinalIgnoreCase)
            .Select(p => GameSystemWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<GameSystemEntryDefinition>>(list);
    }

    private string? DuplicateMessage(GameSystemEntryDefinition definition, Guid selfId)
    {
        if (definition.Kind == GameSystemEntryKind.Options)
        {
            var otherOptions = _drafts.Values.Any(d =>
                d.Id != selfId && d.Definition.Kind == GameSystemEntryKind.Options);
            return otherOptions ? "Les options du projet existent déjà." : null;
        }

        var clash = _drafts.Values.Any(d =>
            d.Id != selfId
            && d.Definition.Kind == definition.Kind
            && string.Equals(d.Definition.Key, definition.Key, StringComparison.Ordinal));
        if (!clash)
        {
            return null;
        }

        return GameSystemEntryDefinition.DuplicateKeyMessage(definition.Kind, definition.Key);
    }

    private static void Normalize(GameSystemEntryDefinition definition)
    {
        definition.Label = definition.Label.Trim();
        definition.Note = string.IsNullOrWhiteSpace(definition.Note) ? null : definition.Note.Trim();
        if (definition.Kind == GameSystemEntryKind.Options)
        {
            definition.Key = string.Empty;
            if (MapAudioTrack.TryCreate(
                    definition.StartingBgmAsset,
                    definition.StartingBgmVolume,
                    definition.StartingBgmFadeMs,
                    "Musique de départ",
                    out var track,
                    out _))
            {
                definition.StartingBgmAsset = track.Asset;
                definition.StartingBgmVolume = track.Volume;
                definition.StartingBgmFadeMs = track.FadeMs;
            }

            return;
        }

        definition.Key = definition.Key.Trim();
        definition.StartingBgmAsset = string.Empty;
        definition.StartingBgmVolume = MapAudioTrack.DefaultVolume;
        definition.StartingBgmFadeMs = 0;
    }

    private sealed record DraftRecord(
        Guid Id,
        GameSystemEntryDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, GameSystemEntryDefinition Definition, long Revision);
}
