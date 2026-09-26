namespace Frog.Core.Models;

/// <summary>
/// Classe / métier éditable : nom, notes (description), PV/PM, caractéristiques 1–99,
/// sort de départ optionnel et équipement par défaut (arme, armure).
/// Les emplacements suivent <c>EquipmentSlotKind</c> déjà en jeu. Pas un champ de protocole.
/// </summary>
public sealed class ClassDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 4000;
    public const int MinStat = 1;
    public const int MaxStat = 99;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int BaseHp { get; set; }

    public int BaseMp { get; set; }

    public int Str { get; set; }

    public int Agi { get; set; }

    public int Vit { get; set; }

    public int Int { get; set; }

    public int Dex { get; set; }

    public int Luck { get; set; }

    public Guid? StartingSpellId { get; set; }

    /// <summary>Objet publié de type arme, indice d’équipement par défaut. Vide : aucun.</summary>
    public Guid? DefaultWeaponItemId { get; set; }

    /// <summary>Objet publié de type armure, indice d’équipement par défaut. Vide : aucun.</summary>
    public Guid? DefaultArmorItemId { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant de classe manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom de classe invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (Description?.Length > MaxDescriptionLength)
        {
            error = $"Description trop longue ({MaxDescriptionLength} caractères maximum).";
            return false;
        }

        if (BaseHp <= 0 || BaseMp <= 0)
        {
            error = "Les points de vie et de mana de base doivent être strictement positifs.";
            return false;
        }

        if (!IsValidStat(Str)
            || !IsValidStat(Agi)
            || !IsValidStat(Vit)
            || !IsValidStat(Int)
            || !IsValidStat(Dex)
            || !IsValidStat(Luck))
        {
            error = $"Chaque statistique doit être comprise entre {MinStat} et {MaxStat}.";
            return false;
        }

        if (StartingSpellId == Guid.Empty)
        {
            error = "L’identifiant du sort de départ est invalide.";
            return false;
        }

        if (DefaultWeaponItemId == Guid.Empty)
        {
            error = "L’identifiant de l’arme par défaut est invalide.";
            return false;
        }

        if (DefaultArmorItemId == Guid.Empty)
        {
            error = "L’identifiant de l’armure par défaut est invalide.";
            return false;
        }

        if (DefaultWeaponItemId is Guid weapon
            && DefaultArmorItemId is Guid armor
            && weapon == armor)
        {
            error = "L’arme et l’armure par défaut doivent être des objets distincts.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsValidStat(int value) => value is >= MinStat and <= MaxStat;
}
