using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>Charge uniquement les définitions d’objets publiées via le port Application.</summary>
public sealed class PublishedItemConsumer
{
    private readonly IPublishedItemCatalog _catalog;
    private readonly ILogger<PublishedItemConsumer> _logger;

    public PublishedItemConsumer(IPublishedItemCatalog catalog, ILogger<PublishedItemConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ItemDefinition>> LoadPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published item definitions loaded count={Count}", list.Count);
        return list;
    }
}
