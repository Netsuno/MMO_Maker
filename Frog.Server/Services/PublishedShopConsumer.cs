using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>Charge uniquement les définitions de boutiques publiées.</summary>
public sealed class PublishedShopConsumer
{
    private readonly IPublishedShopCatalog _catalog;
    private readonly ILogger<PublishedShopConsumer> _logger;

    public PublishedShopConsumer(
        IPublishedShopCatalog catalog,
        ILogger<PublishedShopConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShopDefinition>> LoadPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published shop definitions loaded count={Count}", list.Count);
        return list;
    }
}
