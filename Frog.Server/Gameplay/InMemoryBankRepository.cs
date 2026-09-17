using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

public sealed class InMemoryBankRepository : IBankRepository
{
    private readonly ConcurrentDictionary<Guid, BankSlotRecord[]> _slots = new();

    public Task<BankSnapshot> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var slots = _slots.GetOrAdd(characterId, static _ => CreateEmpty());
        return Task.FromResult(new BankSnapshot(characterId, slots.ToArray()));
    }

    public Task<BankMutationResult> DepositItemAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        int maxStack,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || maxStack < 1)
        {
            return Task.FromResult(new BankMutationResult(BankMutationStatus.InvalidQuantity));
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
                    slots[i] = new BankSlotRecord(i, itemId, slots[i].Quantity + can);
                    remaining -= can;
                }
            }

            for (var i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].ItemId is null)
                {
                    var can = Math.Min(maxStack, remaining);
                    slots[i] = new BankSlotRecord(i, itemId, can);
                    remaining -= can;
                }
            }

            if (remaining > 0)
            {
                return Task.FromResult(new BankMutationResult(
                    BankMutationStatus.Full,
                    new BankSnapshot(characterId, slots.ToArray()),
                    ErrorMessage: "Banque pleine."));
            }

            return Task.FromResult(new BankMutationResult(
                BankMutationStatus.Ok,
                new BankSnapshot(characterId, slots.ToArray())));
        }
    }

    public Task<BankMutationResult> WithdrawItemAsync(
        Guid characterId,
        int slotIndex,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || slotIndex < 0 || slotIndex >= GameplayLimits.BankSlotCount)
        {
            return Task.FromResult(new BankMutationResult(BankMutationStatus.InvalidSlot));
        }

        var slots = _slots.GetOrAdd(characterId, static _ => CreateEmpty());
        lock (slots)
        {
            var slot = slots[slotIndex];
            if (slot.ItemId is null || slot.Quantity < quantity)
            {
                return Task.FromResult(new BankMutationResult(BankMutationStatus.InvalidQuantity));
            }

            var left = slot.Quantity - quantity;
            slots[slotIndex] = left == 0
                ? new BankSlotRecord(slotIndex, null, 0)
                : new BankSlotRecord(slotIndex, slot.ItemId, left);
            return Task.FromResult(new BankMutationResult(
                BankMutationStatus.Ok,
                new BankSnapshot(characterId, slots.ToArray())));
        }
    }

    private static BankSlotRecord[] CreateEmpty()
    {
        var slots = new BankSlotRecord[GameplayLimits.BankSlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new BankSlotRecord(i, null, 0);
        }

        return slots;
    }
}
