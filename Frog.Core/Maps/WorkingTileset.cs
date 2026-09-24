namespace Frog.Core.Maps;

/// <summary>
/// Palette de travail de l’éditeur : liste nommée et ordonnée de <see cref="TileAssetId"/>.
/// Pas de disposition pixels ici — le canevas (position dans la palette) appartient à l’UI éditeur.
/// Une carte ne stocke jamais ces positions.
/// </summary>
public sealed class WorkingTileset
{
    public const int MaxNameLength = 120;
    public const int MaxTileCount = 8192;

    public string Name { get; set; } = string.Empty;

    public List<TileAssetId> Tiles { get; } = new();

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom de palette invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (Tiles.Count > MaxTileCount)
        {
            error = $"Palette trop grande (> {MaxTileCount}).";
            return false;
        }

        for (var i = 0; i < Tiles.Count; i++)
        {
            if (Tiles[i].IsNone)
            {
                error = $"Palette « {Name} » : tuile {i} sans TileAssetId.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
