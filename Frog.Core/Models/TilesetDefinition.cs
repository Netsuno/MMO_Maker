using System.Security.Cryptography;
using System.Text;

namespace Frog.Core.Models;

/// <summary>Définition de tileset (contenu éditable) — identité stable <see cref="Id"/>.</summary>
public sealed class TilesetDefinition
{
    public const int MinTileSizePixels = 8;
    public const int MaxTileSizePixels = 256;
    public const int MaxNameLength = 120;
    public const int MaxLogicalPathLength = 500;

    public Guid Id { get; set; }

    /// <summary>Nom affiché (références UI par nom ; stockage par Id).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Chemin logique d’asset (ex. <c>tiles/grass.png</c>), unique.</summary>
    public string LogicalPath { get; set; } = string.Empty;

    public int TileSizePixels { get; set; } = 32;

    public int WidthPixels { get; set; }

    public int HeightPixels { get; set; }

    /// <summary>Empreinte SHA-256 hex (64 caractères) du fichier source.</summary>
    public string Sha256Hex { get; set; } = string.Empty;

    /// <summary>
    /// Alias entier optionnel pour peindre les cartes (compat <see cref="Tile.TilesetId"/>).
    /// Unique lorsqu’assigné.
    /// </summary>
    public int? EditorPaletteId { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant tileset manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom tileset invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LogicalPath) || LogicalPath.Length > MaxLogicalPathLength)
        {
            error = $"Chemin logique invalide (1–{MaxLogicalPathLength} caractères).";
            return false;
        }

        if (LogicalPath.Contains('\\', StringComparison.Ordinal)
            || LogicalPath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(LogicalPath))
        {
            error = "Chemin logique doit être relatif, sans '..' ni séparateur Windows.";
            return false;
        }

        if (TileSizePixels is < MinTileSizePixels or > MaxTileSizePixels)
        {
            error = $"Taille de tuile hors plage ({MinTileSizePixels}–{MaxTileSizePixels}).";
            return false;
        }

        if (WidthPixels <= 0 || HeightPixels <= 0)
        {
            error = "Dimensions pixels invalides.";
            return false;
        }

        if (WidthPixels % TileSizePixels != 0 || HeightPixels % TileSizePixels != 0)
        {
            error = "Dimensions non multiples de la taille de tuile.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Sha256Hex)
            || Sha256Hex.Length != 64
            || !IsHex(Sha256Hex))
        {
            error = "SHA-256 hex invalide (64 caractères hex).";
            return false;
        }

        if (EditorPaletteId is <= 0)
        {
            error = "EditorPaletteId doit être > 0 lorsqu’il est défini.";
            return false;
        }

        error = null;
        return true;
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash);
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            var ok = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
