using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

public sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly ConcurrentDictionary<Guid, InventorySlotRecord[]> _slots = new();

    public Task<InventorySnapshot> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var slots = _slots.GetOrAdd(characterId, static _ => CreateEmpty());
        return Task.FromResult(new InventorySnapshot(characterId, slots.ToArray()));
    }

    public Task<InventoryMutationResult> TryAddAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        int maxStack,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || maxStack < 1)
        {
            return Task.FromResult(new InventoryMutationResult(
                InventoryMutationStatus.InvalidQuantity,
                ErrorMessage: "Quantite invalide."));
        }

        var slots = _slots.GetOrAdd(characterId, static _ => CreateEmpty());
        lock (slots)
        {
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

            if (remaining > 0)
            {
                return Task.FromResult(new InventoryMutationResult(
                    InventoryMutationStatus.Full,
                    new InventorySnapshot(characterId, slots.ToArray()),
                    "Inventaire plein."));
            }

            return Task.FromResult(new InventoryMutationResult(
                InventoryMutationStatus.Ok,
                new InventorySnapshot(characterId, slots.ToArray())));
        }
    }

    public Task<InventoryMutationResult> TryRemoveAsync(
        Guid characterId,
        int slotIndex,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0
            || slotIndex < 0
            || slotIndex >= GameplayLimits.InventorySlotCount)
        {
            return Task.FromResult(new InventoryMutationResult(InventoryMutationStatus.InvalidSlot));
        }

        var slots = _slots.GetOrAdd(characterId, static _ => CreateEmpty());
        lock (slots)
        {
            var slot = slots[slotIndex];
            if (slot.ItemId is null || slot.Quantity < quantity)
            {
                return Task.FromResult(new InventoryMutationResult(
                    InventoryMutationStatus.InvalidQuantity,
                    ErrorMessage: "Quantite insuffisante."));
            }

            var left = slot.Quantity - quantity;
            slots[slotIndex] = left == 0
                ? new InventorySlotRecord(slotIndex, null, 0)
                : new InventorySlotRecord(slotIndex, slot.ItemId, left);
            return Task.FromResult(new InventoryMutationResult(
                InventoryMutationStatus.Ok,
                new InventorySnapshot(characterId, slots.ToArray())));
        }
    }

    public Task<InventoryMutationResult> ReplaceAllAsync(
        Guid characterId,
        IReadOnlyList<InventorySlotRecord> slots,
        CancellationToken cancellationToken = default)
    {
        var arr = CreateEmpty();
        foreach (var s in slots)
        {
            if (s.SlotIndex is >= 0 and < GameplayLimits.InventorySlotCount)
            {
                arr[s.SlotIndex] = new InventorySlotRecord(s.SlotIndex, s.ItemId, s.Quantity);
            }
        }

        _slots[characterId] = arr;
        return Task.FromResult(new InventoryMutationResult(
            InventoryMutationStatus.Ok,
            new InventorySnapshot(characterId, arr.ToArray())));
    }

    private static InventorySlotRecord[] CreateEmpty()
    {
        var slots = new InventorySlotRecord[GameplayLimits.InventorySlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlotRecord(i, null, 0);
        }

        return slots;
    }
}
