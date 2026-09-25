namespace Frog.Editor.Services;

/// <summary>Agrégat par tuile pour l’overlay marqueurs sur le canevas (plusieurs placements possibles sur une même case).</summary>
public readonly record struct MapEventMarkerView(
    int TileX,
    int TileY,
    int PlacementCount,
    string PrimarySlug,
    string PrimaryTriggerKind,
    string PrimaryDisplayName = "",
    string PrimaryPlacementKey = "");
