using Frog.Core.Constants;

namespace Frog.Core.Maps;

/// <summary>
/// Modes du tileset, dans l’ordre des boutons de la base VX (F9).
/// Le clic sur une vignette 48×48 applique le mode courant.
/// </summary>
public enum TileFlagEditMode : byte
{
    PassageGlobal = 0,
    PassageFourDirections = 1,
    Priority = 2,
    Bush = 3,
    Counter = 4,
    Damage = 5,
    Terrain = 6,
}

/// <summary>Application d’un clic de mode sur les drapeaux d’une tuile. Pas d’image VX.</summary>
public static class TileFlagEdit
{
    public static bool TryHitDirection(int localX, int localY, int size, out TilePassageDirection direction)
    {
        direction = default;
        if (size <= 0 || localX < 0 || localY < 0 || localX >= size || localY >= size)
        {
            return false;
        }

        var edge = Math.Max(1, size / 3);
        if (localY < edge)
        {
            direction = TilePassageDirection.North;
            return true;
        }

        if (localY >= size - edge)
        {
            direction = TilePassageDirection.South;
            return true;
        }

        if (localX < edge)
        {
            direction = TilePassageDirection.West;
            return true;
        }

        if (localX >= size - edge)
        {
            direction = TilePassageDirection.East;
            return true;
        }

        return false;
    }

    public static TileAssetFlags Apply(TileAssetFlags flags, TileFlagEditMode mode, int localX, int localY, int tileSize = TileAssetMetrics.TargetTileSizePixels)
    {
        switch (mode)
        {
            case TileFlagEditMode.PassageGlobal:
                return flags.CyclePassageGlobal();
            case TileFlagEditMode.PassageFourDirections:
                return TryHitDirection(localX, localY, tileSize, out var direction)
                    ? flags.WithPassage(direction, !flags.Allows(direction)) with { Star = false }
                    : flags;
            case TileFlagEditMode.Priority:
                return flags.CyclePriority();
            case TileFlagEditMode.Bush:
                return flags.WithBush(!flags.Bush);
            case TileFlagEditMode.Counter:
                return flags.WithCounter(!flags.Counter);
            case TileFlagEditMode.Damage:
                return flags.WithDamage(!flags.Damage);
            case TileFlagEditMode.Terrain:
                return flags.CycleTerrain();
            default:
                return flags;
        }
    }

    public static string OverlayText(TileAssetFlags flags, TileFlagEditMode mode) => mode switch
    {
        TileFlagEditMode.PassageGlobal => flags.PassageMark,
        TileFlagEditMode.PassageFourDirections => string.Empty,
        TileFlagEditMode.Priority => flags.PriorityMark,
        TileFlagEditMode.Bush => flags.Bush ? "■" : string.Empty,
        TileFlagEditMode.Counter => flags.Counter ? "◆" : string.Empty,
        TileFlagEditMode.Damage => flags.Damage ? "●" : string.Empty,
        TileFlagEditMode.Terrain => flags.Terrain.ToString(),
        _ => string.Empty,
    };
}
