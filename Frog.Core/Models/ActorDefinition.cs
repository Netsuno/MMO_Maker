using Frog.Core.Character;
using Frog.Core.Gameplay;

namespace Frog.Core.Models;

/// <summary>
/// Héros (acteur) éditable, calqué sur la base RPG Maker : nom affiché, classe liée,
/// visage, apparence paperdoll déjà en jeu (<see cref="CharacterLook"/>) et
/// équipement de départ arme/armure (<see cref="EquipmentSlotKind"/>).
/// Les caractéristiques de base suivent <see cref="CharacterStatsWire"/> et
/// <see cref="ClassDefinition"/> (1–99). Pas un champ de protocole.
/// </summary>
public sealed class ActorDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 4000;
    public const int MaxLogicalPathLength = 500;
    public const int MinStat = CharacterStatsWire.MinStat;
    public const int MaxStat = CharacterStatsWire.MaxStat;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? ClassId { get; set; }

    /// <summary>
    /// Chemin logique du visage, même convention que le sprite NPC.
    /// Vide : pas de portrait fichier (le paperdoll reste <see cref="CharacterLook"/>).
    /// </summary>
    public string? FaceLogicalPath { get; set; }

    public byte Body { get; set; }

    public byte Hair { get; set; }

    public byte Tunic { get; set; }

    public Guid? StartingWeaponItemId { get; set; }

    public Guid? StartingArmorItemId { get; set; }

    public int BaseHp { get; set; }

    public int BaseMp { get; set; }

    public int Str { get; set; }

    public int Agi { get; set; }

    public int Vit { get; set; }

    public int Int { get; set; }

    public int Dex { get; set; }

    public int Luck { get; set; }

    public CharacterLook ToLook() => new CharacterLook(Body, Hair, Tunic);

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant de héros manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom de héros invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (Description?.Length > MaxDescriptionLength)
        {
            error = $"Description trop longue ({MaxDescriptionLength} caractères maximum).";
            return false;
        }

        if (ClassId == Guid.Empty)
        {
            error = "L’identifiant de classe lié est invalide.";
            return false;
        }

        if (!TryValidateFacePath(FaceLogicalPath, out error))
        {
            return false;
        }

        if (Body >= CharacterLook.BodyCount
            || Hair >= CharacterLook.HairCount
            || Tunic >= CharacterLook.TunicCount)
        {
            error = "Apparence hors des planches déjà en jeu (corps, cheveux, tunique).";
            return false;
        }

        if (StartingWeaponItemId == Guid.Empty)
        {
            error = "L’identifiant de l’arme de départ est invalide.";
            return false;
        }

        if (StartingArmorItemId == Guid.Empty)
        {
            error = "L’identifiant de l’armure de départ est invalide.";
            return false;
        }

        if (StartingWeaponItemId is Guid weapon
            && StartingArmorItemId is Guid armor
            && weapon == armor)
        {
            error = "L’arme et l’armure de départ doivent être des objets distincts.";
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

        error = null;
        return true;
    }

    private static bool TryValidateFacePath(string? path, out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = null;
            return true;
        }

        if (path.Length > MaxLogicalPathLength)
        {
            error = $"Chemin de visage invalide (1–{MaxLogicalPathLength} caractères).";
            return false;
        }

        if (path.Contains('\\', StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(path))
        {
            error = "Chemin de visage doit être relatif, sans '..' ni séparateur Windows.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsValidStat(int value) => value is >= MinStat and <= MaxStat;
}
