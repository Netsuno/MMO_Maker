using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Application.Prefabs;

/// <summary>
/// Vérifie qu’une carte publiée / playtest référence uniquement des prefabs
/// dont le PNG (et le placement) est matérialisable côté client.
/// </summary>
public static class PublishedPrefabClientCoverage
{
    public sealed record MissingPrefab(string PrefabId, string Reason);

    public static IReadOnlyList<MissingPrefab> Missing(
        Map map,
        PublishedCatalogWire? catalog,
        string? clientBaseDirectory = null,
        Guid mapId = default,
        int runtimeMapId = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        var placements = CollectPlacements(map, catalog, clientBaseDirectory, mapId, runtimeMapId);
        if (placements.Count == 0)
        {
            return Array.Empty<MissingPrefab>();
        }

        var availableSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PrefabCatalog? prefabCatalog = null;
        CollectFromCatalog(catalog, availableSprites, ref prefabCatalog);

        if (!string.IsNullOrWhiteSpace(clientBaseDirectory))
        {
            CollectFromSidecarLayout(clientBaseDirectory, availableSprites, ref prefabCatalog);
        }

        var missing = new List<MissingPrefab>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in placements)
        {
            if (placement is null || string.IsNullOrWhiteSpace(placement.PrefabId))
            {
                continue;
            }

            if (prefabCatalog is null
                || !PrefabPlacementService.TryGetDefinition(prefabCatalog, placement.PrefabId, out var definition))
            {
                if (seen.Add("id:" + placement.PrefabId))
                {
                    missing.Add(new MissingPrefab(placement.PrefabId, "définition absente du catalogue"));
                }

                continue;
            }

            if (!PrefabPlacementService.TryResolveVariant(definition, placement.Facing, out var variant))
            {
                if (seen.Add("variant:" + placement.PrefabId + ":" + placement.Facing))
                {
                    missing.Add(new MissingPrefab(placement.PrefabId, "variante introuvable"));
                }

                continue;
            }

            var sprite = Path.GetFileName(variant.SpriteFileName?.Trim() ?? string.Empty);
            if (string.IsNullOrEmpty(sprite) || !availableSprites.Contains(sprite))
            {
                if (seen.Add("sprite:" + sprite))
                {
                    missing.Add(new MissingPrefab(
                        placement.PrefabId,
                        "image manquante (" + (string.IsNullOrEmpty(sprite) ? "?" : sprite) + ")"));
                }
            }
        }

