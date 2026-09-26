namespace Frog.Core.Maps;

/// <summary>
/// Direction de passage, mode tileset de RPG Maker VX.
/// Nord est le haut de la carte (Y diminue). Les diagonales ne sont pas une direction de passage.
/// </summary>
public enum TilePassageDirection : byte
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

/// <summary>Pas cardinal d’une case vers la voisine.</summary>
public static class TilePassage
{
    public static TilePassageDirection Opposite(TilePassageDirection direction) => direction switch
    {
        TilePassageDirection.North => TilePassageDirection.South,
        TilePassageDirection.South => TilePassageDirection.North,
        TilePassageDirection.East => TilePassageDirection.West,
        TilePassageDirection.West => TilePassageDirection.East,
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>Vrai seulement pour un pas d’exactement une case, axe horizontal ou vertical.</summary>
    public static bool TryFromStep(int deltaX, int deltaY, out TilePassageDirection direction)
    {
        direction = deltaX switch
        {
            0 when deltaY == -1 => TilePassageDirection.North,
            0 when deltaY == 1 => TilePassageDirection.South,
            1 when deltaY == 0 => TilePassageDirection.East,
            -1 when deltaY == 0 => TilePassageDirection.West,
            _ => default,
        };

        return (deltaX == 0 && (deltaY == -1 || deltaY == 1))
               || (deltaY == 0 && (deltaX == -1 || deltaX == 1));
    }

    public static (int DeltaX, int DeltaY) Step(TilePassageDirection direction) => direction switch
    {
        TilePassageDirection.North => (0, -1),
        TilePassageDirection.South => (0, 1),
        TilePassageDirection.East => (1, 0),
        TilePassageDirection.West => (-1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}
