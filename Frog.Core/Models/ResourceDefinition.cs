namespace Frog.Core.Models;

/// <summary>Définition éditable d’une ressource récoltable, sans logique de récolte.</summary>
public sealed class ResourceDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 4000;
    public const int MaxLogicalPathLength = 500;
    public const int MinYieldQuantity = 1;
    public const int MaxYieldQuantity = 999;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string SpriteLogicalPath { get; set; } = string.Empty;

    public int RespawnSeconds { get; set; }

    public Guid? ToolItemId { get; set; }

    public Guid YieldItemId { get; set; }

    public int YieldQuantity { get; set; } = MinYieldQuantity;

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant de ressource manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom de ressource invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (Description?.Length > MaxDescriptionLength)
        {
            error = $"Description trop longue ({MaxDescriptionLength} caractères maximum).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SpriteLogicalPath)
            || SpriteLogicalPath.Length > MaxLogicalPathLength)
        {
            error = $"Chemin de sprite invalide (1–{MaxLogicalPathLength} caractères).";
            return false;
        }

        if (SpriteLogicalPath.Contains('\\', StringComparison.Ordinal)
            || SpriteLogicalPath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(SpriteLogicalPath))
        {
            error = "Le chemin de sprite doit être relatif, sans '..' ni séparateur Windows.";
            return false;
        }

        if (RespawnSeconds < 0)
        {
            error = "Le délai de réapparition doit être positif ou nul.";
            return false;
        }

        if (ToolItemId == Guid.Empty)
        {
            error = "L’identifiant de l’outil ne peut pas être vide.";
            return false;
        }

        if (YieldItemId == Guid.Empty)
        {
            error = "L’objet produit est obligatoire.";
            return false;
        }

        if (YieldQuantity is < MinYieldQuantity or > MaxYieldQuantity)
        {
            error = $"Quantité produite hors plage ({MinYieldQuantity}–{MaxYieldQuantity}).";
            return false;
        }

        error = null;
        return true;
    }
}

/// <summary>Placement éditable d’une ressource sur une carte.</summary>
public sealed class ResourceSpawnDefinition
{
    public Guid Id { get; set; }

    public Guid MapId { get; set; }

    public Guid ResourceId { get; set; }

    public int TileX { get; set; }

    public int TileY { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant de spawn de ressource manquant.";
            return false;
        }

        if (MapId == Guid.Empty)
        {
            error = "Carte du spawn obligatoire.";
            return false;
        }

        if (ResourceId == Guid.Empty)
        {
            error = "Ressource du spawn obligatoire.";
            return false;
        }

        if (TileX < 0 || TileY < 0)
        {
            error = "Les coordonnées du spawn doivent être positives ou nulles.";
            return false;
        }

        error = null;
        return true;
    }
}