        return missing;
    }

    public static bool TryDecodeVariantPng(PublishedPrefabVariantWire? variant, out string fileName, out byte[] png)
    {
        fileName = string.Empty;
        png = [];
        if (variant is null || string.IsNullOrWhiteSpace(variant.SpriteFileName) || string.IsNullOrWhiteSpace(variant.PngBase64))
        {
            return false;
        }

        var name = Path.GetFileName(variant.SpriteFileName.Trim());
        if (string.IsNullOrEmpty(name) || name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(variant.PngBase64);
        }
        catch
        {
            return false;
        }

        if (bytes.Length == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(variant.Sha256Hex) && variant.Sha256Hex.Length == 64)
        {
            var actual = TilesetDefinition.ComputeSha256Hex(bytes);
            if (!actual.Equals(variant.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        fileName = name;
        png = bytes;
        return true;
    }

    public static PrefabCatalog ToPrefabCatalog(IEnumerable<PublishedPrefabWireEntry>? entries)
    {
        var catalog = new PrefabCatalog { CatalogVersion = BuiltInPrefabCatalog.CatalogVersion };
        if (entries is null)
        {
            return catalog;
        }

        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            var definition = new PrefabDefinition
            {
                Id = entry.Id,
                DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Id : entry.DisplayName,
                FootprintWidthTiles = entry.FootprintWidthTiles,
                FootprintHeightTiles = entry.FootprintHeightTiles,
                WidthPixels = entry.WidthPixels,
                HeightPixels = entry.HeightPixels,
            };
            foreach (var variant in entry.Variants)
            {
                if (variant is null || string.IsNullOrWhiteSpace(variant.SpriteFileName))
                {
                    continue;
                }

                definition.Variants.Add(new PrefabFacingVariant
                {
                    Facing = PrefabPlacementService.ParseFacing(variant.Facing),
                    SpriteFileName = Path.GetFileName(variant.SpriteFileName.Trim()),
                    FootprintWidthTiles = variant.FootprintWidthTiles,
                    FootprintHeightTiles = variant.FootprintHeightTiles,
                    WidthPixels = variant.WidthPixels,
                    HeightPixels = variant.HeightPixels,
                });
            }

            if (definition.Variants.Count > 0)
            {
                catalog.Prefabs.Add(definition);
            }
        }

        return catalog;
    }

    public static IReadOnlyList<PrefabPlacement> ToPlacements(IEnumerable<PublishedPrefabPlacementWire>? entries)
    {
        var list = new List<PrefabPlacement>();
        if (entries is null)
        {
            return list;
        }

        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.PrefabId))
            {
                continue;
            }

            list.Add(new PrefabPlacement
            {
                PrefabId = entry.PrefabId,
                Facing = PrefabPlacementService.ParseFacing(entry.Facing),
                TileX = entry.TileX,
                TileY = entry.TileY,
            });
        }

        return list;
    }

    public static IReadOnlyList<PublishedPrefabWireEntry> ToWirePrefabs(
        PrefabCatalog catalog,
        IReadOnlyList<PrefabSpriteFile> sprites)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sprites);
        var byName = new Dictionary<string, PrefabSpriteFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var sprite in sprites)
        {
            var name = Path.GetFileName(sprite.FileName?.Trim() ?? string.Empty);
            if (!string.IsNullOrEmpty(name) && sprite.PngBytes.Length > 0)
            {
                byName[name] = sprite;
            }
        }

        var list = new List<PublishedPrefabWireEntry>();
        foreach (var prefab in catalog.Prefabs)
        {
            if (prefab is null || string.IsNullOrWhiteSpace(prefab.Id))
            {
                continue;
            }

            var variants = new List<PublishedPrefabVariantWire>();
            foreach (var variant in prefab.Variants)
            {
                if (variant is null)
                {
                    continue;
                }

                var name = Path.GetFileName(variant.SpriteFileName?.Trim() ?? string.Empty);
                string? png = null;
                var sha = string.Empty;
                if (!string.IsNullOrEmpty(name) && byName.TryGetValue(name, out var file))
                {
                    png = Convert.ToBase64String(file.PngBytes);
                    sha = TilesetDefinition.ComputeSha256Hex(file.PngBytes);
                }

                variants.Add(new PublishedPrefabVariantWire
                {
                    Facing = PrefabPlacementService.FacingToWire(variant.Facing),
                    SpriteFileName = name,
                    FootprintWidthTiles = variant.FootprintWidthTiles,
                    FootprintHeightTiles = variant.FootprintHeightTiles,
                    WidthPixels = variant.WidthPixels,
                    HeightPixels = variant.HeightPixels,
                    Sha256Hex = sha,
                    PngBase64 = png,
                });
            }

            list.Add(new PublishedPrefabWireEntry
            {
                Id = prefab.Id,
                DisplayName = prefab.DisplayName,
                FootprintWidthTiles = prefab.FootprintWidthTiles,
                FootprintHeightTiles = prefab.FootprintHeightTiles,
                WidthPixels = prefab.WidthPixels,
                HeightPixels = prefab.HeightPixels,
                Variants = variants,
            });
        }

        return list;
    }

    public static PublishedPrefabMapWireEntry ToWireMap(
        Guid mapId,
        string mapName,
        IReadOnlyList<PrefabPlacement> placements,
        int? runtimeMapId = null)
        => new()
        {
            MapId = mapId == Guid.Empty ? string.Empty : mapId.ToString("D"),
            MapName = mapName ?? string.Empty,
            RuntimeMapId = runtimeMapId is > 0 ? runtimeMapId : null,
            Placements = placements.Select(p => new PublishedPrefabPlacementWire
            {
                PrefabId = p.PrefabId,
                Facing = PrefabPlacementService.FacingToWire(p.Facing),
                TileX = p.TileX,
                TileY = p.TileY,
            }).ToArray(),
        };

    public static bool TryMatchPrefabMap(
        PublishedCatalogWire? catalog,
        string? mapName,
        out PublishedPrefabMapWireEntry entry,
        Guid mapId = default,
        int runtimeMapId = 0)
    {
        entry = null!;
        if (catalog is null || catalog.PrefabMaps.Count == 0)
        {
            return false;
        }

        if (runtimeMapId != 0)
        {
            foreach (var item in catalog.PrefabMaps)
            {
                if (item?.RuntimeMapId == runtimeMapId)
                {
                    entry = item;
                    return true;
                }
            }
        }

        if (mapId != Guid.Empty)
        {
            var expected = mapId.ToString("D");
            foreach (var item in catalog.PrefabMaps)
            {
                if (item is not null
                    && MapPrefabPackage.TryParseMapId(item.MapId, out var parsed)
                    && parsed == mapId)
                {
                    entry = item;
                    return true;
                }

                if (item is not null && string.Equals(item.MapId, expected, StringComparison.OrdinalIgnoreCase))
                {
                    entry = item;
                    return true;
                }
            }
        }

        var stem = MapPrefabPackage.SanitizeFileStem(string.IsNullOrWhiteSpace(mapName) ? "world" : mapName);
        PublishedPrefabMapWireEntry? unique = null;
        var matches = 0;
        foreach (var item in catalog.PrefabMaps)
        {
            if (item is null)
            {
                continue;
            }

            var entryStem = MapPrefabPackage.SanitizeFileStem(
                string.IsNullOrWhiteSpace(item.MapName) ? "world" : item.MapName);
            if (!string.Equals(entryStem, stem, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.MapName, mapName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches++;
            unique = item;
        }

        if (matches == 1 && unique is not null)
        {
            entry = unique;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<PrefabPlacement> CollectPlacements(
        Map map,
        PublishedCatalogWire? catalog,
        string? clientBaseDirectory,
        Guid mapId,
        int runtimeMapId)
    {
        if (TryMatchPrefabMap(catalog, map.Name, out var matched, mapId, runtimeMapId))
        {
            return ToPlacements(matched.Placements);
        }

        if (string.IsNullOrWhiteSpace(clientBaseDirectory))
        {
            return Array.Empty<PrefabPlacement>();
        }

        var sidecar = MapPrefabPackage.TryReadPlacementSidecar(
            Path.Combine(clientBaseDirectory, "Maps"),
            map.Name,
            mapId);
        return PrefabPlacementService.ClonePlacements(sidecar?.Placements);
    }

    private static void CollectFromCatalog(
        PublishedCatalogWire? catalog,
        HashSet<string> availableSprites,
        ref PrefabCatalog? prefabCatalog)
    {
        if (catalog is null)
        {
            return;
        }

        var parsed = ToPrefabCatalog(catalog.Prefabs);
        if (parsed.Prefabs.Count > 0)
        {
            prefabCatalog = MergeCatalogs(prefabCatalog, parsed);
        }

        foreach (var entry in catalog.Prefabs)
        {
            foreach (var variant in entry.Variants)
            {
                if (TryDecodeVariantPng(variant, out var fileName, out _))
                {
                    availableSprites.Add(fileName);
                }
            }
        }
    }

    private static void CollectFromSidecarLayout(
        string clientBaseDirectory,
        HashSet<string> availableSprites,
        ref PrefabCatalog? prefabCatalog)
    {
        var prefabsDir = Path.Combine(clientBaseDirectory, MapPrefabPackage.FolderName);
        var diskCatalog = PrefabCatalogJson.TryDeserializeFromFile(Path.Combine(prefabsDir, MapPrefabPackage.CatalogFileName));
        if (diskCatalog is not null)
        {
            prefabCatalog = MergeCatalogs(prefabCatalog, diskCatalog);
        }

        if (!Directory.Exists(prefabsDir))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(prefabsDir, "*.png"))
        {
            try
            {
                if (new FileInfo(path).Length > 0)
                {
                    availableSprites.Add(Path.GetFileName(path));
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    private static PrefabCatalog MergeCatalogs(PrefabCatalog? existing, PrefabCatalog incoming)
    {
        if (existing is null || existing.Prefabs.Count == 0)
        {
            return incoming;
        }

        var seen = new HashSet<string>(existing.Prefabs.Select(p => p.Id), StringComparer.Ordinal);
        foreach (var prefab in incoming.Prefabs)
        {
            if (prefab is not null && seen.Add(prefab.Id))
            {
                existing.Prefabs.Add(prefab);
            }
        }

        return existing;
    }
}
