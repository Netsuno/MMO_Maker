using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Opérations d’édition carte testables sans UI ni rendu.</summary>
public static class MapEditOperations
{
    public static bool IsLayerEditable(Map map, int layerIndex)
        => layerIndex >= 0 && layerIndex < map.Layers.Count && !map.Layers[layerIndex].Locked;

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
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(stamp);
        if (!IsLayerEditable(map, layerIndex))
        {
            return;
        }

        var minX = Math.Min(x0, x1);
        var maxX = Math.Max(x0, x1);
        var minY = Math.Min(y0, y1);
        var maxY = Math.Max(y0, y1);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                PaintTile(map, layerIndex, x, y, stamp);
            }
        }
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

    /// <summary>Remplissage par diffusion, borné aux dimensions de la carte.</summary>
    public static void FloodFill(Map map, int layerIndex, int sx, int sy, Tile replacement)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(replacement);
        if (!IsLayerEditable(map, layerIndex) || !IsInBounds(map, sx, sy))
        {
            return;
        }

        var layer = map.Layers[layerIndex];
        var start = layer.Tiles.FirstOrDefault(t => t.X == sx && t.Y == sy);
        var matchEmpty = start is null;

        var q = new Queue<(int x, int y)>();
        var seen = new HashSet<(int, int)>();
        q.Enqueue((sx, sy));
        var toPaint = new List<(int x, int y)>();

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            if (!seen.Add((x, y)) || !IsInBounds(map, x, y))
            {
                continue;
            }

            var here = layer.Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
            if (matchEmpty)
            {
                if (here is not null)
                {
                    continue;
                }
            }
            else if (start is null || !SameVisualTile(start, here))
            {
                continue;
            }

            toPaint.Add((x, y));
            q.Enqueue((x - 1, y));
            q.Enqueue((x + 1, y));
            q.Enqueue((x, y - 1));
            q.Enqueue((x, y + 1));
        }

        foreach (var (x, y) in toPaint)
        {
            PaintTile(map, layerIndex, x, y, replacement);
        }
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
