using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>
/// Consommation minimale Phase 6 : charge uniquement les tilesets <b>publiés</b> via le port Application.
/// Aucun accès DbContext ici — le catalogue est injecté.
/// </summary>
public sealed class PublishedTilesetConsumer
{
    private readonly IPublishedTilesetCatalog _catalog;
    private readonly ILogger<PublishedTilesetConsumer> _logger;

    public PublishedTilesetConsumer(IPublishedTilesetCatalog catalog, ILogger<PublishedTilesetConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TilesetDefinition>> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published tilesets loaded count={Count}", list.Count);
        return list;
    }
}
