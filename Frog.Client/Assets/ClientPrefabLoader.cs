#nullable enable
using System.Drawing;
using System.IO;
using System.Text;
using Frog.Application.Prefabs;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Client.Assets;

/// <summary>
/// Charge le catalogue + sidecar <c>{nomCarte}.prefabs.json</c> et les PNG sous <c>Prefabs/</c>
/// (même layout que <see cref="ClientTilesetLoader"/>).
/// </summary>
public static class ClientPrefabLoader
{
    public sealed record LoadResult(
        PrefabCatalog Catalog,
        IReadOnlyList<PrefabPlacement> Placements,
        Dictionary<string, Bitmap> Bitmaps);

    public static LoadResult LoadForMap(Map map, string appBaseDirectory)
    {
        ArgumentNullException.ThrowIfNull(map);
        var prefabsDir = Path.Combine(appBaseDirectory, MapPrefabPackage.FolderName);
        var mapsDir = Path.Combine(appBaseDirectory, "Maps");
        Directory.CreateDirectory(prefabsDir);
        Directory.CreateDirectory(mapsDir);

        var catalog = PrefabCatalogJson.TryDeserializeFromFile(Path.Combine(prefabsDir, MapPrefabPackage.CatalogFileName))
                      ?? BuiltInPrefabCatalog.Create();

        var stem = SanitizeFileStem(string.IsNullOrWhiteSpace(map.Name) ? "world" : map.Name);
        var sidecar = PrefabPlacementDocumentJson.TryDeserializeFromFile(
            Path.Combine(mapsDir, stem + MapPrefabPackage.PlacementSidecarSuffix));
        var placements = PrefabPlacementService.ClonePlacements(sidecar?.Placements);

        var bitmaps = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in catalog.Prefabs)
        {
            if (prefab?.Variants is null)
            {
                continue;
            }

            foreach (var variant in prefab.Variants)
            {
                var name = Path.GetFileName(variant.SpriteFileName?.Trim() ?? string.Empty);
                if (string.IsNullOrEmpty(name) || bitmaps.ContainsKey(name))
                {
                    continue;
                }

                TryAddBitmap(bitmaps, name, Path.Combine(prefabsDir, name));
            }
        }

        return new LoadResult(catalog, placements, bitmaps);
    }

    public static void DisposeBitmaps(Dictionary<string, Bitmap>? bitmaps)
    {
        if (bitmaps is null)
        {
            return;
        }

        foreach (var bmp in bitmaps.Values)
        {
            bmp.Dispose();
        }

        bitmaps.Clear();
    }

    private static void TryAddBitmap(Dictionary<string, Bitmap> result, string fileName, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var tmp = new Bitmap(path);
            result[fileName] = new Bitmap(tmp);
        }
        catch
        {
            // ignore fichier illisible
        }
    }

    private static string SanitizeFileStem(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Trim())
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        var stem = sb.ToString();
        return string.IsNullOrEmpty(stem) ? "world" : stem;
    }
}
