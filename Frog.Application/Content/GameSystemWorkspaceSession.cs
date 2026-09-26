using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur Système : catalogue + brouillon courant.</summary>
public sealed class GameSystemWorkspaceSession
{
    private readonly IGameSystemRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public GameSystemWorkspaceSession(IGameSystemRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public IReadOnlyList<GameSystemCatalogEntry> Catalog { get; private set; } = Array.Empty<GameSystemCatalogEntry>();

    public GameSystemDefinition? Current { get; private set; }

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

    public async Task<bool> OpenAsync(Guid systemId, CancellationToken cancellationToken = default)
    {
        if (systemId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(systemId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    public void AdoptNewDraft(GameSystemDefinition definition)
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

    public GameSystemDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun système ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Name = Current.Name + " (copie)";
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public async Task<SaveGameSystemResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveGameSystemResult.ValidationFailed("Aucun système ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveGameSystemResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveGameSystemResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveGameSystemRequest
                    {
                        SystemId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveGameSystemResult.Success success)
            {
                CurrentId = success.SystemId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.SystemId;
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

    public async Task<DeleteGameSystemResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteGameSystemResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteGameSystemResult.Success)
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

    private void ApplyStored(StoredGameSystem stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.SystemId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    internal static GameSystemDefinition Clone(GameSystemDefinition source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Title = source.Title,
        CurrencyUnit = source.CurrencyUnit,
        Description = source.Description,
        Switches = source.Switches
            .Select(flag => new NamedWorldFlag { Key = flag.Key, Label = flag.Label })
            .ToList(),
        Variables = source.Variables
            .Select(flag => new NamedWorldFlag { Key = flag.Key, Label = flag.Label })
            .ToList(),
        StartingPartyActorIds = source.StartingPartyActorIds.ToList(),
    };
}
