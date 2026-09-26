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
        _ => string.Empty,
    };
}
