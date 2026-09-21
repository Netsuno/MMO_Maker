using Frog.Core.Models;

namespace Frog.Application.Assets;

/// <summary>Lit le PNG publié depuis la racine projet (LogicalPath + SHA optionnel).</summary>
public interface IPublishedTilesetImageSource
{
    bool TryReadPng(TilesetDefinition definition, out byte[] bytes);
}

public sealed class EmptyPublishedTilesetImageSource : IPublishedTilesetImageSource
{
    public static readonly EmptyPublishedTilesetImageSource Instance = new();

    public bool TryReadPng(TilesetDefinition definition, out byte[] bytes)
    {
        bytes = [];
        return false;
    }
}

public sealed class ProjectAssetTilesetImageSource : IPublishedTilesetImageSource
{
    private readonly string _assetRoot;

    public ProjectAssetTilesetImageSource(string assetRoot)
    {
        _assetRoot = assetRoot;
    }

    public bool TryReadPng(TilesetDefinition definition, out byte[] bytes)
    {
        bytes = [];
        if (definition is null)
        {
            return false;
        }

        if (EmbeddedPublishedTilesetImageSource.Instance.TryReadPng(definition, out bytes))
        {
            return true;
        }

        var resolved = ProjectAssetPathResolver.TryResolve(_assetRoot, definition.LogicalPath);
        if (resolved.Status != ProjectAssetPathResolver.ResolveStatus.Success
            || string.IsNullOrWhiteSpace(resolved.AbsolutePath))
        {
            return false;
        }

        try
        {
            bytes = File.ReadAllBytes(resolved.AbsolutePath);
        }
        catch
        {
            bytes = [];
            return false;
        }

        if (bytes.Length == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(definition.Sha256Hex)
            && definition.Sha256Hex.Length == 64)
        {
            var actual = TilesetDefinition.ComputeSha256Hex(bytes);
            if (!actual.Equals(definition.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                bytes = [];
                return false;
            }
        }

        return true;
    }
}
