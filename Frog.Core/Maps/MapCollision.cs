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
                if (tile.Type == TileType.Block || FullyBlockedByFlags(tile, map.TileFlags))
                {
                    blocked.Add((tile.X, tile.Y));
                }
            }
        }

        return blocked;
    }

    /// <summary>
    /// La case laisse passer <paramref name="direction"/>.
    /// <see cref="TileType.Block"/> ferme les quatre côtés.
    /// Sinon, le premier drapeau qui ferme ce côté ferme la case (le plus restrictif entre les couches).
    /// Une case vide reste ouverte. Sans table de drapeaux, seul <see cref="TileType.Block"/> ferme.
    /// </summary>
    public static bool CellAllows(Map map, int x, int y, TilePassageDirection direction)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return false;
        }

        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.X != x || tile.Y != y)
                {
                    continue;
                }

                if (tile.Type == TileType.Block)
                {
                    return false;
                }

                if (map.TileFlags is not null
                    && !tile.AssetId.IsNone
                    && !map.TileFlags.Get(tile.AssetId).Allows(direction))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Pas d’une case vers une voisine cardinale (sortie + entrée inverse).</summary>
    public static bool AllowsTileStep(Map map, int fromX, int fromY, int toX, int toY)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!TilePassage.TryFromStep(toX - fromX, toY - fromY, out var direction))
        {
            return false;
        }

        return CellAllows(map, fromX, fromY, direction)
               && CellAllows(map, toX, toY, TilePassage.Opposite(direction));
    }

    /// <summary>
    /// Passage au franchissement de case, quand la carte porte des drapeaux.
    /// Sans drapeaux : vrai (la collision cercle / <see cref="TileType.Block"/> reste seule).
    /// Le déplacement suit l’axe horizontal puis vertical, une case à la fois.
    /// </summary>
    public static bool AllowsPixelMove(Map map, int fromPixelX, int fromPixelY, int toPixelX, int toPixelY, int tileSizePixels)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.TileFlags is not { Count: > 0 } || tileSizePixels <= 0)
        {
            return true;
        }

        var fromX = fromPixelX / tileSizePixels;
        var fromY = fromPixelY / tileSizePixels;
        var toX = toPixelX / tileSizePixels;
        var toY = toPixelY / tileSizePixels;
        var steps = Math.Abs(toX - fromX) + Math.Abs(toY - fromY);
        if (steps == 0)
        {
            return true;
        }

        if (steps > 64)
        {
            return false;
        }

        var x = fromX;
        var y = fromY;
        while (x != toX)
        {
            var next = x + Math.Sign(toX - x);
            if (!AllowsTileStep(map, x, y, next, y))
            {
                return false;
            }

            x = next;
        }

        while (y != toY)
        {
            var next = y + Math.Sign(toY - y);
            if (!AllowsTileStep(map, x, y, x, next))
            {
                return false;
            }

            y = next;
        }

        return true;
    }

    public static bool CellIsBush(Map map, int x, int y) => CellHas(map, x, y, flags => flags.Bush);

    public static bool CellIsCounter(Map map, int x, int y) => CellHas(map, x, y, flags => flags.Counter);

    public static bool CellDealsDamage(Map map, int x, int y) => CellHas(map, x, y, flags => flags.Damage);

    /// <summary>Plus haute priorité des tuiles de la case (0 si aucune). 0 est sous le personnage, 5 au-dessus.</summary>
    public static byte CellPriority(Map map, int x, int y)
    {
        byte max = 0;
        Visit(map, x, y, flags =>
        {
            if (flags.Priority > max)
            {
                max = flags.Priority;
            }
        });
        return max;
    }

    /// <summary>
    /// Comptoir : l’interaction depuis <paramref name="fromX"/>,<paramref name="fromY"/> vise deux cases plus loin
    /// si la case intermédiaire porte le drapeau. Le passage n’est pas modifié.
    /// </summary>
    public static bool TryCounterTarget(
        Map map,
        int fromX,
        int fromY,
        TilePassageDirection direction,
        out int targetX,
        out int targetY)
    {
        ArgumentNullException.ThrowIfNull(map);
        var (dx, dy) = TilePassage.Step(direction);
        var midX = fromX + dx;
        var midY = fromY + dy;
        targetX = fromX + (dx * 2);
        targetY = fromY + (dy * 2);
        if (!CellIsCounter(map, midX, midY)
            || targetX < 0
            || targetY < 0
            || targetX >= map.Width
            || targetY >= map.Height)
        {
            targetX = 0;
            targetY = 0;
            return false;
        }

        return true;
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

    private static bool FullyBlockedByFlags(Tile tile, TileAssetFlagTable? flags)
    {
        if (flags is not { Count: > 0 } || tile.AssetId.IsNone)
        {
            return false;
        }

        return flags.Get(tile.AssetId).BlocksAllPassage;
    }

    private static bool CellHas(Map map, int x, int y, Func<TileAssetFlags, bool> predicate)
    {
        var found = false;
        Visit(map, x, y, flags =>
        {
            if (predicate(flags))
            {
                found = true;
            }
        });
        return found;
    }

    private static void Visit(Map map, int x, int y, Action<TileAssetFlags> visit)
    {
        if (map.TileFlags is not { Count: > 0 } || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return;
        }

        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.X != x || tile.Y != y || tile.AssetId.IsNone)
                {
                    continue;
                }

                visit(map.TileFlags.Get(tile.AssetId));
            }
        }
    }
}
