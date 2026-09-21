#nullable enable
using System.Drawing;
using System.IO;
using Frog.Application.Prefabs;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Client.Assets;

/// <summary>
/// Charge le catalogue + sidecar <c>{nomCarte}.prefabs.json</c> et les PNG sous <c>Prefabs/</c>
/// (même layout que <see cref="ClientTilesetLoader"/> : exe + cwd).
/// </summary>
public static class ClientPrefabLoader
{
    public sealed record LoadResult(
        PrefabCatalog Catalog,
        IReadOnlyList<PrefabPlacement> Placements,
        Dictionary<string, Bitmap> Bitmaps);

    public static LoadResult LoadForMap(
        Map map,
        string appBaseDirectory,
        PublishedCatalogWire? publishedCatalog = null,
        Guid mapId = default,
        int runtimeMapId = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        PrefabCatalog? catalog = null;
        List<PrefabPlacement>? placements = null;
        var bitmaps = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        if (PublishedPrefabClientCoverage.TryMatchPrefabMap(
                publishedCatalog,
                map.Name,
                out var matched,
                mapId,
                runtimeMapId))
        {
            placements = PrefabPlacementService.ClonePlacements(
                PublishedPrefabClientCoverage.ToPlacements(matched.Placements));
            if (MapPrefabPackage.TryParseMapId(matched.MapId, out var matchedId))
            {
                mapId = matchedId;
            }
        }

        foreach (var root in ClientTilesetLoader.ResolveSearchDirectories(appBaseDirectory))
        {
            var prefabsDir = Path.Combine(root, MapPrefabPackage.FolderName);
            var mapsDir = Path.Combine(root, "Maps");
            Directory.CreateDirectory(prefabsDir);
            Directory.CreateDirectory(mapsDir);

            catalog ??= PrefabCatalogJson.TryDeserializeFromFile(Path.Combine(prefabsDir, MapPrefabPackage.CatalogFileName));

            if (placements is null)
            {
                var sidecar = MapPrefabPackage.TryReadPlacementSidecar(mapsDir, map.Name, mapId);
                if (sidecar is not null)
                {
                    placements = PrefabPlacementService.ClonePlacements(sidecar.Placements);
                }
            }

            var sourceCatalog = catalog ?? BuiltInPrefabCatalog.Create();
            foreach (var prefab in sourceCatalog.Prefabs)
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
        }

        return new LoadResult(
            catalog ?? BuiltInPrefabCatalog.Create(),
            placements ?? new List<PrefabPlacement>(),
            bitmaps);
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
}
