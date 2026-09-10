using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Application.Events;

/// <summary>Adaptateur catalogue publié → source de planification Core (J4-SERVER / J4-PG).</summary>
public sealed class PublishedCommonEventSource : IMapEventCommonEventSource
{
    private readonly IPublishedCommonEventCatalog _catalog;

    public PublishedCommonEventSource(IPublishedCommonEventCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public Task<CommonEventDefinition?> TryGetByIdAsync(
        Guid commonEventId,
        CancellationToken cancellationToken = default) =>
        _catalog.TryGetPublishedByIdAsync(commonEventId, cancellationToken);

    public Task<CommonEventDefinition?> TryGetByAliasAsync(
        int editorAliasId,
        CancellationToken cancellationToken = default) =>
        _catalog.TryGetPublishedByAliasAsync(editorAliasId, cancellationToken);
}
