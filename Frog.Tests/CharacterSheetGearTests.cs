using System;
using System.Collections.Generic;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Tests;

/// <summary>Fiche perso — équiper / déséquiper arme et armure via les paquets existants (Netsun).</summary>
public sealed class CharacterSheetGearTests
{
    [Fact]
    public void ProtocolAndWorldTile_StayUnchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(1, (byte)EquipmentSlotKind.Weapon);
        Assert.Equal(2, (byte)EquipmentSlotKind.Armor);
    }

    [Fact]
    public void BagEquip_SendsSelectedInventorySlot_ServerChoosesKind()
    {
        Assert.Equal(CharacterSheetGearAction.None, CharacterSheetGear.FromBagEquip(null).Action);
        var command = CharacterSheetGear.FromBagEquip(3);
        Assert.Equal(CharacterSheetGearAction.Equip, command.Action);
        Assert.Equal((byte)3, command.InventorySlot);
    }

    [Fact]
    public void SlotClick_EquipsMatchingBagItem_OrUnequips_OrFirstFit()
    {
        var bag = new List<EquipBagEntry>
        {
            new(0, ItemType.Consumable),
            new(2, ItemType.Weapon),
            new(4, ItemType.Armor),
        };

        var fromButtonMismatch = CharacterSheetGear.FromSlotClick(
            PaperdollLayer.Weapon,
            occupied: false,
            selectedSlot: 0,
            bag);
        Assert.Equal(CharacterSheetGearAction.None, fromButtonMismatch.Action);

        var swap = CharacterSheetGear.FromSlotClick(
            PaperdollLayer.Weapon,
            occupied: true,
            selectedSlot: 2,
            bag);
        Assert.Equal(CharacterSheetGearAction.Equip, swap.Action);
        Assert.Equal((byte)2, swap.InventorySlot);

        var unequip = CharacterSheetGear.FromSlotClick(
            PaperdollLayer.Weapon,
            occupied: true,
            selectedSlot: 4,
            bag);
        Assert.Equal(CharacterSheetGearAction.Unequip, unequip.Action);
        Assert.Equal(EquipmentSlotKind.Weapon, unequip.UnequipSlot);

        var firstWeapon = CharacterSheetGear.FromSlotClick(
            PaperdollLayer.Weapon,
            occupied: false,
            selectedSlot: null,
            bag);
        Assert.Equal(CharacterSheetGearAction.Equip, firstWeapon.Action);
        Assert.Equal((byte)2, firstWeapon.InventorySlot);

        var firstArmor = CharacterSheetGear.FromSlotClick(
            PaperdollLayer.Armor,
            occupied: false,
            selectedSlot: null,
            bag);
        Assert.Equal((byte)4, firstArmor.InventorySlot);

        var bare = CharacterSheetGear.FromSlotClick(
            PaperdollLayer.Armor,
            occupied: false,
            selectedSlot: null,
            bag: new[] { new EquipBagEntry(0, ItemType.Consumable) });
        Assert.Equal(CharacterSheetGearAction.None, bare.Action);

        var occupiedBare = CharacterSheetGear.FromSlotClick(
            PaperdollLayer.Armor,
            occupied: true,
            selectedSlot: null,
            bag: []);
        Assert.Equal(EquipmentSlotKind.Armor, occupiedBare.UnequipSlot);
    }

    [Fact]
    public void LocalLayers_DoNotSendGearCommands()
    {
        var bag = new[] { new EquipBagEntry(1, ItemType.Weapon) };
        foreach (var layer in new[] { PaperdollLayer.Body, PaperdollLayer.Head, PaperdollLayer.Tunic, PaperdollLayer.Hat })
        {
            var command = CharacterSheetGear.FromSlotClick(layer, occupied: true, selectedSlot: 1, bag);
            Assert.Equal(CharacterSheetGearAction.None, command.Action);
        }
    }

    [Fact]
    public void CatalogType_ParsesWeaponAndArmor_Only()
    {
        Assert.True(CharacterSheetGear.TryParseItemType("Weapon", out var weapon));
        Assert.Equal(ItemType.Weapon, weapon);
        Assert.True(CharacterSheetGear.TryParseItemType(" armor ", out var armor));
        Assert.Equal(ItemType.Armor, armor);
        Assert.False(CharacterSheetGear.TryParseItemType("Unknown", out _));
        Assert.False(CharacterSheetGear.TryParseItemType("", out _));
        Assert.False(CharacterSheetGear.TryParseItemType("chapeau", out _));
        Assert.True(CharacterSheetGear.TypeFits(EquipmentSlotKind.Weapon, ItemType.Weapon));
        Assert.False(CharacterSheetGear.TypeFits(EquipmentSlotKind.Weapon, ItemType.Armor));
        Assert.False(CharacterSheetGear.TypeFits(EquipmentSlotKind.None, ItemType.Weapon));
    }

    [Fact]
    public void PublishedCatalogTypes_LabelBagRows_AndOnlyEquipWeaponOrArmor()
    {
        var weaponId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000002");
        var armorId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000003");
        var potionId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000001");
        var catalog = new (Guid Id, string Name, string Type)[]
        {
            (weaponId, "Épée courte", ItemType.Weapon.ToString()),
            (armorId, "Tunique", ItemType.Armor.ToString()),
            (potionId, "Potion", ItemType.Consumable.ToString()),
        };

        ItemType? Resolve(Guid id)
        {
            foreach (var row in catalog)
            {
                if (row.Id == id && CharacterSheetGear.TryParseItemType(row.Type, out var type))
                {
                    return type;
                }
            }

            return null;
        }

        Assert.Equal(ItemType.Weapon, Resolve(weaponId));
        Assert.Equal(ItemType.Armor, Resolve(armorId));
        Assert.Equal(
            "[0] Épée courte · Arme ×1",
            CharacterSheetGear.FormatBagRow(0, catalog[0].Name, 1, Resolve(weaponId)));
        Assert.Equal(
            "[1] Tunique · Armure ×1",
            CharacterSheetGear.FormatBagRow(1, catalog[1].Name, 1, Resolve(armorId)));
        Assert.Equal(
            "[2] Potion ×3",
            CharacterSheetGear.FormatBagRow(2, catalog[2].Name, 3, Resolve(potionId)));
        Assert.True(CharacterSheetGear.IsEquippable(Resolve(weaponId)));
        Assert.True(CharacterSheetGear.IsEquippable(Resolve(armorId)));
        Assert.False(CharacterSheetGear.IsEquippable(Resolve(potionId)));
        Assert.False(CharacterSheetGear.IsEquippable(null));

        var bag = new List<EquipBagEntry>
        {
            new(2, Resolve(potionId)),
            new(0, Resolve(weaponId)),
            new(1, Resolve(armorId)),
        };

        var weapon = CharacterSheetGear.FromBagEquip(0);
        Assert.Equal(CharacterSheetGearAction.Equip, weapon.Action);
        Assert.Equal((byte)0, weapon.InventorySlot);
        Assert.Equal(EquipmentSlotKind.Weapon, CharacterSheetGear.ServerSlot(PaperdollLayer.Weapon));

        var armor = CharacterSheetGear.FromSlotClick(PaperdollLayer.Armor, occupied: false, selectedSlot: null, bag);
        Assert.Equal(CharacterSheetGearAction.Equip, armor.Action);
        Assert.Equal((byte)1, armor.InventorySlot);
        Assert.Equal(EquipmentSlotKind.Armor, CharacterSheetGear.ServerSlot(PaperdollLayer.Armor));

        var blocked = CharacterSheetGear.FromSlotClick(PaperdollLayer.Weapon, occupied: false, selectedSlot: 2, bag);
        Assert.Equal(CharacterSheetGearAction.None, blocked.Action);

        var unequip = CharacterSheetGear.FromSlotClick(PaperdollLayer.Armor, occupied: true, selectedSlot: null, bag: []);
        Assert.Equal(CharacterSheetGearAction.Unequip, unequip.Action);
        Assert.Equal(EquipmentSlotKind.Armor, unequip.UnequipSlot);
    }
}
