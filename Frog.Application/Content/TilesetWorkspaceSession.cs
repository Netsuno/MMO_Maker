using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur tilesets (hors UI) : catalogue + brouillon courant.</summary>
public sealed class TilesetWorkspaceSession
{
    private readonly ITilesetRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public TilesetWorkspaceSession(ITilesetRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public IReadOnlyList<TilesetCatalogEntry> Catalog { get; private set; } = Array.Empty<TilesetCatalogEntry>();

    public TilesetDefinition? Current { get; private set; }

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

    public async Task<bool> OpenAsync(Guid tilesetId, CancellationToken cancellationToken = default)
    {
        if (tilesetId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(tilesetId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    public void AdoptNewDraft(TilesetDefinition definition)
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

    public TilesetDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun tileset ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Name = Current.Name + " (copie)";
        copy.LogicalPath = DeriveCopyPath(Current.LogicalPath);
        copy.EditorPaletteId = null;
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public async Task<SaveTilesetResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveTilesetResult.ValidationFailed("Aucun tileset ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveTilesetResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveTilesetResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveTilesetRequest
                    {
                        TilesetId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveTilesetResult.Success success)
            {
                CurrentId = success.TilesetId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.TilesetId;
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

    public async Task<DeleteTilesetResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteTilesetResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteTilesetResult.Success)
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

    private void ApplyStored(StoredTileset stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.TilesetId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    private static TilesetDefinition Clone(TilesetDefinition src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        LogicalPath = src.LogicalPath,
        TileSizePixels = src.TileSizePixels,
        WidthPixels = src.WidthPixels,
        HeightPixels = src.HeightPixels,
        Sha256Hex = src.Sha256Hex,
        EditorPaletteId = src.EditorPaletteId,
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
