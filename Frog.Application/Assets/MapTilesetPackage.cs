using System.Text;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Application.Assets;

public sealed record MapTilesetFile(int Id, byte[] PngBytes);

/// <summary>
/// Sidecars déjà consommés par <c>ClientTilesetLoader</c> :
/// <c>Tilesets/{id}.png</c>, <c>Tilesets/manifest.json</c>, <c>Maps/{nom}.tilesets.json</c>.
/// </summary>
public static class MapTilesetPackage
{
    public static IReadOnlyList<int> CollectUsedTilesetIds(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var ids = new SortedSet<int>();
        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.TilesetId > 0)
                {
                    ids.Add(tile.TilesetId);
                }
            }
        }

        return ids.ToArray();
    }

    public static TilesetManifest BuildManifest(IEnumerable<MapTilesetFile> files)
    {
        var manifest = new TilesetManifest { ManifestVersion = 1 };
        foreach (var file in files.Where(f => f.Id > 0 && f.PngBytes.Length > 0).OrderBy(f => f.Id))
        {
            manifest.Entries.Add(new TilesetManifestEntry
            {
                Id = file.Id,
                FileName = FileNameFor(file.Id),
            });
        }

        return manifest;
    }

    public static void WriteSidecars(
        string appBaseDirectory,
        IEnumerable<string> mapNames,
        IReadOnlyList<MapTilesetFile> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appBaseDirectory);
        ArgumentNullException.ThrowIfNull(mapNames);
        ArgumentNullException.ThrowIfNull(files);

        var tilesetsDir = Path.Combine(appBaseDirectory, "Tilesets");
        var mapsDir = Path.Combine(appBaseDirectory, "Maps");
        Directory.CreateDirectory(tilesetsDir);
        Directory.CreateDirectory(mapsDir);

        foreach (var file in files.Where(f => f.Id > 0 && f.PngBytes.Length > 0))
        {
            File.WriteAllBytes(Path.Combine(tilesetsDir, FileNameFor(file.Id)), file.PngBytes);
        }

        var manifest = BuildManifest(files);
        var manifestBytes = TilesetManifestJson.Serialize(manifest);
        File.WriteAllBytes(Path.Combine(tilesetsDir, "manifest.json"), manifestBytes);

        foreach (var name in mapNames)
        {
            var stem = SanitizeFileStem(string.IsNullOrWhiteSpace(name) ? "world" : name);
            File.WriteAllBytes(Path.Combine(mapsDir, stem + ".tilesets.json"), manifestBytes);
        }
    }

    public static string FileNameFor(int tilesetId) => $"{tilesetId}.png";

    public static string SanitizeFileStem(string name)
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
