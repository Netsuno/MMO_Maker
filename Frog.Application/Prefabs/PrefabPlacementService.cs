using System.Text.RegularExpressions;
using Frog.Core.Constants;
using Frog.Core.Models;

namespace Frog.Application.Prefabs;

/// <summary>Validation + placement d’instances prefab (hors blob <c>.fmap</c>).</summary>
public static class PrefabPlacementService
{
    private static readonly Regex IdPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    public static bool IsValidId(string? id)
        => !string.IsNullOrWhiteSpace(id) && IdPattern.IsMatch(id.Trim());

    public static bool TryGetDefinition(PrefabCatalog catalog, string prefabId, out PrefabDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        definition = null!;
        if (string.IsNullOrWhiteSpace(prefabId) || catalog.Prefabs is null)
        {
            return false;
        }

        foreach (var item in catalog.Prefabs)
        {
            if (item is not null && string.Equals(item.Id, prefabId, StringComparison.Ordinal))
            {
                definition = item;
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveVariant(
        PrefabDefinition definition,
        PrefabFacing facing,
        out PrefabFacingVariant variant)
    {
        ArgumentNullException.ThrowIfNull(definition);
        variant = null!;
        if (definition.Variants is { Count: > 0 })
        {
            foreach (var item in definition.Variants)
            {
                if (item is not null && item.Facing == facing)
                {
                    variant = item;
                    return true;
                }
            }

            if (definition.Variants[0] is { } first)
            {
                variant = first;
                return true;
            }

            return false;
        }

        variant = new PrefabFacingVariant { Facing = facing };
        return true;
    }

    public static bool TryResolveFootprint(
        PrefabDefinition definition,
        PrefabFacingVariant? variant,
        out int widthTiles,
        out int heightTiles)
    {
        ArgumentNullException.ThrowIfNull(definition);
        widthTiles = 0;
        heightTiles = 0;

        if (variant is not null && variant.FootprintWidthTiles > 0 && variant.FootprintHeightTiles > 0)
        {
            widthTiles = variant.FootprintWidthTiles;
            heightTiles = variant.FootprintHeightTiles;
            return true;
        }

        if (definition.FootprintWidthTiles > 0 && definition.FootprintHeightTiles > 0)
        {
            widthTiles = definition.FootprintWidthTiles;
            heightTiles = definition.FootprintHeightTiles;
            return true;
        }

        var wPx = variant is { WidthPixels: > 0 } ? variant.WidthPixels : definition.WidthPixels;
        var hPx = variant is { HeightPixels: > 0 } ? variant.HeightPixels : definition.HeightPixels;
        var tile = WorldMetrics.DefaultTileSizePixels;
        if (wPx > 0 && hPx > 0 && tile > 0)
        {
            widthTiles = Math.Max(1, (wPx + tile - 1) / tile);
            heightTiles = Math.Max(1, (hPx + tile - 1) / tile);
            return true;
        }

        return false;
    }

    public static bool TryValidateDefinition(PrefabDefinition definition, out string? error)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!IsValidId(definition.Id))
        {
            error = "Identifiant prefab invalide (a-z, 0-9, tirets).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            error = "Nom affiché requis.";
            return false;
        }

        if (definition.Variants is null || definition.Variants.Count == 0)
        {
            error = "Au moins une variante est requise.";
            return false;
        }

        foreach (var variant in definition.Variants)
        {
            if (variant is null || string.IsNullOrWhiteSpace(variant.SpriteFileName))
            {
                error = "Variante sans fichier sprite.";
                return false;
            }

            var name = Path.GetFileName(variant.SpriteFileName.Trim());
            if (!string.Equals(name, variant.SpriteFileName.Trim(), StringComparison.Ordinal)
                || name.Contains("..", StringComparison.Ordinal))
            {
                error = "Nom de sprite invalide (fichier seul, pas de traversée).";
                return false;
            }

            if (!TryResolveFootprint(definition, variant, out _, out _))
            {
                error = "Empreinte tuiles ou pixels manquante.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public static bool TryValidateCatalog(PrefabCatalog catalog, out string? error)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.CatalogVersion < 1)
        {
            error = "Version de catalogue invalide.";
            return false;
        }

        if (catalog.Prefabs is null || catalog.Prefabs.Count == 0)
        {
            error = "Catalogue vide.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prefab in catalog.Prefabs)
        {
            if (prefab is null)
            {
                error = "Définition prefab invalide.";
                return false;
            }

            if (!TryValidateDefinition(prefab, out error))
            {
                return false;
            }

            if (!seen.Add(prefab.Id))
            {
                error = "Identifiant prefab en double : " + prefab.Id;
                return false;
            }
        }

        error = null;
        return true;
    }

    public static bool FitsOnMap(int tileX, int tileY, int widthTiles, int heightTiles, int mapWidth, int mapHeight)
        => tileX >= 0
           && tileY >= 0
           && widthTiles > 0
           && heightTiles > 0
           && mapWidth > 0
           && mapHeight > 0
           && tileX + widthTiles <= mapWidth
           && tileY + heightTiles <= mapHeight;

    public static bool Occupies(PrefabPlacement placement, int widthTiles, int heightTiles, int tileX, int tileY)
        => tileX >= placement.TileX
           && tileY >= placement.TileY
           && tileX < placement.TileX + widthTiles
           && tileY < placement.TileY + heightTiles;

    public static bool Overlaps(
        int ax,
        int ay,
        int aw,
        int ah,
        int bx,
        int by,
        int bw,
        int bh)
        => ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

    public static bool TryPlace(
        IList<PrefabPlacement> placements,
        PrefabCatalog catalog,
        string prefabId,
        PrefabFacing facing,
        int tileX,
        int tileY,
        int mapWidth,
        int mapHeight,
        out PrefabPlacement? placed,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(placements);
        placed = null;
        if (!TryGetDefinition(catalog, prefabId, out var definition))
        {
            error = "Prefab inconnu.";
            return false;
        }

        if (!TryValidateDefinition(definition, out error))
        {
            return false;
        }

        if (!TryResolveVariant(definition, facing, out var variant)
            || !TryResolveFootprint(definition, variant, out var w, out var h))
        {
            error = "Variante ou empreinte introuvable.";
            return false;
        }

        if (!FitsOnMap(tileX, tileY, w, h, mapWidth, mapHeight))
        {
            error = "Empreinte hors carte.";
            return false;
        }

        RemoveOverlapping(placements, catalog, tileX, tileY, w, h);
        placed = new PrefabPlacement
        {
            PrefabId = definition.Id,
            Facing = variant.Facing == facing ? facing : variant.Facing,
            TileX = tileX,
            TileY = tileY,
        };
        placements.Add(placed);
        error = null;
        return true;
    }

    public static int EraseAt(
        IList<PrefabPlacement> placements,
        PrefabCatalog catalog,
        int tileX,
        int tileY)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(catalog);
        var removed = 0;
        for (var i = placements.Count - 1; i >= 0; i--)
        {
            var item = placements[i];
            if (item is null)
            {
                continue;
            }

            if (!TryGetDefinition(catalog, item.PrefabId, out var definition)
                || !TryResolveVariant(definition, item.Facing, out var variant)
                || !TryResolveFootprint(definition, variant, out var w, out var h))
            {
                continue;
            }

            if (Occupies(item, w, h, tileX, tileY))
            {
                placements.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    public static PrefabFacing NextFacing(PrefabFacing facing)
        => facing switch
        {
            PrefabFacing.South => PrefabFacing.West,
            PrefabFacing.West => PrefabFacing.East,
            PrefabFacing.East => PrefabFacing.North,
            _ => PrefabFacing.South,
        };

    public static PrefabFacing PreviousFacing(PrefabFacing facing)
        => facing switch
        {
            PrefabFacing.South => PrefabFacing.North,
            PrefabFacing.West => PrefabFacing.South,
            PrefabFacing.East => PrefabFacing.West,
            _ => PrefabFacing.East,
        };

    public static PrefabPlacement? TryFindAt(
        IReadOnlyList<PrefabPlacement> placements,
        PrefabCatalog catalog,
        int tileX,
        int tileY)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(catalog);
        for (var i = placements.Count - 1; i >= 0; i--)
        {
            var item = placements[i];
            if (item is null)
            {
                continue;
            }

            if (!TryGetDefinition(catalog, item.PrefabId, out var definition)
                || !TryResolveVariant(definition, item.Facing, out var variant)
                || !TryResolveFootprint(definition, variant, out var w, out var h))
            {
                continue;
            }

            if (Occupies(item, w, h, tileX, tileY))
            {
                return item;
            }
        }

        return null;
    }

    public static bool TryMove(
        IList<PrefabPlacement> placements,
        PrefabCatalog catalog,
        PrefabPlacement target,
        int tileX,
        int tileY,
        int mapWidth,
        int mapHeight,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(target);
        error = null;
        if (!TryGetDefinition(catalog, target.PrefabId, out var definition)
            || !TryResolveVariant(definition, target.Facing, out var variant)
            || !TryResolveFootprint(definition, variant, out var w, out var h))
        {
            error = "Prefab introuvable.";
            return false;
        }

        if (!FitsOnMap(tileX, tileY, w, h, mapWidth, mapHeight))
        {
            error = "Empreinte hors carte.";
            return false;
        }

        var index = -1;
        for (var i = 0; i < placements.Count; i++)
        {
            if (ReferenceEquals(placements[i], target))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            error = "Instance introuvable.";
            return false;
        }

        for (var i = placements.Count - 1; i >= 0; i--)
        {
            if (i == index)
            {
                continue;
            }

            var item = placements[i];
            if (item is null)
            {
                continue;
            }

            if (!TryGetDefinition(catalog, item.PrefabId, out var otherDef)
                || !TryResolveVariant(otherDef, item.Facing, out var otherVariant)
                || !TryResolveFootprint(otherDef, otherVariant, out var ow, out var oh))
            {
                continue;
            }

            if (Overlaps(tileX, tileY, w, h, item.TileX, item.TileY, ow, oh))
            {
                placements.RemoveAt(i);
                if (i < index)
                {
                    index--;
                }
            }
        }

        placements[index].TileX = tileX;
        placements[index].TileY = tileY;
        return true;
    }

    public static HashSet<string> CollectRequiredSpriteFileNames(
        PrefabCatalog catalog,
        IEnumerable<PrefabPlacement>? placements)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        if (placements is not null)
        {
            foreach (var placement in placements)
            {
                if (!string.IsNullOrWhiteSpace(placement?.PrefabId))
                {
                    usedIds.Add(placement.PrefabId);
                }
            }
        }

        foreach (var prefab in catalog.Prefabs)
        {
            if (prefab?.Variants is null)
            {
                continue;
            }

            if (usedIds.Count > 0 && !usedIds.Contains(prefab.Id))
            {
                continue;
            }

            foreach (var variant in prefab.Variants)
            {
                var name = Path.GetFileName(variant.SpriteFileName?.Trim() ?? string.Empty);
                if (!string.IsNullOrEmpty(name) && !name.Contains("..", StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    public static PrefabFacing ParseFacing(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PrefabFacing.South;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "west" => PrefabFacing.West,
            "east" => PrefabFacing.East,
            "north" => PrefabFacing.North,
            _ => PrefabFacing.South,
        };
    }

    public static string FacingToWire(PrefabFacing facing)
        => facing switch
        {
            PrefabFacing.West => "west",
            PrefabFacing.East => "east",
            PrefabFacing.North => "north",
            _ => "south",
        };

    /// <summary>
    /// Copie la dernière instance posée, décalée de son empreinte (droite, puis bas, gauche, haut).
    /// Ne remplace pas les voisins : une case libre est requise.
    /// </summary>
    public static bool TryDuplicateLast(
        IList<PrefabPlacement> placements,
        PrefabCatalog catalog,
        int mapWidth,
        int mapHeight,
        out PrefabPlacement? placed,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(catalog);
        placed = null;
        if (placements.Count == 0)
        {
            error = "Aucun objet posé à dupliquer.";
            return false;
        }

        var source = placements[placements.Count - 1];
        if (source is null
            || !TryGetDefinition(catalog, source.PrefabId, out var definition)
            || !TryResolveVariant(definition, source.Facing, out var variant)
            || !TryResolveFootprint(definition, variant, out var widthTiles, out var heightTiles))
        {
            error = "Prefab introuvable.";
            return false;
        }

        var steps = new (int Dx, int Dy)[]
        {
            (widthTiles, 0),
            (0, heightTiles),
            (-widthTiles, 0),
            (0, -heightTiles),
            (widthTiles, heightTiles),
            (-widthTiles, heightTiles),
            (widthTiles, -heightTiles),
            (-widthTiles, -heightTiles),
        };

        foreach (var (dx, dy) in steps)
        {
            var tileX = source.TileX + dx;
            var tileY = source.TileY + dy;
            if (!CanOccupy(placements, catalog, tileX, tileY, widthTiles, heightTiles, mapWidth, mapHeight))
            {
                continue;
            }

            if (TryPlace(
                    placements,
                    catalog,
                    source.PrefabId,
                    source.Facing,
                    tileX,
                    tileY,
                    mapWidth,
                    mapHeight,
                    out placed,
                    out error))
            {
                return true;
            }
        }

        placed = null;
        error = "Pas de place libre à côté du dernier objet.";
        return false;
    }

    public static List<PrefabPlacement> ClonePlacements(IEnumerable<PrefabPlacement>? source)
    {
        var list = new List<PrefabPlacement>();
        if (source is null)
        {
            return list;
        }

        foreach (var item in source)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.PrefabId))
            {
                continue;
            }

            list.Add(new PrefabPlacement
            {
                PrefabId = item.PrefabId,
                Facing = item.Facing,
                TileX = item.TileX,
                TileY = item.TileY,
            });
        }

        return list;
    }

    private static bool CanOccupy(
        IList<PrefabPlacement> placements,
        PrefabCatalog catalog,
        int tileX,
        int tileY,
        int widthTiles,
        int heightTiles,
        int mapWidth,
        int mapHeight)
    {
        if (!FitsOnMap(tileX, tileY, widthTiles, heightTiles, mapWidth, mapHeight))
        {
            return false;
        }

        foreach (var item in placements)
        {
            if (item is null)
            {
                continue;
            }

            if (!TryGetDefinition(catalog, item.PrefabId, out var definition)
                || !TryResolveVariant(definition, item.Facing, out var variant)
                || !TryResolveFootprint(definition, variant, out var otherWidth, out var otherHeight))
            {
                continue;
            }

            if (Overlaps(tileX, tileY, widthTiles, heightTiles, item.TileX, item.TileY, otherWidth, otherHeight))
            {
                return false;
            }
        }

        return true;
    }

    private static void RemoveOverlapping(
        IList<PrefabPlacement> placements,
        PrefabCatalog catalog,
        int tileX,
        int tileY,
        int widthTiles,
        int heightTiles)
    {
        for (var i = placements.Count - 1; i >= 0; i--)
        {
            var item = placements[i];
            if (item is null)
            {
                continue;
            }

            if (!TryGetDefinition(catalog, item.PrefabId, out var definition)
                || !TryResolveVariant(definition, item.Facing, out var variant)
                || !TryResolveFootprint(definition, variant, out var w, out var h))
            {
                continue;
            }

            if (Overlaps(tileX, tileY, widthTiles, heightTiles, item.TileX, item.TileY, w, h))
            {
                placements.RemoveAt(i);
            }
        }
    }
}
