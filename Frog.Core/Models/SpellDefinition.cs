using Frog.Core.Enums;

namespace Frog.Core.Models;

/// <summary>Définition éditable d’un sort ou d’une compétence avec identité stable.</summary>
public sealed class SpellDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxLogicalPathLength = 500;
    public const int MaxDescriptionLength = 4000;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public SpellKind Kind { get; set; }

    public int ManaCost { get; set; }

    public int CooldownMs { get; set; }

    public TargetType TargetType { get; set; }

    public string IconLogicalPath { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant sort/compétence manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom de sort/compétence invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (!Enum.IsDefined(Kind))
        {
            error = "Type de sort/compétence invalide.";
            return false;
        }

        if (ManaCost < 0)
        {
            error = "Le coût en mana doit être positif ou nul.";
            return false;
        }

        if (CooldownMs < 0)
        {
            error = "Le temps de recharge doit être positif ou nul.";
            return false;
        }

        if (!Enum.IsDefined(TargetType))
        {
            error = "Type de cible invalide.";
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

        if (Description?.Length > MaxDescriptionLength)
        {
            error = $"Description trop longue ({MaxDescriptionLength} caractères maximum).";
            return false;
        }

        error = null;
        return true;
    }
}
