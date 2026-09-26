using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur héros : catalogue + brouillon courant.</summary>
public sealed class ActorWorkspaceSession
{
    private readonly IActorRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public ActorWorkspaceSession(IActorRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public IReadOnlyList<ActorCatalogEntry> Catalog { get; private set; } = Array.Empty<ActorCatalogEntry>();

    public ActorDefinition? Current { get; private set; }

    public Guid? CurrentId { get; private set; }

    public long CurrentRevision { get; private set; }

    public ContentPublishStatus CurrentStatus { get; private set; } = ContentPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public string? SearchFilter { get; set; }

    public ContentPublishStatus? StatusFilter { get; set; }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        Catalog = await _repository
            .ListSummariesAsync(SearchFilter, StatusFilter, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> OpenAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(actorId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    public void AdoptNewDraft(ActorDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Id == Guid.Empty)
        {
            definition.Id = Guid.NewGuid();
        }

        Current = Clone(definition);
        CurrentId = null;
        CurrentRevision = 0;
        CurrentStatus = ContentPublishStatus.Draft;
        PublishedRevision = null;
        IsDirty = true;
    }

    public ActorDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun héros ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Name = Current.Name + " (copie)";
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public async Task<SaveActorResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveActorResult.ValidationFailed("Aucun héros ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveActorResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveActorResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveActorRequest
                    {
                        ActorId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveActorResult.Success success)
            {
                CurrentId = success.ActorId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.ActorId;
                if (intent == SaveContentIntent.Publish)
                {
                    CurrentStatus = ContentPublishStatus.Published;
                    PublishedRevision = success.PublishedRevision;
                }
                else
                {
                    CurrentStatus = ContentPublishStatus.Draft;
                }

                IsDirty = false;
                await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public async Task<DeleteActorResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteActorResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteActorResult.Success)
        {
            Current = null;
            CurrentId = null;
            CurrentRevision = 0;
            CurrentStatus = ContentPublishStatus.Draft;
            PublishedRevision = null;
            IsDirty = false;
            await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void ApplyStored(StoredActor stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.ActorId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    internal static ActorDefinition Clone(ActorDefinition source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Description = source.Description,
        ClassId = source.ClassId,
        FaceLogicalPath = source.FaceLogicalPath,
        Body = source.Body,
        Hair = source.Hair,
        Tunic = source.Tunic,
        StartingWeaponItemId = source.StartingWeaponItemId,
        StartingArmorItemId = source.StartingArmorItemId,
        BaseHp = source.BaseHp,
        BaseMp = source.BaseMp,
        Str = source.Str,
        Agi = source.Agi,
        Vit = source.Vit,
        Int = source.Int,
        Dex = source.Dex,
        Luck = source.Luck,
    };
}
