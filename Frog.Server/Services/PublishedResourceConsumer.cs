using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>Charge uniquement les définitions de ressources publiées.</summary>
public sealed class PublishedResourceConsumer
{
    private readonly IPublishedResourceCatalog _catalog;
    private readonly ILogger<PublishedResourceConsumer> _logger;

    public PublishedResourceConsumer(
        IPublishedResourceCatalog catalog,
        ILogger<PublishedResourceConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResourceDefinition>> LoadPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published resource definitions loaded count={Count}", list.Count);
        return list;
    }
}

/// <summary>Charge uniquement les placements de ressources publiés, filtrables par carte.</summary>
public sealed class PublishedResourceSpawnConsumer
{
    private readonly IPublishedResourceSpawnCatalog _catalog;
    private readonly ILogger<PublishedResourceSpawnConsumer> _logger;

    public PublishedResourceSpawnConsumer(
        IPublishedResourceSpawnCatalog catalog,
        ILogger<PublishedResourceSpawnConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResourceSpawnDefinition>> LoadPublishedAsync(
        Guid? mapId = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(mapId, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Published resource spawns loaded count={Count} mapId={MapId}",
            list.Count,
            mapId);
        return list;
    }
}
