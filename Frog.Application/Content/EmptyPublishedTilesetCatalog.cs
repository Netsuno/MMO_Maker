using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class EmptyPublishedTilesetCatalog : IPublishedTilesetCatalog
{
    public static readonly EmptyPublishedTilesetCatalog Instance = new();

    public Task<IReadOnlyList<TilesetDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TilesetDefinition>>(Array.Empty<TilesetDefinition>());
}
