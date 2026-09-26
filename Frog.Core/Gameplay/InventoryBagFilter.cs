using Frog.Core.Enums;

namespace Frog.Core.Gameplay;

/// <summary>
/// Filtre d'affichage du sac. Aucun bouton enfoncé : toute la liste.
/// Pas un champ de protocole : Hello reste 11.
/// </summary>
public enum InventoryBagCategory : byte
{
    All = 0,
    Weapon = 1,
    Armor = 2,
    Item = 3,
}

/// <summary>Arme, Armure, ou Objet (tout ce qui n'est pas une arme ou une armure).</summary>
public static class InventoryBagFilter
{
    public static string Label(InventoryBagCategory category) => category switch
    {
        InventoryBagCategory.Weapon => EquipPaperdollSlot.French(ItemType.Weapon),
        InventoryBagCategory.Armor => EquipPaperdollSlot.French(ItemType.Armor),
        InventoryBagCategory.Item => "Objet",
        _ => "Tout",
    };

    public static bool Includes(InventoryBagCategory category, ItemType? type) => category switch
    {
        InventoryBagCategory.Weapon => type == ItemType.Weapon,
        InventoryBagCategory.Armor => type == ItemType.Armor,
        InventoryBagCategory.Item => type is not (ItemType.Weapon or ItemType.Armor),
        _ => true,
    };
}
