using System.Collections.Concurrent;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryActorRepository :
    IActorRepository,
    IPublishedActorCatalog,
    IActorClassReferenceCatalog,
    IActorItemReferenceCatalog
{
    private readonly IClassRepository _classes;
    private readonly IPublishedItemCatalog _items;
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();

    public InMemoryActorRepository(
        IClassRepository classes,
        IPublishedItemCatalog items,
        ContentRepositoryCapabilities? capabilities = null)
    {
        _classes = classes ?? throw new ArgumentNullException(nameof(classes));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
        if (classes is InMemoryClassRepository classRepository)
        {
            classRepository.RegisterActorReferences(this);
        }

        if (items is InMemoryItemRepository itemRepository)
        {
            itemRepository.RegisterActorReferences(this);
        }
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public async Task<SaveActorResult> SaveAsync(
        SaveActorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return new SaveActorResult.NotDurable("Persistance mémoire démo désactivée.");
        }

        if (!request.Definition.Validate(out var error))
        {
            return new SaveActorResult.ValidationFailed(error!);
        }

        var referenceError = await ValidateReferencesAsync(request.Definition, cancellationToken)
            .ConfigureAwait(false);
        if (referenceError is not null)
        {
            return new SaveActorResult.ValidationFailed(referenceError);
        }

        Guid id;
        long newRevision;
        if (request.ActorId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveActorResult.Conflict(0);
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = ActorWorkspaceSession.Clone(request.Definition);
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
                return new SaveActorResult.Conflict(0);
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return new SaveActorResult.Conflict(current.Revision);
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = ActorWorkspaceSession.Clone(request.Definition);
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
                ActorWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return new SaveActorResult.Success(newRevision, id, publishedRevision);
    }

    public Task<StoredActor?> LoadByIdAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(actorId, out var draft))
        {
            return Task.FromResult<StoredActor?>(null);
        }

        return Task.FromResult<StoredActor?>(new StoredActor
        {
            ActorId = draft.Id,
            Definition = ActorWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredActor?> LoadPublishedByIdAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(actorId, out var published))
        {
            return Task.FromResult<StoredActor?>(null);
        }

        return Task.FromResult<StoredActor?>(new StoredActor
        {
            ActorId = published.Id,
            Definition = ActorWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<ActorCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (d.Definition.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.Definition.FaceLogicalPath?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new ActorCatalogEntry
            {
                ActorId = d.Id,
                Name = d.Definition.Name,
                ClassId = d.Definition.ClassId,
                BaseHp = d.Definition.BaseHp,
                BaseMp = d.Definition.BaseMp,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ActorCatalogEntry>>(list);
    }

    public Task<DeleteActorResult> DeleteAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryRemove(actorId, out _))
        {
            return Task.FromResult<DeleteActorResult>(new DeleteActorResult.NotFound());
        }

        _published.TryRemove(actorId, out _);
        return Task.FromResult<DeleteActorResult>(new DeleteActorResult.Success());
    }

    public Task<IReadOnlyList<ActorDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => ActorWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<ActorDefinition>>(list);
    }

    public Task<bool> IsClassReferencedAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        var referenced = _drafts.Values.Any(draft => draft.Definition.ClassId == classId)
            || _published.Values.Any(published => published.Definition.ClassId == classId);
        return Task.FromResult(referenced);
    }

    public Task<bool> IsItemReferencedAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var referenced = _drafts.Values.Any(draft => ReferencesItem(draft.Definition, itemId))
            || _published.Values.Any(published => ReferencesItem(published.Definition, itemId));
        return Task.FromResult(referenced);
    }

    private async Task<string?> ValidateReferencesAsync(
        ActorDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ClassId is Guid classId
            && await _classes.LoadPublishedByIdAsync(classId, cancellationToken).ConfigureAwait(false) is null)
        {
            return "La classe liée doit exister dans le catalogue publié.";
        }

        if (definition.StartingWeaponItemId is Guid weaponId)
        {
            var weapon = await _items.LoadPublishedByIdAsync(weaponId, cancellationToken).ConfigureAwait(false);
            if (weapon is null || weapon.Kind != ItemType.Weapon)
            {
                return "L’arme de départ doit être un objet publié de type arme.";
            }
        }

        if (definition.StartingArmorItemId is Guid armorId)
        {
            var armor = await _items.LoadPublishedByIdAsync(armorId, cancellationToken).ConfigureAwait(false);
            if (armor is null || armor.Kind != ItemType.Armor)
            {
                return "L’armure de départ doit être un objet publié de type armure.";
            }
        }

        return null;
    }

    private static bool ReferencesItem(ActorDefinition definition, Guid itemId)
        => definition.StartingWeaponItemId == itemId || definition.StartingArmorItemId == itemId;

    private sealed record DraftRecord(
        Guid Id,
        ActorDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, ActorDefinition Definition, long Revision);
}
