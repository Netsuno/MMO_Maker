using Frog.Core.Gameplay;

namespace Frog.Application.Gameplay;

public static class TradeInventoryMath
{
    public static InventorySlotRecord[] CopySlots(InventorySnapshot snapshot)
    {
        var slots = new InventorySlotRecord[GameplayLimits.InventorySlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlotRecord(i, null, 0);
        }

        foreach (var slot in snapshot.Slots)
        {
            if (slot.SlotIndex is >= 0 and < GameplayLimits.InventorySlotCount)
            {
                slots[slot.SlotIndex] = new InventorySlotRecord(slot.SlotIndex, slot.ItemId, slot.Quantity);
            }
        }

        return slots;
    }

    public static bool TryRemoveByItemId(InventorySlotRecord[] slots, Guid itemId, int quantity)
    {
        if (quantity <= 0 || itemId == Guid.Empty)
        {
            return false;
        }

        var remaining = quantity;
        for (var i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].ItemId != itemId || slots[i].Quantity <= 0)
            {
                continue;
            }

            var take = Math.Min(slots[i].Quantity, remaining);
            var left = slots[i].Quantity - take;
            slots[i] = left == 0
                ? new InventorySlotRecord(i, null, 0)
                : new InventorySlotRecord(i, itemId, left);
            remaining -= take;
        }

        return remaining == 0;
    }

    public static bool TryAddItem(InventorySlotRecord[] slots, Guid itemId, int quantity, int maxStack)
    {
        if (quantity <= 0 || itemId == Guid.Empty || maxStack < 1)
        {
            return false;
        }

        var remaining = quantity;
        for (var i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].ItemId == itemId && slots[i].Quantity < maxStack)
            {
                var can = Math.Min(maxStack - slots[i].Quantity, remaining);
                slots[i] = new InventorySlotRecord(i, itemId, slots[i].Quantity + can);
                remaining -= can;
            }
        }

        for (var i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].ItemId is null)
            {
                var can = Math.Min(maxStack, remaining);
                slots[i] = new InventorySlotRecord(i, itemId, can);
                remaining -= can;
            }
        }

        return remaining == 0;
    }

    public static bool TryAllocateSlots(
        InventorySnapshot inventory,
        IReadOnlyList<TradeStackOffer> items,
        out Dictionary<int, int> slotHolds)
    {
        slotHolds = new Dictionary<int, int>();
        var remaining = items
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var slot in inventory.Slots.OrderBy(s => s.SlotIndex))
        {
            if (slot.ItemId is not Guid itemId || slot.Quantity <= 0)
            {
                continue;
            }

            if (!remaining.TryGetValue(itemId, out var need) || need <= 0)
            {
                continue;
            }

            var take = Math.Min(slot.Quantity, need);
            slotHolds[slot.SlotIndex] = take;
            remaining[itemId] = need - take;
        }

        return remaining.Values.All(v => v <= 0);
    }
}
