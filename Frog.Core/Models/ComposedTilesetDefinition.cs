using Frog.Core.Constants;
using Frog.Core.Maps;

namespace Frog.Core.Models;

/// <summary>
/// Tileset composé de tuiles choisies (48×48), pas d’une feuille découpée.
/// L’ordre de <see cref="Tiles"/> est l’ordre de la palette.
/// </summary>
public sealed class ComposedTilesetDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxLogicalPathLength = 500;
    public const int MaxDisplayNameLength = 120;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Chemin logique unique (ex. <c>tiles/composed/herbe.tileset</c>).</summary>
    public string LogicalPath { get; set; } = string.Empty;

    public List<ComposedTileRef> Tiles { get; set; } = new();

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant de tileset manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > MaxNameLength)
        {
            error = $"Nom de tileset invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LogicalPath) || LogicalPath.Trim().Length > MaxLogicalPathLength)
        {
            error = $"Chemin logique invalide (1–{MaxLogicalPathLength} caractères).";
            return false;
        }

        var path = LogicalPath.Trim().Replace('\\', '/');
        if (path.Contains('\\', StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(path))
        {
            error = "Chemin logique doit être relatif, sans '..' ni séparateur Windows.";
            return false;
        }

        if (Tiles.Count is < 1 or > WorkingTileset.MaxTileCount)
        {
            error = Tiles.Count == 0
                ? "Ajoutez au moins une tuile."
                : $"Tileset trop grand (> {WorkingTileset.MaxTileCount} tuiles).";
            return false;
        }

        for (var i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];
            if (tile is null)
            {
                error = $"Tuile {i + 1} manquante.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(tile.DisplayName)
                && tile.DisplayName.Trim().Length > MaxDisplayNameLength)
            {
                error = $"Nom de la tuile {i + 1} trop long (max {MaxDisplayNameLength}).";
                return false;
            }

            if (tile.NormalizedRgba is null
                || tile.NormalizedRgba.Length != TileAssetMetrics.CanonicalPixelByteCount)
            {
                error = $"Tuile {i + 1} : pixels {TileAssetMetrics.TargetTileSizePixels}×{TileAssetMetrics.TargetTileSizePixels} attendus.";
                return false;
            }

            if (!TileAssetId.TryParse(tile.TileAssetId, out var id) || id.IsNone)
            {
                error = $"Tuile {i + 1} : TileAssetId invalide.";
                return false;
            }

            TileAssetId hashed;
            try
            {
                hashed = TileAssetId.FromNormalizedRgba(tile.NormalizedRgba);
            }
            catch (ArgumentException)
            {
                error = $"Tuile {i + 1} : pixels {TileAssetMetrics.TargetTileSizePixels}×{TileAssetMetrics.TargetTileSizePixels} attendus.";
                return false;
            }

            if (hashed.IsNone || hashed != id)
            {
                error = $"Tuile {i + 1} : TileAssetId incohérent avec les pixels.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public void Normalize()
    {
        Name = Name.Trim();
        LogicalPath = LogicalPath.Trim().Replace('\\', '/');
        foreach (var tile in Tiles)
        {
            if (tile is null)
            {
                continue;
            }

            tile.TileAssetId = tile.TileAssetId.Trim().ToLowerInvariant();
            tile.DisplayName = string.IsNullOrWhiteSpace(tile.DisplayName) ? null : tile.DisplayName.Trim();
        }
    }
}

/// <summary>Une tuile choisie : identifiant de contenu et RGBA prémultiplié 48×48.</summary>
public sealed class ComposedTileRef
{
    public string TileAssetId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    /// <summary>9216 octets RGBA8 prémultipliés. Le SHA-256 de ce buffer est <see cref="TileAssetId"/>.</summary>
    public byte[] NormalizedRgba { get; set; } = Array.Empty<byte>();
}
