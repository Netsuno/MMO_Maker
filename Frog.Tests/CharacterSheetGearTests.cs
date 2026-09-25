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
}
