using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Application.Assets;

/// <summary>
/// Vérifie qu’une carte publiée / playtest référence uniquement des tilesets dont le PNG
/// est matérialisable côté client (catalogue <c>pngBase64</c> et/ou sidecars disque).
/// </summary>
public static class PublishedTilesetClientCoverage
{
    public static IReadOnlyList<int> MissingTilesetIds(
        Map map,
        PublishedCatalogWire? catalog,
        string? clientBaseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        var used = MapTilesetPackage.CollectUsedTilesetIds(map);
        if (used.Count == 0)
        {
            return Array.Empty<int>();
        }

        var available = new HashSet<int>();
        CollectFromCatalog(catalog, available);

        if (!string.IsNullOrWhiteSpace(clientBaseDirectory))
        {
            CollectFromSidecarLayout(clientBaseDirectory, used, available);
        }

        return used.Where(id => !available.Contains(id)).ToArray();
    }

    public static bool TryDecodeCatalogPng(PublishedTilesetWireEntry entry, out int paletteId, out byte[] png)
    {
        paletteId = 0;
        png = [];
        if (entry is null || entry.PaletteId <= 0 || string.IsNullOrWhiteSpace(entry.PngBase64))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(entry.PngBase64);
        }
        catch
        {
            return false;
        }

        if (bytes.Length == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.Sha256Hex) && entry.Sha256Hex.Length == 64)
        {
            var actual = TilesetDefinition.ComputeSha256Hex(bytes);
            if (!actual.Equals(entry.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        paletteId = entry.PaletteId;
        png = bytes;
        return true;
    }

    private static void CollectFromCatalog(PublishedCatalogWire? catalog, HashSet<int> available)
    {
        if (catalog is null)
        {
            return;
        }

        foreach (var entry in catalog.Tilesets)
        {
            if (TryDecodeCatalogPng(entry, out var paletteId, out _))
            {
                available.Add(paletteId);
            }
        }
    }

    private static void CollectFromSidecarLayout(
        string clientBaseDirectory,
        IReadOnlyList<int> used,
        HashSet<int> available)
    {
        var tilesetsDir = Path.Combine(clientBaseDirectory, "Tilesets");
        foreach (var id in used)
        {
            if (available.Contains(id))
            {
                continue;
            }

            var path = Path.Combine(tilesetsDir, MapTilesetPackage.FileNameFor(id));
            if (File.Exists(path))
            {
                try
                {
                    if (new FileInfo(path).Length > 0)
                    {
                        available.Add(id);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
