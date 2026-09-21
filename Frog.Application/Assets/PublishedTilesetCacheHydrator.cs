using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Application.Assets;

/// <summary>
/// Reconstruit les PNG session (ids carte) depuis les tilesets publiés (Guid + palette + octets).
/// </summary>
public static class PublishedTilesetCacheHydrator
{
    public static IReadOnlyList<MapTilesetFile> CollectPngFiles(
        Map map,
        IReadOnlyList<TilesetDefinition> published,
        IPublishedTilesetImageSource? images = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(published);

        var used = MapTilesetPackage.CollectUsedTilesetIds(map);
        if (used.Count == 0)
        {
            return Array.Empty<MapTilesetFile>();
        }

        var source = images ?? EmbeddedPublishedTilesetImageSource.Instance;
        var files = new List<MapTilesetFile>();
        var claimed = new HashSet<int>();
        var shaToUsed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in published)
        {
            var palette = TilesetPaletteAlignment.ResolveClientTilesetId(def, used);
            if (palette is not > 0 || !used.Contains(palette.Value) || claimed.Contains(palette.Value))
            {
                continue;
            }

            if (!source.TryReadPng(def, out var bytes) || bytes.Length == 0)
            {
                continue;
            }

            files.Add(new MapTilesetFile(palette.Value, bytes));
            claimed.Add(palette.Value);
            shaToUsed[TilesetDefinition.ComputeSha256Hex(bytes).ToUpperInvariant()] = palette.Value;
        }

        foreach (var def in published)
        {
            var remaining = used.Where(id => !claimed.Contains(id)).ToArray();
            if (remaining.Length == 0)
            {
                break;
            }

            if (!source.TryReadPng(def, out var bytes) || bytes.Length == 0)
            {
                continue;
            }

            var sha = TilesetDefinition.ComputeSha256Hex(bytes).ToUpperInvariant();
            if (shaToUsed.ContainsKey(sha))
            {
                continue;
            }

            var aligned = TilesetPaletteAlignment.ResolveClientTilesetId(def, remaining, shaToUsed);
            if (aligned is not > 0 || claimed.Contains(aligned.Value))
            {
                if (remaining.Length == 1 && !claimed.Contains(remaining[0]))
                {
                    aligned = remaining[0];
                }
                else
                {
                    continue;
                }
            }

            files.Add(new MapTilesetFile(aligned.Value, bytes));
            claimed.Add(aligned.Value);
            shaToUsed[sha] = aligned.Value;
        }

        return files;
    }

    public static async Task<IReadOnlyList<MapTilesetFile>> CollectPngFilesAsync(
        Map map,
        IPublishedTilesetCatalog catalog,
        IPublishedTilesetImageSource? images = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var published = await catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        return CollectPngFiles(map, published, images);
    }
}
