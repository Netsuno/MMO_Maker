using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Application.Prefabs;

/// <summary>
/// Écrit <c>Prefabs/</c> + <c>Maps/{nom}.prefabs.json</c> depuis <see cref="PublishedCatalogWire"/>.
/// </summary>
public static class PublishedPrefabCatalogMaterializer
{
    public static int Materialize(PublishedCatalogWire? catalog, params string[] directories)
    {
        if (catalog is null || (catalog.Prefabs.Count == 0 && catalog.PrefabMaps.Count == 0))
        {
            return 0;
        }

        var prefabCatalog = PublishedPrefabClientCoverage.ToPrefabCatalog(catalog.Prefabs);
        if (prefabCatalog.Prefabs.Count == 0)
        {
            prefabCatalog = BuiltInPrefabCatalog.Create();
        }

        var sprites = new List<PrefabSpriteFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in catalog.Prefabs)
        {
            foreach (var variant in entry.Variants)
            {
                if (!PublishedPrefabClientCoverage.TryDecodeVariantPng(variant, out var fileName, out var png)
                    || !seen.Add(fileName))
                {
                    continue;
                }

                sprites.Add(new PrefabSpriteFile(fileName, png));
            }
        }

        var targets = directories
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targets.Length == 0)
        {
            return sprites.Count;
        }

        var written = 0;
        foreach (var dir in targets)
        {
            var names = catalog.PrefabMaps
                .Select(m => string.IsNullOrWhiteSpace(m.MapName) ? "world" : m.MapName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length == 0)
            {
                names = ["world"];
            }

            // Catalogue + PNG une fois ; placements par carte ensuite.
            MapPrefabPackage.WriteSidecars(dir, names, prefabCatalog, Array.Empty<PrefabPlacement>(), sprites);
            foreach (var mapEntry in catalog.PrefabMaps)
            {
                var stem = MapPrefabPackage.SanitizeFileStem(
                    string.IsNullOrWhiteSpace(mapEntry.MapName) ? "world" : mapEntry.MapName);
                var mapsDir = Path.Combine(dir, "Maps");
                Directory.CreateDirectory(mapsDir);
                var document = new PrefabPlacementDocument
                {
                    DocumentVersion = 1,
                    Placements = PublishedPrefabClientCoverage.ToPlacements(mapEntry.Placements).ToList(),
                };
                File.WriteAllBytes(
                    Path.Combine(mapsDir, stem + MapPrefabPackage.PlacementSidecarSuffix),
                    PrefabPlacementDocumentJson.Serialize(document));
            }

            written = Math.Max(written, sprites.Count + catalog.PrefabMaps.Count);
        }

        return written;
    }
}
