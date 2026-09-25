using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>
/// Tampon rectangle ou ellipse. Défaut : rectangle plein.
/// <see cref="Outline"/> ne peint que le bord. <see cref="Ellipse"/> inscrit l’ellipse dans le rectangle.
/// </summary>
public readonly record struct ShapeStampOptions
{
    /// <summary>Contour seulement. Faux = plein.</summary>
    public bool Outline { get; init; }

    /// <summary>Ellipse inscrite dans le rectangle tracé. Faux = rectangle.</summary>
    public bool Ellipse { get; init; }
}

/// <summary>
/// Options du pot de peinture. Défaut : couche active seulement, sans filtre de collision.
/// La diffusion est 4-connexe (haut, bas, gauche, droite), jamais en diagonale.
/// </summary>
public readonly record struct FloodFillOptions
{
    /// <summary>
    /// Écrit la même région sur les autres couches visibles et déverrouillées.
    /// La couche Attributs n’est peinte que si elle est la couche active.
    /// </summary>
    public bool VisibleUnlockedLayers { get; init; }

    /// <summary>
    /// N’englobe pas une case dont la collision ou les attributs diffèrent de la graine
    /// (attributs portés par la tuile, et tuile de la couche Attributs lorsqu’elle est distincte).
    /// </summary>
    public bool RespectAttributes { get; init; }
}

/// <summary>Opérations d’édition carte testables sans UI ni rendu.</summary>
public static class MapEditOperations
{
    public static bool IsLayerEditable(Map map, int layerIndex)
        => layerIndex >= 0 && layerIndex < map.Layers.Count && !map.Layers[layerIndex].Locked;

    /// <summary>Visible et déverrouillée. Le rectangle, l’ellipse et le pot ne peignent pas une couche masquée.</summary>
    public static bool IsLayerPaintable(Map map, int layerIndex)
        => IsLayerEditable(map, layerIndex) && map.Layers[layerIndex].Visible;

    public static void PaintTile(Map map, int layerIndex, int x, int y, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tile);
        if (!IsLayerEditable(map, layerIndex) || !IsInBounds(map, x, y))
        {
            return;
        }

