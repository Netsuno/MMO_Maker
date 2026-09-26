using Frog.Core.Enums;

namespace Frog.Core.Gameplay;

/// <summary>
/// Lien objet équipable → emplacement paperdoll déjà en jeu.
/// Arme et armure seulement (<see cref="EquipmentSlotKind"/>, <see cref="PaperdollLayer"/>).
/// Pas un champ de protocole et pas une nouvelle planche.
/// </summary>
public static class EquipPaperdollSlot
{
    public static bool TryResolve(ItemType kind, out EquipmentSlotKind slot, out PaperdollLayer layer)
    {
        switch (kind)
        {
            case ItemType.Weapon:
                layer = PaperdollLayer.Weapon;
                slot = CharacterSheetGear.ServerSlot(layer);
                return slot == EquipmentSlotKind.Weapon;
            case ItemType.Armor:
                layer = PaperdollLayer.Armor;
                slot = CharacterSheetGear.ServerSlot(layer);
                return slot == EquipmentSlotKind.Armor;
            default:
                slot = EquipmentSlotKind.None;
                layer = default;
                return false;
        }
    }

    /// <summary>Mêmes libellés que la fiche perso : Arme, Armure.</summary>
    public static string French(ItemType kind) => kind switch
    {
        ItemType.Weapon => "Arme",
        ItemType.Armor => "Armure",
        _ => "—",
    };
}
