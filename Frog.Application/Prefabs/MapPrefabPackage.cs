using System.Text;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Application.Prefabs;

public sealed record PrefabSpriteFile(string FileName, byte[] PngBytes);

/// <summary>
/// Sidecars prefab (même layout que tilesets) :
/// <c>Prefabs/catalog.json</c>, <c>Prefabs/{sprite}.png</c>, <c>Maps/{nom}.prefabs.json</c>.
/// </summary>
public static class MapPrefabPackage
{
    public const string FolderName = "Prefabs";
    public const string CatalogFileName = "catalog.json";
    public const string PlacementSidecarSuffix = ".prefabs.json";

    public static void WriteSidecars(
        string appBaseDirectory,
        IEnumerable<string> mapNames,
        PrefabCatalog catalog,
        IReadOnlyList<PrefabPlacement> placements,
        IReadOnlyList<PrefabSpriteFile>? sprites = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appBaseDirectory);
        ArgumentNullException.ThrowIfNull(mapNames);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(placements);

        var prefabsDir = Path.Combine(appBaseDirectory, FolderName);
        var mapsDir = Path.Combine(appBaseDirectory, "Maps");
        Directory.CreateDirectory(prefabsDir);
        Directory.CreateDirectory(mapsDir);

        File.WriteAllBytes(Path.Combine(prefabsDir, CatalogFileName), PrefabCatalogJson.Serialize(catalog));

        foreach (var sprite in sprites ?? Array.Empty<PrefabSpriteFile>())
        {
            if (string.IsNullOrWhiteSpace(sprite.FileName) || sprite.PngBytes.Length == 0)
            {
                continue;
            }

            var name = Path.GetFileName(sprite.FileName);
            if (string.IsNullOrWhiteSpace(name) || name.Contains("..", StringComparison.Ordinal))
            {
                continue;
            }

            File.WriteAllBytes(Path.Combine(prefabsDir, name), sprite.PngBytes);
        }

        var document = new PrefabPlacementDocument
        {
            DocumentVersion = 1,
            Placements = PrefabPlacementService.ClonePlacements(placements),
        };
        var placementBytes = PrefabPlacementDocumentJson.Serialize(document);

        foreach (var name in mapNames)
        {
            var stem = SanitizeFileStem(string.IsNullOrWhiteSpace(name) ? "world" : name);
            File.WriteAllBytes(Path.Combine(mapsDir, stem + PlacementSidecarSuffix), placementBytes);
        }
    }

    public static void WritePlacementSidecarNextToMap(string mapFilePath, IReadOnlyList<PrefabPlacement> placements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapFilePath);
        ArgumentNullException.ThrowIfNull(placements);

        var dir = Path.GetDirectoryName(mapFilePath);
        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(stem))
        {
            return;
        }

        var document = new PrefabPlacementDocument
        {
            DocumentVersion = 1,
            Placements = PrefabPlacementService.ClonePlacements(placements),
        };
        File.WriteAllBytes(Path.Combine(dir, stem + PlacementSidecarSuffix), PrefabPlacementDocumentJson.Serialize(document));
    }

    public static PrefabPlacementDocument? TryReadPlacementSidecarNextToMap(string mapFilePath)
    {
        var dir = Path.GetDirectoryName(mapFilePath);
        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(stem))
        {
            return null;
        }

        return PrefabPlacementDocumentJson.TryDeserializeFromFile(Path.Combine(dir, stem + PlacementSidecarSuffix));
    }

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
