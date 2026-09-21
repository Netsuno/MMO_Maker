using Frog.Core.Models;

namespace Frog.Application.Assets;

/// <summary>Lit le PNG embarqué sur <see cref="TilesetDefinition.PngBytes"/> (snapshot publié).</summary>
public sealed class EmbeddedPublishedTilesetImageSource : IPublishedTilesetImageSource
{
    public static readonly EmbeddedPublishedTilesetImageSource Instance = new();

    public bool TryReadPng(TilesetDefinition definition, out byte[] bytes)
    {
        bytes = [];
        if (definition?.PngBytes is not { Length: > 0 } png)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(definition.Sha256Hex) && definition.Sha256Hex.Length == 64)
        {
            var actual = TilesetDefinition.ComputeSha256Hex(png);
            if (!actual.Equals(definition.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        bytes = png;
        return true;
    }
}

/// <summary>Essaie plusieurs sources (embarqué puis filesystem).</summary>
public sealed class CompositePublishedTilesetImageSource : IPublishedTilesetImageSource
{
    private readonly IPublishedTilesetImageSource[] _sources;

    public CompositePublishedTilesetImageSource(params IPublishedTilesetImageSource[] sources)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public bool TryReadPng(TilesetDefinition definition, out byte[] bytes)
    {
        foreach (var source in _sources)
        {
            if (source.TryReadPng(definition, out bytes) && bytes.Length > 0)
            {
                return true;
            }
        }

        bytes = [];
        return false;
    }
}
