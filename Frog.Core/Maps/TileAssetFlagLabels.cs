namespace Frog.Core.Maps;

/// <summary>
/// Libellés français du mode tileset (base de données VX, onglet Tilesets).
/// L’UI éditeur les reprend tels quels. Aucun asset VX.
/// </summary>
public static class TileAssetFlagLabels
{
    public const string PanelTitle = "Drapeaux";
    public const string PassageGlobal = "Passage (global)";
    public const string PassageFour = "Passage (4 directions)";
    public const string Priority = "Mode échelle";
    public const string Bush = "Carreaux obscurcissants";
    public const string Counter = "Carreaux d'interaction";
    public const string Damage = "Sol blessant";
    public const string Terrain = "Numéro de terrain";
    public const string Autotile = "Autotile";
    public const string AutotileGroup = "Groupe";
    public const string AutotileApply = "Appliquer le groupe";
    public const string JoinMenu = "Raccorder les autotiles";
    public const string EditMenu = "Groupe d’autotile de la tuile…";
    public const string North = "Nord";
    public const string South = "Sud";
    public const string East = "Est";
    public const string West = "Ouest";
    public const string Empty = "Choisissez une tuile 48×48.";

    public static string Hint(TileFlagEditMode mode) => mode switch
    {
        TileFlagEditMode.PassageGlobal => "Clic : ○ ouvert, × bloqué, ★ au-dessus.",
        TileFlagEditMode.PassageFourDirections => "Clic sur un bord : ouvre ou ferme cette direction.",
        TileFlagEditMode.Priority => "Clic : priorité 0–5. ○ au sol, ★ au-dessus.",
        TileFlagEditMode.Bush => "Clic : obscurcit le bas du sprite.",
        TileFlagEditMode.Counter => "Clic : interaction à travers le carreau.",
        TileFlagEditMode.Damage => "Clic : sol blessant.",
        TileFlagEditMode.Terrain => "Clic : numéro de terrain 0–7.",
        TileFlagEditMode.Autotile => "Nommez le groupe et le rôle. Le pinceau raccorde les voisins du même groupe.",
        _ => string.Empty,
    };

    public static string RoleLabel(AutotileRole role) => role switch
    {
        AutotileRole.None => "Aucun",
        AutotileRole.Center => "Centre",
        AutotileRole.North => "Bord nord",
        AutotileRole.East => "Bord est",
        AutotileRole.South => "Bord sud",
        AutotileRole.West => "Bord ouest",
        AutotileRole.NorthEast => "Coin nord-est",
        AutotileRole.NorthWest => "Coin nord-ouest",
        AutotileRole.SouthEast => "Coin sud-est",
        AutotileRole.SouthWest => "Coin sud-ouest",
        AutotileRole.Isolated => "Isolée",
        AutotileRole.Horizontal => "Barre horizontale",
        AutotileRole.Vertical => "Barre verticale",
        _ => "Aucun",
    };

    public static string RoleMark(AutotileRole role) => role switch
    {
        AutotileRole.Center => "C",
        AutotileRole.North => "N",
        AutotileRole.East => "E",
        AutotileRole.South => "S",
        AutotileRole.West => "O",
        AutotileRole.NorthEast => "NE",
        AutotileRole.NorthWest => "NO",
        AutotileRole.SouthEast => "SE",
        AutotileRole.SouthWest => "SO",
        AutotileRole.Isolated => "I",
        AutotileRole.Horizontal => "H",
        AutotileRole.Vertical => "V",
        _ => string.Empty,
    };

    public static string FormatBrush(TileAssetFlags flags)
    {
        var text = "n° terrain " + flags.Terrain.ToString();
        if (flags.AutotileRole != AutotileRole.None && !string.IsNullOrEmpty(flags.AutotileGroup))
        {
            text += " · " + flags.AutotileGroup + " · " + RoleLabel(flags.AutotileRole);
        }

        return text;
    }
}
