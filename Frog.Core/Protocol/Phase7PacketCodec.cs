using System.Buffers.Binary;

namespace Frog.Core.Protocol;

/// <summary>Décodage binaire Phase 7 (miroir de <c>Frog.Server.Network.PacketSender</c>).</summary>
public static class Phase7PacketCodec
{
    public static bool TryParseCombatState(ReadOnlySpan<byte> body, out CombatStateWire state)
    {
        state = new CombatStateWire();
        if (body.Length != 4 + 8 + 4 + 4 + 4 + 4 + 4 + 1)
        {
            return false;
        }

        var o = 0;
        var level = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
        o += 4;
        var xp = BinaryPrimitives.ReadInt64LittleEndian(body.Slice(o));
        o += 8;
        var hp = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
        o += 4;
        var maxHp = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
        o += 4;
        var mp = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
        o += 4;
        var maxMp = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
        o += 4;
        var gold = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
        o += 4;
        var isDead = body[o] != 0;
        state = new CombatStateWire
        {
            Level = level,
            Experience = xp,
            Hp = hp,
            MaxHp = maxHp,
            Mp = mp,
            MaxMp = maxMp,
            Gold = gold,
            IsDead = isDead,
        };
        return true;
    }

    public static bool TryParseExperienceGain(ReadOnlySpan<byte> body, out ExperienceGainWire gain)
    {
        gain = new ExperienceGainWire();
        if (body.Length != 8 + 4 + 8)
        {
            return false;
        }

        gain = new ExperienceGainWire
        {
            Amount = BinaryPrimitives.ReadInt64LittleEndian(body),
            Level = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(8)),
            Experience = BinaryPrimitives.ReadInt64LittleEndian(body.Slice(12)),
        };
        return true;
    }

    public static bool TryParseInventorySnapshot(ReadOnlySpan<byte> body, out InventorySnapshotWire snapshot)
    {
        snapshot = new InventorySnapshotWire();
        if (body.Length < 1 + 16 + 16)
        {
            return false;
        }

        var slotCount = body[0];
        var expected = 1 + 16 + 16 + slotCount * (1 + 1 + 16 + 4);
        if (body.Length != expected)
        {
            return false;
        }

        var o = 1;
        var weapon = ReadGuid(body.Slice(o));
        o += 16;
        var armor = ReadGuid(body.Slice(o));
        o += 16;
        var slots = new List<InventorySlotWire>(slotCount);
        for (var i = 0; i < slotCount; i++)
        {
            var slotIndex = body[o++];
            var hasItem = body[o++] != 0;
            var itemId = ReadGuid(body.Slice(o));
            o += 16;
            var qty = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
            o += 4;
            slots.Add(new InventorySlotWire
            {
                SlotIndex = slotIndex,
                ItemId = hasItem && itemId != Guid.Empty && qty > 0 ? itemId : null,
                Quantity = hasItem ? qty : 0,
            });
        }

        snapshot = new InventorySnapshotWire
        {
            EquippedWeaponItemId = weapon == Guid.Empty ? null : weapon,
            EquippedArmorItemId = armor == Guid.Empty ? null : armor,
            Slots = slots,
        };
        return true;
    }

    public static bool TryParseBankSnapshot(ReadOnlySpan<byte> body, out BankSnapshotWire snapshot)
    {
        snapshot = new BankSnapshotWire();
        if (body.Length < 1 + 4)
        {
            return false;
        }

        var slotCount = body[0];
        var bankGold = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(1));
        var expected = 1 + 4 + slotCount * (1 + 1 + 16 + 4);
        if (body.Length != expected)
        {
            return false;
        }

        var o = 5;
        var slots = new List<BankSlotWire>(slotCount);
        for (var i = 0; i < slotCount; i++)
        {
            var slotIndex = body[o++];
            var hasItem = body[o++] != 0;
            var itemId = ReadGuid(body.Slice(o));
            o += 16;
            var qty = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
            o += 4;
            slots.Add(new BankSlotWire
            {
                SlotIndex = slotIndex,
                ItemId = hasItem && itemId != Guid.Empty && qty > 0 ? itemId : null,
                Quantity = hasItem ? qty : 0,
            });
        }

        snapshot = new BankSnapshotWire
        {
            BankGold = bankGold,
            Slots = slots,
        };
        return true;
    }

    public static bool TryParseGroundItemsSnapshot(ReadOnlySpan<byte> body, out GroundItemsSnapshotWire snapshot)
    {
        snapshot = new GroundItemsSnapshotWire();
        if (body.Length < 4 + 2)
        {
            return false;
        }

        var mapId = BinaryPrimitives.ReadInt32LittleEndian(body);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(4));
        var expected = 4 + 2 + count * (16 + 16 + 4 + 4 + 4);
        if (body.Length != expected)
        {
            return false;
        }

        var o = 6;
        var items = new List<GroundItemWire>(count);
        for (var i = 0; i < count; i++)
        {
            var groundId = ReadGuid(body.Slice(o));
            o += 16;
            var itemId = ReadGuid(body.Slice(o));
            o += 16;
            var qty = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
            o += 4;
            var px = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
            o += 4;
            var py = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(o));
            o += 4;
            items.Add(new GroundItemWire
            {
                GroundItemId = groundId,
                ItemId = itemId,
                Quantity = qty,
                PixelX = px,
                PixelY = py,
            });
        }

        snapshot = new GroundItemsSnapshotWire
        {
            MapId = mapId,
            Items = items,
        };
        return true;
    }

    private static Guid ReadGuid(ReadOnlySpan<byte> span) => new(span);
}