        var layer = map.Layers[layerIndex];
        layer.Tiles.RemoveAll(t => t.X == x && t.Y == y);
        layer.Tiles.Add(CloneTile(tile, x, y));
    }

    public static void EraseTile(Map map, int layerIndex, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!IsLayerEditable(map, layerIndex) || !IsInBounds(map, x, y))
        {
            return;
        }

        map.Layers[layerIndex].Tiles.RemoveAll(t => t.X == x && t.Y == y);
    }

    public static void PaintRectangle(Map map, int layerIndex, int x0, int y0, int x1, int y1, Tile stamp)
        => PaintShape(map, layerIndex, x0, y0, x1, y1, stamp);

    /// <summary>
    /// Cases du rectangle (ou de l’ellipse inscrite), extrémités comprises.
    /// Le contour est le bord de la forme pleine : une case pleine dont un voisin 4-connexe est dehors.
    /// Une case seule est à la fois le plein et le contour.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> EnumerateShape(int x0, int y0, int x1, int y1, ShapeStampOptions options = default)
    {
        var minX = Math.Min(x0, x1);
        var maxX = Math.Max(x0, x1);
        var minY = Math.Min(y0, y1);
        var maxY = Math.Max(y0, y1);
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var filled = new List<(int X, int Y)>(width * height);
        var inside = new HashSet<(int X, int Y)>();
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (options.Ellipse && !ContainsEllipse(x, y, minX, minY, width, height))
                {
                    continue;
                }

                filled.Add((x, y));
                inside.Add((x, y));
            }
        }

        if (!options.Outline)
        {
            return filled;
        }

        var outline = new List<(int X, int Y)>(filled.Count);
        foreach (var (x, y) in filled)
        {
            if (!inside.Contains((x - 1, y))
                || !inside.Contains((x + 1, y))
                || !inside.Contains((x, y - 1))
                || !inside.Contains((x, y + 1)))
            {
                outline.Add((x, y));
            }
        }

        return outline;
    }

    /// <summary>
    /// Peint la forme sur une seule couche. Défaut : rectangle plein.
    /// Couche masquée, verrouillée, ou entièrement hors carte : 0, sans mutation.
    /// <paramref name="beforeMutate"/> est appelé une seule fois, avant toute écriture,
    /// et seulement s’il y a au moins une case dans la carte (un pas d’annulation).
    /// </summary>
    public static int PaintShape(
        Map map,
        int layerIndex,
        int x0,
        int y0,
        int x1,
        int y1,
        Tile stamp,
        ShapeStampOptions options = default,
        Action? beforeMutate = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(stamp);
        if (!IsLayerPaintable(map, layerIndex))
        {
            return 0;
        }

        var writes = new List<(int X, int Y)>();
        foreach (var (x, y) in EnumerateShape(x0, y0, x1, y1, options))
        {
            if (IsInBounds(map, x, y))
            {
                writes.Add((x, y));
            }
        }

        if (writes.Count == 0)
        {
            return 0;
        }

        beforeMutate?.Invoke();
        foreach (var (x, y) in writes)
        {
            PaintTile(map, layerIndex, x, y, stamp);
        }

        return writes.Count;
    }

    /// <summary>
    /// Centre de la case dans l’ellipse inscrite dans le rectangle de cases.
    /// (2·dx + 1 − w)² · h² + (2·dy + 1 − h)² · w² ≤ w² · h².
    /// </summary>
    private static bool ContainsEllipse(int x, int y, int minX, int minY, int width, int height)
    {
        long w = width;
        long h = height;
        long dx = (2L * (x - minX)) + 1 - w;
        long dy = (2L * (y - minY)) + 1 - h;
        var limit = w * w * h * h;
        return (dx * dx * h * h) + (dy * dy * w * w) <= limit;
    }

    /// <summary>Trait d'une tuile de large (Bresenham), extrémités comprises. Hors carte : ignoré.</summary>
    public static void PaintLine(Map map, int layerIndex, int x0, int y0, int x1, int y1, Tile stamp)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(stamp);
        if (!IsLayerEditable(map, layerIndex))
        {
            return;
        }

        foreach (var (x, y) in EnumerateLine(x0, y0, x1, y1))
        {
            PaintTile(map, layerIndex, x, y, stamp);
        }
    }

    /// <summary>
    /// Cases d'un segment en Bresenham entier (largeur 1). Inclut les deux bouts.
    /// Chaque pas avance d'au plus une case en X et en Y.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> EnumerateLine(int x0, int y0, int x1, int y1)
    {
        var points = new List<(int X, int Y)>();
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        var limit = dx + dy;

        while (points.Count <= limit)
        {
            points.Add((x0, y0));
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    /// <summary>Maj : verrouille l'arrivée sur l'axe dominant (horizontal si |dx| &gt;= |dy|).</summary>
    public static (int X, int Y) ConstrainToDominantAxis(int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        return dx >= dy ? (x1, y0) : (x0, y1);
    }

    /// <summary>
    /// Remplissage 4-connexe par file (pas de récursion), borné à la carte.
    /// <paramref name="replacement"/> null efface la région (sélection vide ou gomme).
    /// Retourne le nombre de cases réellement modifiées. Couche active verrouillée,
    /// masquée ou hors carte : 0, sans mutation.
    /// <paramref name="beforeMutate"/> est appelé une seule fois, avant toute écriture,
    /// et seulement s’il y a au moins une case à changer (un pas d’annulation).
    /// </summary>
    public static int FloodFill(
        Map map,
        int layerIndex,
        int sx,
        int sy,
        Tile? replacement,
        FloodFillOptions options = default,
        Action? beforeMutate = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!IsInBounds(map, sx, sy))
        {
            return 0;
        }

        var targets = ResolveFloodTargets(map, layerIndex, options.VisibleUnlockedLayers);
        if (targets.Count == 0)
        {
            return 0;
        }

        var externalAttributes = -1;
        if (options.RespectAttributes && map.Layers[layerIndex].LayerType != LayerType.Attributes)
        {
            externalAttributes = FindAttributesLayerIndex(map);
        }

        var region = CollectFloodRegion(
            map,
            map.Layers[layerIndex],
            sx,
            sy,
            options.RespectAttributes,
            externalAttributes);
        if (region.Count == 0)
        {
            return 0;
        }

        var writes = new List<(int Layer, int X, int Y)>();
        foreach (var target in targets)
        {
            var index = IndexTiles(map.Layers[target]);
            foreach (var (x, y) in region)
            {
                index.TryGetValue((x, y), out var existing);
                if (replacement is null)
                {
                    if (existing is not null)
                    {
                        writes.Add((target, x, y));
                    }
                }
                else if (!SamePlacedTile(existing, replacement))
                {
                    writes.Add((target, x, y));
                }
            }
        }

        if (writes.Count == 0)
        {
            return 0;
        }

        beforeMutate?.Invoke();
        foreach (var (layer, x, y) in writes)
        {
            if (replacement is null)
            {
                EraseTile(map, layer, x, y);
            }
            else
            {
                PaintTile(map, layer, x, y, replacement);
            }
        }

        return writes.Count;
    }

    public static void SetBlockTile(Map map, int layerIndex, int x, int y)
    {
        var tile = new Tile
        {
            X = x,
            Y = y,
            Type = TileType.Block,
            Attributes = { new BlockAttribute() },
        };
        PaintTile(map, layerIndex, x, y, tile);
    }

    public static void SetWarpDestination(Map map, int layerIndex, int x, int y, Guid targetMapId, int targetX, int targetY)
    {
        var tile = new Tile
        {
            X = x,
            Y = y,
            Type = TileType.Warp,
            WarpTargetMapId = targetMapId,
            WarpTargetX = targetX,
            WarpTargetY = targetY,
        };
        PaintTile(map, layerIndex, x, y, tile);
    }

    public static void SetLayerVisibility(Map map, int layerIndex, bool visible)
    {
        if (layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return;
        }

        map.Layers[layerIndex].Visible = visible;
    }

    public static void SetLayerLocked(Map map, int layerIndex, bool locked)
    {
        if (layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return;
        }

        map.Layers[layerIndex].Locked = locked;
    }

    public static void AddLayer(Map map, LayerType type = LayerType.Ground)
        => map.Layers.Add(new Layer { LayerType = type });

    public static void RemoveLayer(Map map, int layerIndex)
    {
        if (layerIndex >= 0 && layerIndex < map.Layers.Count)
        {
            map.Layers.RemoveAt(layerIndex);
        }
    }

    public static void RenameLayer(Map map, int layerIndex, string displayName)
    {
        if (layerIndex >= 0 && layerIndex < map.Layers.Count)
        {
            map.Layers[layerIndex].DisplayName = displayName;
        }
    }

    public static void ChangeLayerType(Map map, int layerIndex, LayerType type)
    {
        if (layerIndex >= 0 && layerIndex < map.Layers.Count)
        {
            map.Layers[layerIndex].LayerType = type;
        }
    }

    public static int CountTilesInRect(Map map, int layerIndex, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (layerIndex < 0 || layerIndex >= map.Layers.Count || width <= 0 || height <= 0)
        {
            return 0;
        }

        var layer = map.Layers[layerIndex];
        var n = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                if (layer.Tiles.Any(t => t.X == x && t.Y == y))
                {
                    n++;
                }
            }
        }

        return n;
    }

    /// <summary>
    /// Vrai s’il existe au moins une tuile effaçable dans le rectangle.
    /// <paramref name="onlyLayerIndex"/> null = toutes les couches déverrouillées.
    /// </summary>
    public static bool HasEditableTilesInRect(
        Map map,
        int left,
        int top,
        int width,
        int height,
        int? onlyLayerIndex)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (onlyLayerIndex is int only)
        {
            return IsLayerEditable(map, only) && CountTilesInRect(map, only, left, top, width, height) > 0;
        }

        for (var i = 0; i < map.Layers.Count; i++)
        {
            if (IsLayerEditable(map, i) && CountTilesInRect(map, i, left, top, width, height) > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Efface le rectangle sur une couche. Couche verrouillée ou hors index : aucun effet.</summary>
    public static void EraseRectangle(Map map, int layerIndex, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!IsLayerEditable(map, layerIndex) || width <= 0 || height <= 0)
        {
            return;
        }

        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                EraseTile(map, layerIndex, x, y);
            }
        }
    }

    /// <summary>
    /// Rotation / miroir in situ d’un rectangle de couche. Le nouveau rectangle est ancré en (left, top).
    /// Ne mute pas si le rectangle ne contient aucune tuile.
    /// </summary>
    public static bool TryTransformLayerRect(
        Map map,
        int layerIndex,
        int left,
        int top,
        int width,
        int height,
        TileSelectionTransformKind kind,
        out int newWidth,
        out int newHeight)
    {
        ArgumentNullException.ThrowIfNull(map);
        newWidth = width;
        newHeight = height;
        if (!IsLayerEditable(map, layerIndex) || width <= 0 || height <= 0)
        {
            return false;
        }

        var layer = map.Layers[layerIndex];
        var captured = new List<Tile>();
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var t = layer.Tiles.FirstOrDefault(tile => tile.X == x && tile.Y == y);
                if (t is null)
                {
                    continue;
                }

                captured.Add(CloneTile(t, x - left, y - top));
            }
        }

        if (captured.Count == 0)
        {
            return false;
        }

        var transformed = TileSelectionTransform.Apply(captured, width, height, kind);
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                layer.Tiles.RemoveAll(t => t.X == x && t.Y == y);
            }
        }

        foreach (var template in transformed.Tiles)
        {
            PaintTile(map, layerIndex, left + template.X, top + template.Y, template);
        }

        newWidth = transformed.Width;
        newHeight = transformed.Height;
        return true;
    }

    /// <summary>
    /// Rotation / miroir in situ du rectangle sur une ou toutes les couches éditables.
    /// Les couches verrouillées ou vides ne bougent pas. Le rectangle reste ancré en (left, top).
    /// </summary>
    public static bool TryTransformMapRect(
        Map map,
        int left,
        int top,
        int width,
        int height,
        TileSelectionTransformKind kind,
        int? onlyLayerIndex,
        out int newWidth,
        out int newHeight)
    {
        ArgumentNullException.ThrowIfNull(map);
        newWidth = width;
        newHeight = height;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (onlyLayerIndex is int only && (only < 0 || only >= map.Layers.Count))
        {
            return false;
        }

        var (nw, nh) = TileSelectionTransform.TransformSize(width, height, kind);
        var any = false;
        var last = onlyLayerIndex ?? map.Layers.Count - 1;
        var first = onlyLayerIndex ?? 0;
        for (var index = first; index <= last; index++)
        {
            if (!IsLayerEditable(map, index) || CountTilesInRect(map, index, left, top, width, height) == 0)
            {
                continue;
            }

            if (TryTransformLayerRect(map, index, left, top, width, height, kind, out _, out _))
            {
                any = true;
            }
        }

        if (!any)
        {
            return false;
        }

        newWidth = nw;
        newHeight = nh;
        return true;
    }

    public static Tile CloneTileAt(Tile source, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(source);
        return CloneTile(source, x, y);
    }

    /// <summary>Visible et déverrouillée. Le pot ne peint jamais une couche masquée ou verrouillée.</summary>
    private static bool IsFloodPaintable(Map map, int layerIndex) => IsLayerPaintable(map, layerIndex);

    private static List<int> ResolveFloodTargets(Map map, int activeLayer, bool visibleUnlockedLayers)
    {
        if (!IsFloodPaintable(map, activeLayer))
        {
            return new List<int>();
        }

        // La couche Attributs n’est remplie que lorsqu’elle est la couche active,
        // même si l’option multi-couches est cochée.
        if (!visibleUnlockedLayers || map.Layers[activeLayer].LayerType == LayerType.Attributes)
        {
            return new List<int> { activeLayer };
        }

        var targets = new List<int>();
        for (var i = 0; i < map.Layers.Count; i++)
        {
            if (map.Layers[i].LayerType == LayerType.Attributes || !IsFloodPaintable(map, i))
            {
                continue;
            }

            targets.Add(i);
        }

        return targets;
    }

    private static int FindAttributesLayerIndex(Map map)
    {
        for (var i = 0; i < map.Layers.Count; i++)
        {
            if (map.Layers[i].LayerType == LayerType.Attributes)
            {
                return i;
            }
        }

        return -1;
    }

    private static Dictionary<(int X, int Y), Tile> IndexTiles(Layer layer)
    {
        var index = new Dictionary<(int X, int Y), Tile>(layer.Tiles.Count);
        foreach (var tile in layer.Tiles)
        {
            index[(tile.X, tile.Y)] = tile;
        }

        return index;
    }

    /// <summary>Région 4-connexe de la couche graine. Les voisins hors carte ne sont pas enfilés.</summary>
    private static List<(int X, int Y)> CollectFloodRegion(
        Map map,
        Layer layer,
        int sx,
        int sy,
        bool respectAttributes,
        int externalAttributesLayer)
    {
        var cells = IndexTiles(layer);
        cells.TryGetValue((sx, sy), out var seed);

        Dictionary<(int X, int Y), Tile>? attrCells = null;
        Tile? attrSeed = null;
        if (respectAttributes && externalAttributesLayer >= 0)
        {
            attrCells = IndexTiles(map.Layers[externalAttributesLayer]);
            attrCells.TryGetValue((sx, sy), out attrSeed);
        }

        var region = new List<(int X, int Y)>();
        var pending = new Queue<(int X, int Y)>();
        var seen = new HashSet<(int X, int Y)>();
        Enqueue(sx, sy);

        while (pending.Count > 0)
        {
            var (x, y) = pending.Dequeue();
            cells.TryGetValue((x, y), out var here);
            if (!MatchesSeed(seed, here, respectAttributes))
            {
                continue;
            }

            if (attrCells is not null)
            {
                attrCells.TryGetValue((x, y), out var attrHere);
                if (!SameCollisionBarrier(attrSeed, attrHere))
                {
                    continue;
                }
            }

            region.Add((x, y));
            Enqueue(x - 1, y);
            Enqueue(x + 1, y);
            Enqueue(x, y - 1);
            Enqueue(x, y + 1);
        }

        return region;

        void Enqueue(int x, int y)
        {
            if ((uint)x >= (uint)map.Width || (uint)y >= (uint)map.Height)
            {
                return;
            }

            if (seen.Add((x, y)))
            {
                pending.Enqueue((x, y));
            }
        }
    }

    private static bool MatchesSeed(Tile? seed, Tile? here, bool respectAttributes)
    {
        if (seed is null)
        {
            return here is null;
        }

        if (!SameVisualTile(seed, here))
        {
            return false;
        }

        return !respectAttributes || SameCollisionBarrier(seed, here);
    }

    private static bool SamePlacedTile(Tile? existing, Tile stamp)
        => existing is not null && SameVisualTile(stamp, existing) && SameCollisionBarrier(stamp, existing);

    /// <summary>
    /// Signature de collision : type, script, warp et attributs. Le graphique (Src / AssetId) n’entre pas en compte,
    /// pour que la couche Attributs bloque le pot même si deux blocages n’ont pas la même image.
    /// </summary>
    private static bool SameCollisionBarrier(Tile? a, Tile? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Type != b.Type)
        {
            return false;
        }

        if (!string.Equals(a.ScriptId ?? string.Empty, b.ScriptId ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        if (a.Type == TileType.Warp
            && (a.WarpTargetMapId != b.WarpTargetMapId
                || a.WarpTargetX != b.WarpTargetX
                || a.WarpTargetY != b.WarpTargetY))
        {
            return false;
        }

        if (a.Attributes.Count != b.Attributes.Count)
        {
            return false;
        }

        if (a.Attributes.Count == 0)
        {
            return true;
        }

        var left = a.Attributes.Select(AttributeKey).ToArray();
        var right = b.Attributes.Select(AttributeKey).ToArray();
        Array.Sort(left, StringComparer.Ordinal);
        Array.Sort(right, StringComparer.Ordinal);
        for (var i = 0; i < left.Length; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string AttributeKey(ITileAttribute attribute) => attribute switch
    {
        BlockAttribute => "block",
        WarpAttribute warp => $"warp:{warp.TargetMapId:N}:{warp.TargetX}:{warp.TargetY}",
        ResourceAttribute resource => $"res:{resource.ResourceId}",
        _ => attribute.GetType().FullName ?? attribute.GetType().Name,
    };

    private static bool IsInBounds(Map map, int x, int y)
        => x >= 0 && y >= 0 && x < map.Width && y < map.Height;

    private static Tile CloneTile(Tile source, int x, int y)
    {
        var clone = new Tile
        {
            X = x,
            Y = y,
            Type = source.Type,
            SrcX = source.SrcX,
            SrcY = source.SrcY,
            TilesetId = source.TilesetId,
            AssetId = source.AssetId,
            WarpTargetMapId = source.WarpTargetMapId,
            WarpTargetX = source.WarpTargetX,
            WarpTargetY = source.WarpTargetY,
            ScriptId = source.ScriptId,
        };

        foreach (var attribute in source.Attributes)
        {
            clone.Attributes.Add(CloneAttribute(attribute));
        }

        return clone;
    }

    private static ITileAttribute CloneAttribute(ITileAttribute attribute) => attribute switch
    {
        BlockAttribute => new BlockAttribute(),
        WarpAttribute warp => new WarpAttribute
        {
            TargetMapId = warp.TargetMapId,
            TargetX = warp.TargetX,
            TargetY = warp.TargetY,
        },
        ResourceAttribute resource => new ResourceAttribute { ResourceId = resource.ResourceId },
        _ => throw new NotSupportedException(
            $"Attribut de tuile non copié : {attribute.GetType().Name}."),
    };

    private static bool SameVisualTile(Tile a, Tile? b)
    {
        if (b is null)
        {
            return false;
        }

        if (a.AssetId != b.AssetId || a.TilesetId != b.TilesetId || a.SrcX != b.SrcX || a.SrcY != b.SrcY || a.Type != b.Type)
        {
            return false;
        }

        return a.Type != TileType.Warp
               || (a.WarpTargetMapId == b.WarpTargetMapId
                   && a.WarpTargetX == b.WarpTargetX
                   && a.WarpTargetY == b.WarpTargetY);
    }
}
