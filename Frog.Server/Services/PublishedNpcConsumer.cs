using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>
/// Charge uniquement les définitions NPC/monstres publiées via le port Application.
/// </summary>
public sealed class PublishedNpcConsumer
{
    private readonly IPublishedNpcCatalog _catalog;
    private readonly ILogger<PublishedNpcConsumer> _logger;

    public PublishedNpcConsumer(IPublishedNpcCatalog catalog, ILogger<PublishedNpcConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NpcDefinition>> LoadPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published NPC definitions loaded count={Count}", list.Count);
        return list;
    }
}
