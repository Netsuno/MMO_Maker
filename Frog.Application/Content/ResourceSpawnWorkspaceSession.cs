using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur des placements de ressources.</summary>
public sealed class ResourceSpawnWorkspaceSession
{
    private readonly IResourceSpawnRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public ResourceSpawnWorkspaceSession(IResourceSpawnRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public IReadOnlyList<ResourceSpawnCatalogEntry> Catalog { get; private set; } =
        Array.Empty<ResourceSpawnCatalogEntry>();

    public ResourceSpawnDefinition? Current { get; private set; }

    public Guid? CurrentId { get; private set; }

    public long CurrentRevision { get; private set; }

    public ContentPublishStatus CurrentStatus { get; private set; } = ContentPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public Guid? MapFilter { get; set; }

    public Guid? ResourceFilter { get; set; }

    public ContentPublishStatus? StatusFilter { get; set; }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        Catalog = await _repository
            .ListSummariesAsync(MapFilter, ResourceFilter, StatusFilter, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> OpenAsync(Guid spawnId, CancellationToken cancellationToken = default)
    {
        if (spawnId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(spawnId, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        Current = Clone(stored.Definition);
        CurrentId = stored.SpawnId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
        return true;
    }

    public void AdoptNewDraft(ResourceSpawnDefinition definition)
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

    public ResourceSpawnDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun spawn de ressource ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public async Task<SaveResourceSpawnResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                "Aucun spawn de ressource ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveResourceSpawnResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository.SaveAsync(
                    new SaveResourceSpawnRequest
                    {
                        SpawnId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveResourceSpawnResult.Success success)
            {
                CurrentId = success.SpawnId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.SpawnId;
                CurrentStatus = intent == SaveContentIntent.Publish
                    ? ContentPublishStatus.Published
                    : ContentPublishStatus.Draft;
                if (intent == SaveContentIntent.Publish)
                {
                    PublishedRevision = success.PublishedRevision;
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

    public async Task<DeleteResourceSpawnResult> DeleteCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteResourceSpawnResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteResourceSpawnResult.Success)
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

    internal static ResourceSpawnDefinition Clone(ResourceSpawnDefinition source) => new()
    {
        Id = source.Id,
        MapId = source.MapId,
        ResourceId = source.ResourceId,
        TileX = source.TileX,
        TileY = source.TileY,
    };
}
