using Frog.Core.Gameplay;

namespace Frog.Client.Models;

/// <summary>
/// Slot paperdoll. <see cref="Weapon"/> et <see cref="Armor"/> ont les mêmes
/// valeurs que <see cref="EquipmentSlotKind"/> (paquet existant). Casque, tunique
/// et main gauche sont des couches visuelles : le snapshot ne les porte pas.
/// </summary>
public enum EquipmentSlot : byte
{
    None = 0,
    Weapon = 1,
    Armor = 2,
    Headwear = 3,
    Offhand = 4,
    Tunic = 5,
}

/// <summary>Correspondance slot visuel ↔ slot serveur, quand elle existe.</summary>
public static class EquipmentSlotMapping
{
    public static bool TryGetServerSlot(EquipmentSlot slot, out EquipmentSlotKind serverSlot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon:
                serverSlot = EquipmentSlotKind.Weapon;
                return true;
            case EquipmentSlot.Armor:
                serverSlot = EquipmentSlotKind.Armor;
                return true;
            default:
                serverSlot = EquipmentSlotKind.None;
                return false;
        }
    }

    public static EquipmentSlot FromServerSlot(EquipmentSlotKind kind) => kind switch
    {
        EquipmentSlotKind.Weapon => EquipmentSlot.Weapon,
        EquipmentSlotKind.Armor => EquipmentSlot.Armor,
        _ => EquipmentSlot.None,
    };
}
