using System.Text.RegularExpressions;

namespace Frog.Core.Models;

/// <summary>
/// Objet de carte publiable (prop placable). Distinct d’un objet d’inventaire.
/// L’identifiant de placement est celui du catalogue prefab (palette « Objets »).
/// </summary>
public sealed class MapObjectDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxLogicalPathLength = 500;
    public const int MaxPlacementIdLength = 64;
    public const int MinFootprintTiles = 1;
    public const int MaxFootprintTiles = 32;

    /// <summary>Même motif que <c>PrefabPlacementService.IsValidId</c> (minuscules, chiffres, tirets).</summary>
    private static readonly Regex PlacementIdPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Chemin logique d’asset (ex. <c>prefabs/lampe.png</c>), unique.</summary>
    public string LogicalPath { get; set; } = string.Empty;

    /// <summary>Id stable du prefab posé sur la carte (ex. <c>obj-ab12</c>).</summary>
    public string PlacementId { get; set; } = string.Empty;

    public int FootprintWidthTiles { get; set; } = 1;

    public int FootprintHeightTiles { get; set; } = 1;

    public int WidthPixels { get; set; }

    public int HeightPixels { get; set; }

    public string Sha256Hex { get; set; } = string.Empty;

    /// <summary>PNG publié. Recopié vers <c>Prefabs/</c> pour la palette de placement.</summary>
    public byte[]? PngBytes { get; set; }

    public string SpriteFileName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LogicalPath))
            {
                return string.Empty;
            }

            return Path.GetFileName(LogicalPath.Replace('\\', '/').Trim());
        }
    }

    public static string CreatePlacementId(Guid id)
    {
        if (id == Guid.Empty)
        {
            id = Guid.NewGuid();
        }

        return "obj-" + id.ToString("N");
    }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant d’objet de carte manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > MaxNameLength)
        {
            error = $"Nom invalide (1–{MaxNameLength} caractères).";
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

        var placement = PlacementId.Trim();
        if (placement.Length is < 1 or > MaxPlacementIdLength || !PlacementIdPattern.IsMatch(placement))
        {
            error = "Identifiant de placement invalide (minuscules, chiffres et tirets).";
            return false;
        }

        if (FootprintWidthTiles is < MinFootprintTiles or > MaxFootprintTiles
            || FootprintHeightTiles is < MinFootprintTiles or > MaxFootprintTiles)
        {
            error = $"Empreinte hors plage ({MinFootprintTiles}–{MaxFootprintTiles} tuiles).";
            return false;
        }

        if (WidthPixels <= 0 || HeightPixels <= 0)
        {
            error = "Dimensions pixels invalides.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Sha256Hex)
            || Sha256Hex.Length != 64
            || !IsHex(Sha256Hex))
        {
            error = "SHA-256 hex invalide (64 caractères hex).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SpriteFileName)
            || SpriteFileName.Contains("..", StringComparison.Ordinal))
        {
            error = "Nom de sprite invalide.";
            return false;
        }

        error = null;
        return true;
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> bytes)
        => TilesetDefinition.ComputeSha256Hex(bytes);

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
