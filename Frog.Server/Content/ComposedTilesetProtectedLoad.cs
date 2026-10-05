using Frog.Application.Content;
using Frog.Core.Distribution;
using Frog.Server.Config;

using Microsoft.Extensions.Options;

namespace Frog.Server.Content;

/// <summary>
/// Assemble la charge signée des tilesets composés publiés.
/// Même clé Ed25519 que le <c>.frogpack</c>. Pas de catalogue JSON brut.
/// </summary>
public sealed class ComposedTilesetProtectedLoad
{
    private readonly IPublishedComposedTilesetCatalog _catalog;
    private readonly IOptions<TilePackOptions> _options;

    public ComposedTilesetProtectedLoad(
        IPublishedComposedTilesetCatalog catalog,
        IOptions<TilePackOptions> options)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<byte[]?> TryCreateSignedPackAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.TryGetPrivateSeed(out var seed))
        {
            return null;
        }

        var published = await _catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        return ComposedTilesetPackWriter.Write(published, seed);
    }
}
