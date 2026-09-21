using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Rotation 90° horaire et miroirs d’une grille de tuiles (positions relatives, pas les pixels du tileset).</summary>
public enum TileSelectionTransformKind
{
    Rotate90Clockwise = 0,
    MirrorHorizontal = 1,
    MirrorVertical = 2,
}

public readonly record struct TileSelectionTransformResult(
    IReadOnlyList<Tile> Tiles,
    int Width,
    int Height);

/// <summary>Maths pures pour copier / coller / transformer une sélection de couche tuile.</summary>
public static class TileSelectionTransform
{
    public static (int Width, int Height) TransformSize(int width, int height, TileSelectionTransformKind kind)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "La sélection doit avoir une largeur et une hauteur > 0.");
        }

        return kind == TileSelectionTransformKind.Rotate90Clockwise
            ? (height, width)
            : (width, height);
    }

    public static void MapPoint(
        int x,
        int y,
        int width,
        int height,
        TileSelectionTransformKind kind,
        out int mappedX,
        out int mappedY)
    {
        var (newWidth, newHeight) = TransformSize(width, height, kind);
        _ = newWidth;
        _ = newHeight;
        switch (kind)
        {
            case TileSelectionTransformKind.Rotate90Clockwise:
                mappedX = height - 1 - y;
                mappedY = x;
                break;
            case TileSelectionTransformKind.MirrorHorizontal:
                mappedX = width - 1 - x;
                mappedY = y;
                break;
            case TileSelectionTransformKind.MirrorVertical:
                mappedX = x;
                mappedY = height - 1 - y;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Transformation de sélection inconnue.");
        }
    }

    public static TileSelectionTransformResult Apply(
        IEnumerable<Tile> tiles,
        int width,
        int height,
        TileSelectionTransformKind kind)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var (newWidth, newHeight) = TransformSize(width, height, kind);
        var list = new List<Tile>();
        foreach (var tile in tiles)
        {
            MapPoint(tile.X, tile.Y, width, height, kind, out var nx, out var ny);
            list.Add(MapEditOperations.CloneTileAt(tile, nx, ny));
        }

        return new TileSelectionTransformResult(list, newWidth, newHeight);
    }
}
