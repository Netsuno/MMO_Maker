using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur : catalogue des tilesets composés et brouillon courant.</summary>
public sealed class ComposedTilesetWorkspaceSession
{
    private readonly IComposedTilesetRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public ComposedTilesetWorkspaceSession(IComposedTilesetRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public IReadOnlyList<ComposedTilesetCatalogEntry> Catalog { get; private set; } = Array.Empty<ComposedTilesetCatalogEntry>();

    public ComposedTilesetDefinition? Current { get; private set; }

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

    public void AdoptNewDraft(ComposedTilesetDefinition definition)
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

    public ComposedTilesetDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun tileset ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Name = Current.Name + " (copie)";
        copy.LogicalPath = DeriveCopyPath(Current.LogicalPath);
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public async Task<SaveComposedTilesetResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return new SaveComposedTilesetResult.ValidationFailed("Aucun tileset ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveComposedTilesetResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveComposedTilesetResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveComposedTilesetRequest
                    {
                        TilesetId = CurrentId,
                        Definition = Clone(Current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveComposedTilesetResult.Success success)
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

    public async Task<DeleteComposedTilesetResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteComposedTilesetResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteComposedTilesetResult.Success)
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

    private void ApplyStored(StoredComposedTileset stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.TilesetId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    private static ComposedTilesetDefinition Clone(ComposedTilesetDefinition src)
    {
        var copy = new ComposedTilesetDefinition
        {
            Id = src.Id,
            Name = src.Name,
            LogicalPath = src.LogicalPath,
        };
        foreach (var tile in src.Tiles)
        {
            if (tile is null)
            {
                continue;
            }

            copy.Tiles.Add(new ComposedTileRef
            {
                TileAssetId = tile.TileAssetId,
                DisplayName = tile.DisplayName,
                NormalizedRgba = tile.NormalizedRgba is { Length: > 0 } bytes ? bytes.ToArray() : Array.Empty<byte>(),
            });
        }

        return copy;
    }

    private static string DeriveCopyPath(string path)
    {
        var slash = path.LastIndexOf('/');
        var stem = slash >= 0 ? path[(slash + 1)..] : path;
        var folder = slash >= 0 ? path[..(slash + 1)] : string.Empty;
        var dot = stem.LastIndexOf('.');
        var name = dot > 0 ? stem[..dot] : stem;
        var ext = dot > 0 ? stem[dot..] : ".tileset";
        return folder + name + "-copie" + ext;
    }
}
