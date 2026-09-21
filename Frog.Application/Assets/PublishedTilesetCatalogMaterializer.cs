using Frog.Core.Protocol;

namespace Frog.Application.Assets;

/// <summary>
/// Écrit <c>Tilesets/{paletteId}.png</c> depuis <see cref="PublishedCatalogWire"/> (même layout client).
/// </summary>
public static class PublishedTilesetCatalogMaterializer
{
    public static int Materialize(PublishedCatalogWire? catalog, params string[] directories)
        => Materialize(catalog, directories, mapNames: null);

    public static int Materialize(
        PublishedCatalogWire? catalog,
        string[] directories,
        IReadOnlyList<string>? mapNames)
    {
        if (catalog is null || catalog.Tilesets.Count == 0)
        {
            return 0;
        }

        var written = new List<MapTilesetFile>();
        foreach (var entry in catalog.Tilesets)
        {
            if (!PublishedTilesetClientCoverage.TryDecodeCatalogPng(entry, out var paletteId, out var png))
            {
                continue;
            }

            written.Add(new MapTilesetFile(paletteId, png));
        }

        if (written.Count == 0)
        {
            return 0;
        }

        var targets = directories
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var names = mapNames ?? Array.Empty<string>();
        foreach (var dir in targets)
        {
            MapTilesetPackage.WriteSidecars(dir, names, written);
        }

        return written.Count;
    }
}
