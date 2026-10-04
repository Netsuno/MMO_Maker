using Frog.Core.Models;

namespace Frog.Application.Prefabs;

/// <summary>
/// Relie les objets de carte publiés au catalogue de placement (palette « Objets »).
/// Les props intégrés restent ; un objet publié s’ajoute, ou remplace le même identifiant.
/// </summary>
public static class MapObjectPlacementCatalog
{
    public static PrefabCatalog Merge(PrefabCatalog baseCatalog, IEnumerable<MapObjectDefinition>? published)
    {
        ArgumentNullException.ThrowIfNull(baseCatalog);
        var merged = new PrefabCatalog
        {
            CatalogVersion = baseCatalog.CatalogVersion == 0
                ? BuiltInPrefabCatalog.CatalogVersion
                : baseCatalog.CatalogVersion,
        };
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var prefab in baseCatalog.Prefabs)
        {
            if (prefab is null || string.IsNullOrWhiteSpace(prefab.Id))
            {
                continue;
            }

            if (indexById.ContainsKey(prefab.Id))
            {
                continue;
            }

            indexById[prefab.Id] = merged.Prefabs.Count;
            merged.Prefabs.Add(ClonePrefab(prefab));
        }

        if (published is null)
        {
            return merged;
        }

        foreach (var mapObject in published)
        {
            if (mapObject is null || !mapObject.Validate(out _))
            {
                continue;
            }

            var prefab = ToPrefab(mapObject);
            if (indexById.TryGetValue(prefab.Id, out var index))
            {
                merged.Prefabs[index] = prefab;
            }
            else
            {
                indexById[prefab.Id] = merged.Prefabs.Count;
                merged.Prefabs.Add(prefab);
            }
        }

        return merged;
    }

    public static PrefabDefinition ToPrefab(MapObjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var file = source.SpriteFileName;
        return new PrefabDefinition
        {
            Id = source.PlacementId.Trim(),
            DisplayName = source.Name.Trim(),
            FootprintWidthTiles = source.FootprintWidthTiles,
            FootprintHeightTiles = source.FootprintHeightTiles,
            WidthPixels = source.WidthPixels,
            HeightPixels = source.HeightPixels,
            Variants =
            {
                new PrefabFacingVariant
                {
                    Facing = PrefabFacing.South,
                    SpriteFileName = file,
                    FootprintWidthTiles = source.FootprintWidthTiles,
                    FootprintHeightTiles = source.FootprintHeightTiles,
                    WidthPixels = source.WidthPixels,
                    HeightPixels = source.HeightPixels,
                },
            },
        };
    }

    /// <summary>Écrit les PNG publiés sous <c>Prefabs/</c> pour l’aperçu de la palette.</summary>
    public static int MaterializeSprites(IEnumerable<MapObjectDefinition>? published, string? directory)
    {
        if (published is null || string.IsNullOrWhiteSpace(directory))
        {
            return 0;
        }

        Directory.CreateDirectory(directory);
        var written = 0;
        foreach (var mapObject in published)
        {
            if (mapObject?.PngBytes is not { Length: > 0 } png)
            {
                continue;
            }

            var name = mapObject.SpriteFileName;
            if (string.IsNullOrWhiteSpace(name)
                || name.Contains("..", StringComparison.Ordinal)
                || name.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                continue;
            }

            File.WriteAllBytes(Path.Combine(directory, name), png);
            written++;
        }

        return written;
    }

    private static PrefabDefinition ClonePrefab(PrefabDefinition src)
    {
        var copy = new PrefabDefinition
        {
            Id = src.Id,
            DisplayName = src.DisplayName,
            FootprintWidthTiles = src.FootprintWidthTiles,
            FootprintHeightTiles = src.FootprintHeightTiles,
            WidthPixels = src.WidthPixels,
            HeightPixels = src.HeightPixels,
        };
        if (src.Variants is null)
        {
            return copy;
        }

        foreach (var variant in src.Variants)
        {
            if (variant is null)
            {
                continue;
            }

            copy.Variants.Add(new PrefabFacingVariant
            {
                Facing = variant.Facing,
                SpriteFileName = variant.SpriteFileName,
                FootprintWidthTiles = variant.FootprintWidthTiles,
                FootprintHeightTiles = variant.FootprintHeightTiles,
                WidthPixels = variant.WidthPixels,
                HeightPixels = variant.HeightPixels,
            });
        }

        return copy;
    }
}
