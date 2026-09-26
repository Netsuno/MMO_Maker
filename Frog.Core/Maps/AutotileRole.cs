namespace Frog.Core.Maps;

/// <summary>
/// Rôle d’une tuile 48×48 dans un groupe d’autotile.
/// Le pinceau choisit le rôle d’après les voisins du même groupe (4 directions).
/// Ce n’est pas une feuille A1–A5 : chaque rôle est un <see cref="TileAssetId"/> déjà auteur.
/// </summary>
public enum AutotileRole : byte
{
    None = 0,
    Center = 1,
    North = 2,
    East = 3,
    South = 4,
    West = 5,
    NorthEast = 6,
    NorthWest = 7,
    SouthEast = 8,
    SouthWest = 9,
    Isolated = 10,
    Horizontal = 11,
    Vertical = 12,
}

/// <summary>Jetons stables du fichier <c>tile-flags.json</c> (<c>autotileRole</c>).</summary>
public static class AutotileRoles
{
    public static string ToToken(AutotileRole role) => role switch
    {
        AutotileRole.None => "none",
        AutotileRole.Center => "center",
        AutotileRole.North => "north",
        AutotileRole.East => "east",
        AutotileRole.South => "south",
        AutotileRole.West => "west",
        AutotileRole.NorthEast => "northEast",
        AutotileRole.NorthWest => "northWest",
        AutotileRole.SouthEast => "southEast",
        AutotileRole.SouthWest => "southWest",
        AutotileRole.Isolated => "isolated",
        AutotileRole.Horizontal => "horizontal",
        AutotileRole.Vertical => "vertical",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static bool TryParse(string? token, out AutotileRole role)
    {
        role = token switch
        {
            "none" => AutotileRole.None,
            "center" => AutotileRole.Center,
            "north" => AutotileRole.North,
            "east" => AutotileRole.East,
            "south" => AutotileRole.South,
            "west" => AutotileRole.West,
            "northEast" => AutotileRole.NorthEast,
            "northWest" => AutotileRole.NorthWest,
            "southEast" => AutotileRole.SouthEast,
            "southWest" => AutotileRole.SouthWest,
            "isolated" => AutotileRole.Isolated,
            "horizontal" => AutotileRole.Horizontal,
            "vertical" => AutotileRole.Vertical,
            _ => AutotileRole.None,
        };

        return token is "none" or "center" or "north" or "east" or "south" or "west"
            or "northEast" or "northWest" or "southEast" or "southWest"
            or "isolated" or "horizontal" or "vertical";
    }
}
