using Frog.Core.Constants;

namespace Frog.Core.Gameplay;

/// <summary>
/// Pose 1–N piles sur la tuile de mort, puis sur les tuiles libres adjacentes.
/// Si tout est bloqué, les piles restent sur la tuile d'origine pour ne pas perdre le butin.
/// </summary>
public static class GroundLootPlacement
{
    private static readonly (int Dx, int Dy)[] NeighborOffsets =
    [
        (0, 0),
        (1, 0),
        (0, 1),
        (-1, 0),
        (0, -1),
        (1, 1),
        (1, -1),
        (-1, 1),
        (-1, -1),
        (2, 0),
        (0, 2),
        (-2, 0),
        (0, -2),
    ];

    public static (int X, int Y) PixelToTile(int pixelX, int pixelY, int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        if (tileSizePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSizePixels));
        }

        var x = pixelX >= 0 ? pixelX / tileSizePixels : -1;
        var y = pixelY >= 0 ? pixelY / tileSizePixels : -1;
        return (x, y);
    }

    public static IReadOnlyList<(int PixelX, int PixelY)> ChooseDropPixels(
        int originPixelX,
        int originPixelY,
        int stackCount,
        Func<int, int, bool>? isTileFree = null,
        int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        if (stackCount <= 0)
        {
            return Array.Empty<(int, int)>();
        }

        if (tileSizePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSizePixels));
        }

        var origin = PixelToTile(originPixelX, originPixelY, tileSizePixels);
        var chosen = new List<(int PixelX, int PixelY)>(stackCount);
        foreach (var (dx, dy) in NeighborOffsets)
        {
            if (chosen.Count >= stackCount)
            {
                break;
            }

            var tx = origin.X + dx;
            var ty = origin.Y + dy;
            if (isTileFree is not null && !isTileFree(tx, ty))
            {
                continue;
            }

            chosen.Add(WorldMetrics.TileCenterToPixels(tx, ty, tileSizePixels));
        }

        if (chosen.Count == 0)
        {
            var fallback = WorldMetrics.TileCenterToPixels(origin.X, origin.Y, tileSizePixels);
            while (chosen.Count < stackCount)
            {
                chosen.Add(fallback);
            }

            return chosen;
        }

        while (chosen.Count < stackCount)
        {
            chosen.Add(chosen[0]);
        }

        return chosen;
    }
}
