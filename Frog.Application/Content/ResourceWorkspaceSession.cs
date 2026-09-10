using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur des ressources : catalogue + brouillon courant.</summary>
public sealed class ResourceWorkspaceSession
{
    private readonly IResourceRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public ResourceWorkspaceSession(IResourceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public IReadOnlyList<ResourceCatalogEntry> Catalog { get; private set; } =
        Array.Empty<ResourceCatalogEntry>();

    public ResourceDefinition? Current { get; private set; }

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

    public async Task<bool> OpenAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        if (resourceId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(resourceId, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        Current = Clone(stored.Definition);
        CurrentId = stored.ResourceId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
        return true;
    }

    public void AdoptNewDraft(ResourceDefinition definition)
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

    public ResourceDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucune ressource ouverte.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Name += " (copie)";
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public async Task<SaveResourceResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveResourceResult.ValidationFailed("Aucune ressource ouverte.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveResourceResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveResourceResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository.SaveAsync(
                    new SaveResourceRequest
                    {
                        ResourceId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveResourceResult.Success success)
            {
                CurrentId = success.ResourceId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.ResourceId;
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

    public async Task<DeleteResourceResult> DeleteCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteResourceResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteResourceResult.Success)
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

    internal static ResourceDefinition Clone(ResourceDefinition source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Description = source.Description,
        SpriteLogicalPath = source.SpriteLogicalPath,
        RespawnSeconds = source.RespawnSeconds,
        ToolItemId = source.ToolItemId,
        YieldItemId = source.YieldItemId,
        YieldQuantity = source.YieldQuantity,
    };
}
