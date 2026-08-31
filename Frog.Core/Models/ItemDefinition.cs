using Frog.Core.Enums;

namespace Frog.Core.Models;

/// <summary>Définition éditable d’un objet avec identité stable.</summary>
public sealed class ItemDefinition
{
    public const int MinStackSize = 1;
    public const int MaxStackSize = 999;
    public const int MaxNameLength = 120;
    public const int MaxLogicalPathLength = 500;
    public const int MaxDescriptionLength = 4000;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ItemType Kind { get; set; }

    public string IconLogicalPath { get; set; } = string.Empty;

    public int MaxStack { get; set; } = MinStackSize;

    public int BuyPrice { get; set; }

    public int SellPrice { get; set; }

    public string? Description { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant objet manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom d’objet invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (!Enum.IsDefined(Kind) || Kind == ItemType.Unknown)
        {
            error = "Type d’objet invalide.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(IconLogicalPath)
            || IconLogicalPath.Length > MaxLogicalPathLength)
        {
            error = $"Chemin d’icône invalide (1–{MaxLogicalPathLength} caractères).";
            return false;
        }

        if (IconLogicalPath.Contains('\\', StringComparison.Ordinal)
            || IconLogicalPath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(IconLogicalPath))
        {
            error = "Chemin d’icône doit être relatif, sans '..' ni séparateur Windows.";
            return false;
        }

        if (MaxStack is < MinStackSize or > MaxStackSize)
        {
            error = $"Taille de pile hors plage ({MinStackSize}–{MaxStackSize}).";
            return false;
        }

        if (BuyPrice < 0 || SellPrice < 0)
        {
            error = "Les prix d’achat et de vente doivent être positifs ou nuls.";
            return false;
        }

        if (Description?.Length > MaxDescriptionLength)
        {
            error = $"Description trop longue ({MaxDescriptionLength} caractères maximum).";
            return false;
        }

        error = null;
        return true;
    }
}
