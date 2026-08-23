using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur NPC/monstres : catalogue + brouillon courant.</summary>
public sealed class NpcWorkspaceSession
{
    private readonly INpcRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public NpcWorkspaceSession(INpcRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public IReadOnlyList<NpcCatalogEntry> Catalog { get; private set; } = Array.Empty<NpcCatalogEntry>();

    public NpcDefinition? Current { get; private set; }

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

    public async Task<bool> OpenAsync(Guid npcId, CancellationToken cancellationToken = default)
    {
        if (npcId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(npcId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    public void AdoptNewDraft(NpcDefinition definition)
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

    public NpcDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun NPC ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Name = Current.Name + " (copie)";
        copy.EditorAliasId = null;
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public async Task<SaveNpcResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveNpcResult.ValidationFailed("Aucun NPC ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveNpcResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveNpcResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveNpcRequest
                    {
                        NpcId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveNpcResult.Success success)
            {
                CurrentId = success.NpcId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.NpcId;
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

    public async Task<DeleteNpcResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteNpcResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteNpcResult.Success)
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

    private void ApplyStored(StoredNpc stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.NpcId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
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
}
