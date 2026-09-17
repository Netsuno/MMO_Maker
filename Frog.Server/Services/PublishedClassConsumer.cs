using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>Charge uniquement les définitions de classes publiées.</summary>
public sealed class PublishedClassConsumer
{
    private readonly IPublishedClassCatalog _catalog;
    private readonly ILogger<PublishedClassConsumer> _logger;

    public PublishedClassConsumer(
        IPublishedClassCatalog catalog,
        ILogger<PublishedClassConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClassDefinition>> LoadPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published class definitions loaded count={Count}", list.Count);
        return list;
    }
}
