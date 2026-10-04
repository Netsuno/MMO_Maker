using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur des objets de carte : catalogue + brouillon courant.</summary>
public sealed class MapObjectWorkspaceSession
{
    private readonly IMapObjectRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public MapObjectWorkspaceSession(IMapObjectRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public IReadOnlyList<MapObjectCatalogEntry> Catalog { get; private set; } = Array.Empty<MapObjectCatalogEntry>();

    public MapObjectDefinition? Current { get; private set; }

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

    public async Task<bool> OpenAsync(Guid mapObjectId, CancellationToken cancellationToken = default)
    {
        if (mapObjectId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(mapObjectId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    public void AdoptNewDraft(MapObjectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Id == Guid.Empty)
        {
            definition.Id = Guid.NewGuid();
        }

        if (string.IsNullOrWhiteSpace(definition.PlacementId))
        {
            definition.PlacementId = MapObjectDefinition.CreatePlacementId(definition.Id);
        }

        Current = Clone(definition);
        CurrentId = null;
        CurrentRevision = 0;
        CurrentStatus = ContentPublishStatus.Draft;
        PublishedRevision = null;
        IsDirty = true;
    }

    public MapObjectDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun objet de carte ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Name = Current.Name + " (copie)";
        copy.LogicalPath = DeriveCopyPath(Current.LogicalPath);
        copy.PlacementId = MapObjectDefinition.CreatePlacementId(copy.Id);
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public async Task<SaveMapObjectResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveMapObjectResult.ValidationFailed("Aucun objet de carte ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveMapObjectResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveMapObjectResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveMapObjectRequest
                    {
                        MapObjectId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveMapObjectResult.Success success)
            {
                CurrentId = success.MapObjectId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.MapObjectId;
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

    public async Task<DeleteMapObjectResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteMapObjectResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteMapObjectResult.Success)
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

    private void ApplyStored(StoredMapObject stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.MapObjectId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    private static MapObjectDefinition Clone(MapObjectDefinition src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        LogicalPath = src.LogicalPath,
        PlacementId = src.PlacementId,
        FootprintWidthTiles = src.FootprintWidthTiles,
        FootprintHeightTiles = src.FootprintHeightTiles,
        WidthPixels = src.WidthPixels,
        HeightPixels = src.HeightPixels,
        Sha256Hex = src.Sha256Hex,
        PngBytes = src.PngBytes is { Length: > 0 } png ? png.ToArray() : src.PngBytes,
    };

    private static string DeriveCopyPath(string path)
    {
        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
        var file = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var name = string.IsNullOrEmpty(dir)
            ? $"{file}_copy{ext}"
            : $"{dir}/{file}_copy{ext}";
        return name.Replace('\\', '/');
    }
}
