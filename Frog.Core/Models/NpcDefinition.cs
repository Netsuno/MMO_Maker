namespace Frog.Core.Models;

public enum NpcKind : byte
{
    Npc = 0,
    Monster = 1,
}

/// <summary>Définition éditable d’un PNJ ou monstre avec identité stable.</summary>
public sealed class NpcDefinition
{
    public const int MinLevel = 1;
    public const int MaxLevel = 99;
    public const int MaxNameLength = 120;
    public const int MaxLogicalPathLength = 500;
    public const int MaxNotesLength = 2000;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public NpcKind Kind { get; set; }

    public string SpriteLogicalPath { get; set; } = string.Empty;

    public int Level { get; set; } = MinLevel;

    public string? Notes { get; set; }

    /// <summary>
    /// Alias entier optionnel compatible avec les références historiques
    /// <c>map_npc_spawns.npc_definition_id</c>.
    /// </summary>
    public int? EditorAliasId { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant NPC manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom NPC invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (!Enum.IsDefined(Kind))
        {
            error = "Type NPC/monstre invalide.";
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
            error = "Chemin de sprite doit être relatif, sans '..' ni séparateur Windows.";
            return false;
        }

        if (Level is < MinLevel or > MaxLevel)
        {
            error = $"Niveau hors plage ({MinLevel}–{MaxLevel}).";
            return false;
        }

        if (Notes?.Length > MaxNotesLength)
        {
            error = $"Notes trop longues ({MaxNotesLength} caractères maximum).";
            return false;
        }

        if (EditorAliasId is <= 0)
        {
            error = "EditorAliasId doit être > 0 lorsqu’il est défini.";
            return false;
        }

        error = null;
        return true;
    }
}
