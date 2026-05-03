#nullable enable
using System.Drawing;
using System.IO;
using System.Text;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Client.Assets;

/// <summary>
/// Charge les bitmaps tileset pour le client : manifeste <c>{nomCarte}.tilesets.json</c>, <c>Tilesets/manifest.json</c>,
/// ou fichiers <c>Tilesets/{id}.png</c> à la racine d’exécution.
/// </summary>
public static class ClientTilesetLoader
{
    /// <summary>Bitmaps par id tileset ; l’appelant doit <see cref="Bitmap.Dispose"/> chaque valeur quand la carte change.</summary>
    public static Dictionary<int, Bitmap> LoadForMap(Map map, string appBaseDirectory)
    {
        var wanted = CollectTilesetIds(map);
        var result = new Dictionary<int, Bitmap>();
        if (wanted.Count == 0)
        {
            return result;
        }

        var mapsDir = Path.Combine(appBaseDirectory, "Maps");
        var tilesetsDir = Path.Combine(appBaseDirectory, "Tilesets");
        Directory.CreateDirectory(tilesetsDir);
        Directory.CreateDirectory(mapsDir);

        var safeName = SanitizeFileStem(string.IsNullOrWhiteSpace(map.Name) ? "world" : map.Name);
        var manifestCandidates = new[]
        {
            Path.Combine(mapsDir, safeName + ".tilesets.json"),
            Path.Combine(tilesetsDir, "manifest.json"),
        };

        foreach (var manifestPath in manifestCandidates)
        {
            var man = TilesetManifestJson.TryDeserializeFromFile(manifestPath);
            if (man is null)
            {
                continue;
            }

            var manifestDir = Path.GetDirectoryName(manifestPath) ?? appBaseDirectory;
            foreach (var e in man.Entries)
            {
                if (e.Id <= 0 || string.IsNullOrWhiteSpace(e.FileName) || !wanted.Contains(e.Id) || result.ContainsKey(e.Id))
                {
                    continue;
                }

                var path = Path.Combine(manifestDir, e.FileName);
                TryAddBitmap(result, e.Id, path);
            }
        }

        foreach (var id in wanted)
        {
            if (result.ContainsKey(id))
            {
                continue;
            }

            var fallback = Path.Combine(tilesetsDir, $"{id}.png");
            TryAddBitmap(result, id, fallback);
        }

        return result;
    }

    private static void TryAddBitmap(Dictionary<int, Bitmap> result, int id, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var tmp = new Bitmap(path);
            result[id] = new Bitmap(tmp);
        }
        catch
        {
            // ignore fichier illisible
        }
    }

    private static HashSet<int> CollectTilesetIds(Map map)
    {
        var s = new HashSet<int>();
        foreach (var layer in map.Layers)
        {
            foreach (var t in layer.Tiles)
            {
                if (t.TilesetId > 0)
                {
                    s.Add(t.TilesetId);
                }
            }
        }

        return s;
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
