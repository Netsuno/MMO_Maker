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

    private static bool IsInBounds(Map map, int x, int y)
        => x >= 0 && y >= 0 && x < map.Width && y < map.Height;

    private static Tile CloneTile(Tile source, int x, int y)
        => new()
        {
            X = x,
            Y = y,
            Type = source.Type,
            SrcX = source.SrcX,
            SrcY = source.SrcY,
            TilesetId = source.TilesetId,
            WarpTargetMapId = source.WarpTargetMapId,
            WarpTargetX = source.WarpTargetX,
            WarpTargetY = source.WarpTargetY,
            ScriptId = source.ScriptId,
        };

    private static bool SameVisualTile(Tile a, Tile? b)
    {
        if (b is null)
        {
            return false;
        }

        if (a.TilesetId != b.TilesetId || a.SrcX != b.SrcX || a.SrcY != b.SrcY || a.Type != b.Type)
        {
            return false;
        }

        return a.Type != TileType.Warp
               || (a.WarpTargetMapId == b.WarpTargetMapId
                   && a.WarpTargetX == b.WarpTargetX
                   && a.WarpTargetY == b.WarpTargetY);
    }
}
