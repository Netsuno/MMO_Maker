using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>
/// État éditeur hors UI : catalogue + carte courante via <see cref="IMapRepository"/>.
/// </summary>
public sealed class MapWorkspaceSession
{
    private readonly IMapRepository _repository;

    public MapWorkspaceSession(IMapRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public IReadOnlyList<MapCatalogEntry> Catalog { get; private set; } = Array.Empty<MapCatalogEntry>();

    public Map? CurrentMap { get; private set; }

    public Guid? CurrentMapId { get; private set; }

    public long CurrentRevision { get; private set; }

    public MapPublishStatus CurrentStatus { get; private set; } = MapPublishStatus.Draft;

    /// <summary>
    /// Si le catalogue est vide, enregistre la carte démo puis l’ouvre.
    /// Sinon rafraîchit le catalogue et ouvre la première entrée (ou <paramref name="preferredMapId"/>).
    /// </summary>
    public async Task InitializeAsync(Guid? preferredMapId = null, CancellationToken cancellationToken = default)
    {
        await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);

        if (Catalog.Count == 0)
        {
            var demo = DemoMapFactory.CreateStarter();
            var saved = await _repository.SaveAsync(
                    new SaveMapRequest
                    {
                        MapId = DemoMapFactory.DefaultMapId,
                        Map = demo,
                        ExpectedRevision = 0,
                        Status = MapPublishStatus.Draft,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (saved is not SaveMapResult.Success)
            {
                throw new InvalidOperationException("Impossible d’enregistrer la carte démo initiale.");
            }

            await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
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

        CurrentMap = stored.Map;
        CurrentMapId = stored.MapId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        return true;
    }

    /// <summary>Remplace la carte courante par une nouvelle carte locale (non encore persistée).</summary>
    public void AdoptLocalDraft(Map map, Guid? mapId = null, long revision = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        CurrentMap = map;
        CurrentMapId = mapId;
        CurrentRevision = revision;
        CurrentStatus = MapPublishStatus.Draft;
    }
}
