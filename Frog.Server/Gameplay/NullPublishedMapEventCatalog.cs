using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

internal sealed class NullPublishedMapEventCatalog : IPublishedMapEventCatalog
{
    public static NullPublishedMapEventCatalog Instance { get; } = new();

    public Task<IReadOnlyList<MapEventDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MapEventDefinition>>(Array.Empty<MapEventDefinition>());

    public Task<MapEventDefinition?> TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult<MapEventDefinition?>(null);

    public Task<MapEventDefinition?> TryGetPublishedByAliasAsync(
        int editorAliasId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<MapEventDefinition?>(null);
}
