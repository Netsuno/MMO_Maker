using System.Collections.Concurrent;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryClassRepository :
    IClassRepository,
    IPublishedClassCatalog,
    IClassSpellReferenceCatalog,
    IClassItemReferenceCatalog
{
    private readonly ISpellRepository _spells;
    private readonly IPublishedItemCatalog? _items;
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();
    private IActorClassReferenceCatalog? _actorReferences;

    public InMemoryClassRepository(
        ISpellRepository spells,
        ContentRepositoryCapabilities? capabilities = null,
        IPublishedItemCatalog? items = null)
    {
        _spells = spells ?? throw new ArgumentNullException(nameof(spells));
        _items = items;
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
        if (spells is InMemorySpellRepository spellRepository)
        {
            spellRepository.RegisterClassReferences(this);
        }

        if (items is InMemoryItemRepository itemRepository)
        {
            itemRepository.RegisterClassReferences(this);
        }
    }

    internal void RegisterActorReferences(IActorClassReferenceCatalog actorReferences)
    {
        _actorReferences = actorReferences ?? throw new ArgumentNullException(nameof(actorReferences));
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public async Task<SaveClassResult> SaveAsync(
        SaveClassRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return new SaveClassResult.NotDurable("Persistance mémoire démo désactivée.");
        }

        if (!request.Definition.Validate(out var error))
        {
            return new SaveClassResult.ValidationFailed(error!);
        }

        if (request.Definition.StartingSpellId is Guid startingSpellId
            && await _spells.LoadPublishedByIdAsync(startingSpellId, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            return new SaveClassResult.ValidationFailed(
                "Le sort de départ doit exister dans le catalogue publié.");
        }

        var equipmentError = await ValidateEquipmentAsync(request.Definition, cancellationToken)
            .ConfigureAwait(false);
        if (equipmentError is not null)
        {
            return new SaveClassResult.ValidationFailed(equipmentError);
        }

        Guid id;
        long newRevision;
        if (request.ClassId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveClassResult.Conflict(0);
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = ClassWorkspaceSession.Clone(request.Definition);
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
                return new SaveClassResult.Conflict(0);
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return new SaveClassResult.Conflict(current.Revision);
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = ClassWorkspaceSession.Clone(request.Definition);
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
                ClassWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return new SaveClassResult.Success(newRevision, id, publishedRevision);
    }

    public Task<StoredClass?> LoadByIdAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(classId, out var draft))
        {
            return Task.FromResult<StoredClass?>(null);
        }

        return Task.FromResult<StoredClass?>(new StoredClass
        {
            ClassId = draft.Id,
            Definition = ClassWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredClass?> LoadPublishedByIdAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(classId, out var published))
        {
            return Task.FromResult<StoredClass?>(null);
        }

        return Task.FromResult<StoredClass?>(new StoredClass
        {
            ClassId = published.Id,
            Definition = ClassWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<ClassCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
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
            .Select(d => new ClassCatalogEntry
            {
                ClassId = d.Id,
                Name = d.Definition.Name,
                BaseHp = d.Definition.BaseHp,
                BaseMp = d.Definition.BaseMp,
                StartingSpellId = d.Definition.StartingSpellId,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ClassCatalogEntry>>(list);
    }

    public async Task<DeleteClassResult> DeleteAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        if (_actorReferences is not null
            && await _actorReferences.IsClassReferencedAsync(classId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new DeleteClassResult.Referenced(
                "La classe est référencée par un brouillon ou un snapshot publié de héros.");
        }

        if (!_drafts.TryRemove(classId, out _))
        {
            return new DeleteClassResult.NotFound();
        }

        _published.TryRemove(classId, out _);
        return new DeleteClassResult.Success();
    }

    public Task<IReadOnlyList<ClassDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => ClassWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<ClassDefinition>>(list);
    }

    public Task<bool> IsSpellReferencedAsync(
        Guid spellId,
        CancellationToken cancellationToken = default)
    {
        var referenced = _drafts.Values.Any(draft => draft.Definition.StartingSpellId == spellId)
            || _published.Values.Any(published => published.Definition.StartingSpellId == spellId);
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

    private async Task<string?> ValidateEquipmentAsync(
        ClassDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.DefaultWeaponItemId is null && definition.DefaultArmorItemId is null)
        {
            return null;
        }

        if (_items is null)
        {
            return "Le catalogue d’objets est requis pour l’équipement par défaut.";
        }

        if (definition.DefaultWeaponItemId is Guid weaponId)
        {
            var weapon = await _items.LoadPublishedByIdAsync(weaponId, cancellationToken)
                .ConfigureAwait(false);
            if (weapon is null || weapon.Kind != ItemType.Weapon)
            {
                return "L’arme par défaut doit être un objet publié de type arme.";
            }
        }

        if (definition.DefaultArmorItemId is Guid armorId)
        {
            var armor = await _items.LoadPublishedByIdAsync(armorId, cancellationToken)
                .ConfigureAwait(false);
            if (armor is null || armor.Kind != ItemType.Armor)
            {
                return "L’armure par défaut doit être un objet publié de type armure.";
            }
        }

        return null;
    }

    private static bool ReferencesItem(ClassDefinition definition, Guid itemId)
        => definition.DefaultWeaponItemId == itemId || definition.DefaultArmorItemId == itemId;

    private sealed record DraftRecord(
        Guid Id,
        ClassDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, ClassDefinition Definition, long Revision);
}
