using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>Collision tuile joueur ↔ murs (Aligné avec <see cref="Frog.Server.Services.MapService"/> après refactor).</summary>
public static class MapCollision
{
    public static HashSet<(int X, int Y)> IndexBlockedTiles(Map map)
    {
        var blocked = new HashSet<(int X, int Y)>();
        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.Type == TileType.Block)
                {
                    blocked.Add((tile.X, tile.Y));
                }
            }
        }

        return blocked;
    }

    /// <summary>True si le cercle intersecte au moins une tuile <see cref="TileType.Block"/> indexée dans <paramref name="blockedTiles"/>.</summary>
    public static bool IsBlockedForPlayerCircle(
        Map map,
        HashSet<(int X, int Y)> blockedTiles,
        int centerPixelX,
        int centerPixelY,
        int radiusPixels,
        int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        if (map.Width <= 0 || map.Height <= 0)
        {
            return true;
        }

        var w = map.Width;
        var h = map.Height;

        var minTx = (centerPixelX - radiusPixels) / tileSizePixels;
        var minTy = (centerPixelY - radiusPixels) / tileSizePixels;
        var maxTx = (centerPixelX + radiusPixels) / tileSizePixels;
        var maxTy = (centerPixelY + radiusPixels) / tileSizePixels;

        minTx = Math.Clamp(minTx, 0, w - 1);
        maxTx = Math.Clamp(maxTx, 0, w - 1);
        minTy = Math.Clamp(minTy, 0, h - 1);
        maxTy = Math.Clamp(maxTy, 0, h - 1);

        for (var ty = minTy; ty <= maxTy; ty++)
        {
            for (var tx = minTx; tx <= maxTx; tx++)
            {
                if (!blockedTiles.Contains((tx, ty)))
                {
                    continue;
                }

                var left = tx * tileSizePixels;
                var top = ty * tileSizePixels;
                var right = left + tileSizePixels - 1;
                var bottom = top + tileSizePixels - 1;
                var nx = Math.Max(left, Math.Min(centerPixelX, right));
                var ny = Math.Max(top, Math.Min(centerPixelY, bottom));
                if (WorldMetrics.DistanceSquaredPixels(centerPixelX, centerPixelY, nx, ny) <= radiusPixels * radiusPixels)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
