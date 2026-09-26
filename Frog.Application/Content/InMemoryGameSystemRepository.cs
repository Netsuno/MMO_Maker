using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryGameSystemRepository :
    IGameSystemRepository,
    IPublishedGameSystemCatalog
{
    private readonly IPublishedActorCatalog _actors;
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _publishedTips = new();
    private readonly ConcurrentDictionary<(Guid SystemId, long Revision), PublishedRecord> _snapshots = new();

    public InMemoryGameSystemRepository(
        IPublishedActorCatalog actors,
        ContentRepositoryCapabilities? capabilities = null)
    {
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public async Task<SaveGameSystemResult> SaveAsync(
        SaveGameSystemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return new SaveGameSystemResult.NotDurable("Persistance mémoire démo désactivée.");
        }

        if (!request.Definition.Validate(out var error))
        {
            return new SaveGameSystemResult.ValidationFailed(error!);
        }

        var referenceError = await ValidatePartyAsync(request.Definition, cancellationToken).ConfigureAwait(false);
        if (referenceError is not null)
        {
            return new SaveGameSystemResult.ValidationFailed(referenceError);
        }

        Guid id;
        long newRevision;
        if (request.SystemId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveGameSystemResult.Conflict(0);
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = GameSystemWorkspaceSession.Clone(request.Definition);
            definition.Id = id;
            Normalize(definition);
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
                return new SaveGameSystemResult.Conflict(0);
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return new SaveGameSystemResult.Conflict(current.Revision);
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = GameSystemWorkspaceSession.Clone(request.Definition);
            definition.Id = id;
            Normalize(definition);
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
                GameSystemWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _snapshots[(id, publishedRevision.Value)] = snapshot;
            _publishedTips[id] = snapshot;
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return new SaveGameSystemResult.Success(newRevision, id, publishedRevision);
    }

    public Task<StoredGameSystem?> LoadByIdAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(systemId, out var draft))
        {
            return Task.FromResult<StoredGameSystem?>(null);
        }

        return Task.FromResult<StoredGameSystem?>(new StoredGameSystem
        {
            SystemId = draft.Id,
            Definition = GameSystemWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredGameSystem?> LoadPublishedByIdAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        if (!_publishedTips.TryGetValue(systemId, out var published))
        {
            return Task.FromResult<StoredGameSystem?>(null);
        }

        return Task.FromResult<StoredGameSystem?>(new StoredGameSystem
        {
            SystemId = published.Id,
            Definition = GameSystemWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<GameSystemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(draft => MatchesSearch(draft.Definition, search));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(draft => draft.Status == status);
        }

        var list = query
            .OrderBy(draft => draft.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToEntry)
            .ToList();
        return Task.FromResult<IReadOnlyList<GameSystemCatalogEntry>>(list);
    }

    public Task<DeleteGameSystemResult> DeleteAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryRemove(systemId, out _))
        {
            return Task.FromResult<DeleteGameSystemResult>(new DeleteGameSystemResult.NotFound());
        }

        _publishedTips.TryRemove(systemId, out _);
        foreach (var key in _snapshots.Keys.Where(key => key.SystemId == systemId))
        {
            _snapshots.TryRemove(key, out _);
        }

        return Task.FromResult<DeleteGameSystemResult>(new DeleteGameSystemResult.Success());
    }

    public Task<IReadOnlyList<GameSystemDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _publishedTips.Values
            .OrderBy(published => published.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(published => GameSystemWorkspaceSession.Clone(published.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<GameSystemDefinition>>(list);
    }

    private async Task<string?> ValidatePartyAsync(
        GameSystemDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.StartingPartyActorIds.Count == 0)
        {
            return null;
        }

        var published = await _actors.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var known = published.Select(actor => actor.Id).ToHashSet();
        foreach (var actorId in definition.StartingPartyActorIds)
        {
            if (!known.Contains(actorId))
            {
                return $"Le héros {actorId:N} du groupe de départ doit exister dans le catalogue publié.";
            }
        }

        return null;
    }

    private static bool MatchesSearch(GameSystemDefinition definition, string search)
    {
        if (definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || definition.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
            || definition.CurrencyUnit.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (definition.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }

        return definition.Switches.Any(flag => FlagMatches(flag, search))
               || definition.Variables.Any(flag => FlagMatches(flag, search));
    }

    private static bool FlagMatches(NamedWorldFlag flag, string search)
        => flag.Key.Contains(search, StringComparison.OrdinalIgnoreCase)
           || flag.Label.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static GameSystemCatalogEntry ToEntry(DraftRecord draft) => new()
    {
        SystemId = draft.Id,
        Name = draft.Definition.Name,
        Title = draft.Definition.Title,
        CurrencyUnit = draft.Definition.CurrencyUnit,
        SwitchCount = draft.Definition.Switches.Count,
        VariableCount = draft.Definition.Variables.Count,
        PartyCount = draft.Definition.StartingPartyActorIds.Count,
        Revision = draft.Revision,
        Status = draft.Status,
        PublishedRevision = draft.PublishedRevision,
    };

    private static void Normalize(GameSystemDefinition definition)
    {
        definition.Name = definition.Name.Trim();
        definition.Title = definition.Title.Trim();
        definition.CurrencyUnit = definition.CurrencyUnit.Trim();
        definition.Description = string.IsNullOrWhiteSpace(definition.Description)
            ? null
            : definition.Description.Trim();
        foreach (var flag in definition.Switches)
        {
            flag.Key = flag.Key.Trim();
            flag.Label = flag.Label.Trim();
        }

        foreach (var flag in definition.Variables)
        {
            flag.Key = flag.Key.Trim();
            flag.Label = flag.Label.Trim();
        }
    }

    private sealed record DraftRecord(
        Guid Id,
        GameSystemDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, GameSystemDefinition Definition, long Revision);
}
