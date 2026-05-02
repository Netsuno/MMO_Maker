// #TODO (FR) : Étendre selon les catégories d’objets héritées (consommable, arme, armure, clé, quête).
namespace Frog.Core.Enums;

/// <summary>Catégories d’objets. Doivent rester stables pour I/O et protocole.</summary>
public enum ItemType : byte
{
    Unknown = 0,
    Consumable = 1,
    Weapon = 2,
    Armor = 3,
    Key = 4,
    Quest = 5
}
