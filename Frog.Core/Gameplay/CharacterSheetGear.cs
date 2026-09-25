using Frog.Core.Enums;

namespace Frog.Core.Gameplay;

/// <summary>Ligne de sac connue de la fiche (index fil + type catalogue, s'il est connu).</summary>
public readonly record struct EquipBagEntry(byte SlotIndex, ItemType? Type);

public enum CharacterSheetGearAction : byte
{
    None = 0,
    Equip = 1,
    Unequip = 2,
}

/// <summary>
/// Ordre vers les paquets existants <c>EquipRequest</c> / <c>UnequipRequest</c>.
/// Pas un champ de protocole : Hello reste 11.
/// </summary>
public readonly record struct CharacterSheetGearCommand(
    CharacterSheetGearAction Action,
    byte InventorySlot = 0,
    EquipmentSlotKind UnequipSlot = EquipmentSlotKind.None)
{
    public static CharacterSheetGearCommand None { get; } = new(CharacterSheetGearAction.None);

    public static CharacterSheetGearCommand Equip(byte inventorySlot) =>
        new(CharacterSheetGearAction.Equip, inventorySlot);

    public static CharacterSheetGearCommand Unequip(EquipmentSlotKind slot) =>
        new(CharacterSheetGearAction.Unequip, UnequipSlot: slot);
}

/// <summary>
/// Décide équiper / déséquiper depuis la fiche sans toucher au fil.
/// Arme et armure seulement. Tunique et casque restent un aperçu local.
/// </summary>
public static class CharacterSheetGear
{
    public static bool TryParseItemType(string? catalogType, out ItemType type)
    {
        type = ItemType.Unknown;
        return !string.IsNullOrWhiteSpace(catalogType)
            && Enum.TryParse(catalogType.Trim(), ignoreCase: true, out type)
            && Enum.IsDefined(type)
            && type != ItemType.Unknown;
    }

    public static bool TypeFits(EquipmentSlotKind slot, ItemType type) => slot switch
    {
        EquipmentSlotKind.Weapon => type == ItemType.Weapon,
        EquipmentSlotKind.Armor => type == ItemType.Armor,
        _ => false,
    };

    public static EquipmentSlotKind ServerSlot(PaperdollLayer layer) => layer switch
    {
        PaperdollLayer.Weapon => EquipmentSlotKind.Weapon,
        PaperdollLayer.Armor => EquipmentSlotKind.Armor,
        _ => EquipmentSlotKind.None,
    };

    /// <summary>Bouton Équiper ou double-clic : le serveur choisit l'emplacement selon le type d'objet.</summary>
    public static CharacterSheetGearCommand FromBagEquip(byte? selectedSlot) =>
        selectedSlot is byte slot
            ? CharacterSheetGearCommand.Equip(slot)
            : CharacterSheetGearCommand.None;

    /// <summary>
    /// Clic sur un emplacement arme ou armure.
    /// Objet du sac sélectionné du bon type : équiper (échange si l'emplacement est pris).
    /// Sinon, emplacement occupé : déséquiper.
    /// Sinon, sans sélection : premier objet du sac du bon type.
    /// </summary>
    public static CharacterSheetGearCommand FromSlotClick(
        PaperdollLayer layer,
        bool occupied,
        byte? selectedSlot,
        IReadOnlyList<EquipBagEntry> bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var serverSlot = ServerSlot(layer);
        if (serverSlot == EquipmentSlotKind.None)
        {
            return CharacterSheetGearCommand.None;
        }

        if (selectedSlot is byte selected && TryFind(bag, selected, out var picked))
        {
            if (picked.Type is ItemType type && TypeFits(serverSlot, type))
            {
                return CharacterSheetGearCommand.Equip(selected);
            }

            return occupied
                ? CharacterSheetGearCommand.Unequip(serverSlot)
                : CharacterSheetGearCommand.None;
        }

        if (occupied)
        {
            return CharacterSheetGearCommand.Unequip(serverSlot);
        }

        foreach (var entry in bag)
        {
            if (entry.Type is ItemType fit && TypeFits(serverSlot, fit))
            {
                return CharacterSheetGearCommand.Equip(entry.SlotIndex);
            }
        }

        return CharacterSheetGearCommand.None;
    }

    private static bool TryFind(IReadOnlyList<EquipBagEntry> bag, byte slot, out EquipBagEntry entry)
    {
        foreach (var row in bag)
        {
            if (row.SlotIndex == slot)
            {
                entry = row;
                return true;
            }
        }

        entry = default;
        return false;
    }
}
