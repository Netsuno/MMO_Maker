using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>Charge uniquement les définitions de sorts et compétences publiées.</summary>
public sealed class PublishedSpellConsumer
{
    private readonly IPublishedSpellCatalog _catalog;
    private readonly ILogger<PublishedSpellConsumer> _logger;

    public PublishedSpellConsumer(
        IPublishedSpellCatalog catalog,
        ILogger<PublishedSpellConsumer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SpellDefinition>> LoadPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published spell definitions loaded count={Count}", list.Count);
        return list;
    }
}
