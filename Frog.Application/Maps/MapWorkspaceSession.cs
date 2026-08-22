using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>
/// État éditeur hors UI : catalogue + carte courante via <see cref="IMapRepository"/>.
/// </summary>
public sealed class MapWorkspaceSession
{
    private readonly IMapRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public MapWorkspaceSession(IMapRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public MapRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public bool IsSaveInProgress { get; private set; }

    public IReadOnlyList<MapCatalogEntry> Catalog { get; private set; } = Array.Empty<MapCatalogEntry>();

    public Map? CurrentMap { get; private set; }

    public Guid? CurrentMapId { get; private set; }

    public long CurrentRevision { get; private set; }

    public MapPublishStatus CurrentStatus { get; private set; } = MapPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    /// <summary>Modifications en mémoire non encore persistées.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Si le catalogue est vide, enregistre la carte démo (PostgreSQL / test mémoire) ou l’ouvre localement (démo).
    /// Sinon rafraîchit le catalogue et ouvre la première entrée (ou <paramref name="preferredMapId"/>).
    /// </summary>
    public async Task InitializeAsync(Guid? preferredMapId = null, CancellationToken cancellationToken = default)
    {
        await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);

        if (Catalog.Count == 0)
        {
            var demo = DemoMapFactory.CreateStarter();
            if (Capabilities.AllowsSave)
            {
                var saved = await _repository.SaveAsync(
                        new SaveMapRequest
                        {
                            MapId = null,
                            Map = demo,
                            ExpectedRevision = 0,
                            Intent = SaveMapIntent.SaveDraft,
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                if (saved is not SaveMapResult.Success success)
                {
                    throw new InvalidOperationException("Impossible d’enregistrer la carte démo initiale.");
                }

                await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
                var demoOpened = await OpenMapAsync(success.MapId, cancellationToken).ConfigureAwait(false);
                if (!demoOpened)
                {
                    throw new InvalidOperationException("Impossible d’ouvrir la carte démo enregistrée.");
                }

                return;
            }
            else
            {
                AdoptLocalDraft(demo, DemoMapFactory.DefaultMapId, revision: 0, markDirty: false);
                return;
            }
        }

        var targetId = preferredMapId
                       ?? Catalog.FirstOrDefault(e => e.MapId == DemoMapFactory.DefaultMapId)?.MapId
                       ?? Catalog[0].MapId;

        var opened = await OpenMapAsync(targetId, cancellationToken).ConfigureAwait(false);
        if (!opened)
        {
            throw new InvalidOperationException("Impossible d’ouvrir la carte initiale du workspace.");
        }
    }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        Catalog = await _repository.ListSummariesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> OpenMapAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        if (mapId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(mapId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    /// <summary>Remplace la carte courante par une nouvelle carte locale (non encore persistée).</summary>
    public void AdoptLocalDraft(Map map, Guid? mapId = null, long revision = 0, bool markDirty = true)
    {
        ArgumentNullException.ThrowIfNull(map);
        CurrentMap = map;
        CurrentMapId = mapId;
        CurrentRevision = revision;
        CurrentStatus = MapPublishStatus.Draft;
        PublishedRevision = null;
        IsDirty = markDirty;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void ClearDirty()
    {
        IsDirty = false;
    }

    /// <summary>Persiste la carte courante (brouillon ou publication).</summary>
    public async Task<SaveMapResult> SaveCurrentAsync(
        SaveMapIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (CurrentMap is null)
        {
            return new SaveMapResult.ValidationFailed("Aucune carte ouverte.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveMapResult.NotDurable(
                "Cette session n’est pas persistante. Configurez PostgreSQL pour enregistrer durablement.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveMapResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
        }

        IsSaveInProgress = true;
        try
        {
            var result = await _repository.SaveAsync(
                    new SaveMapRequest
                    {
                        MapId = CurrentMapId,
                        Map = CurrentMap,
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveMapResult.Success success)
            {
                CurrentMapId = success.MapId;
                CurrentRevision = success.NewRevision;
                PublishedRevision = success.PublishedRevision;
                CurrentStatus = intent == SaveMapIntent.Publish ? MapPublishStatus.Published : MapPublishStatus.Draft;
                IsDirty = false;
                await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            IsSaveInProgress = false;
            _saveGate.Release();
        }
    }

    /// <summary>Obsolète — utiliser <see cref="SaveCurrentAsync(SaveMapIntent, CancellationToken)"/>.</summary>
    public Task<SaveMapResult> SaveCurrentAsync(MapPublishStatus status, CancellationToken cancellationToken = default)
        => SaveCurrentAsync(
            status == MapPublishStatus.Published ? SaveMapIntent.Publish : SaveMapIntent.SaveDraft,
            cancellationToken);

    /// <summary>Recharge la carte courante depuis le dépôt (résolution conflit).</summary>
    public async Task<bool> ReloadCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentMapId is not Guid mapId)
        {
            return false;
        }

        return await OpenMapAsync(mapId, cancellationToken).ConfigureAwait(false);
    }

    private void ApplyStored(StoredMap stored)
    {
        CurrentMap = stored.Map;
        CurrentMapId = stored.MapId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }
}
