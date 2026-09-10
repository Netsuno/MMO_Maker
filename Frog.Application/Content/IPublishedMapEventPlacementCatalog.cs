using Frog.Core.Protocol;

namespace Frog.Application.Content;

/// <summary>Placements d'événements publiés par carte runtime (consommation serveur).</summary>
public interface IPublishedMapEventPlacementCatalog
{
    Task<IReadOnlyList<MapEventWireEntry>> GetPlacementsForRuntimeMapAsync(
        int runtimeMapId,
        CancellationToken cancellationToken = default);
}
