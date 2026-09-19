using System.Collections.Concurrent;
using Frog.Application.Gameplay;

namespace Frog.Server.Trade;

public sealed class TradeHoldRegistry : ITradeHoldQuery
{
    private readonly ConcurrentDictionary<Guid, CharacterHold> _holds = new();

    public void Replace(Guid characterId, Guid tradeId, int gold, IReadOnlyDictionary<int, int> slotQuantities)
    {
        _holds[characterId] = new CharacterHold(tradeId, gold, new Dictionary<int, int>(slotQuantities));
    }

    public void Release(Guid characterId, Guid? tradeId = null)
    {
        if (_holds.TryGetValue(characterId, out var hold)
            && (tradeId is null || hold.TradeId == tradeId))
        {
            _holds.TryRemove(characterId, out _);
        }
    }

    public int ReservedInSlot(Guid characterId, int slotIndex)
        => _holds.TryGetValue(characterId, out var hold)
            && hold.SlotQuantities.TryGetValue(slotIndex, out var qty)
            ? qty
            : 0;

    public int ReservedGold(Guid characterId)
        => _holds.TryGetValue(characterId, out var hold) ? hold.Gold : 0;

    public bool CanRemoveFromSlot(Guid characterId, int slotIndex, int quantity, int slotQuantity)
    {
        if (quantity <= 0 || slotQuantity < quantity)
        {
            return false;
        }

        return slotQuantity - ReservedInSlot(characterId, slotIndex) >= quantity;
    }

    public bool CanSpendGold(Guid characterId, int amount, int currentGold)
        => amount >= 0 && currentGold - ReservedGold(characterId) >= amount;

    public bool CanSpendItem(Guid characterId, Guid itemId, int quantity, InventorySnapshot inventory)
    {
        var available = 0;
        foreach (var slot in inventory.Slots)
        {
            if (slot.ItemId != itemId || slot.Quantity <= 0)
            {
                continue;
            }

            available += Math.Max(0, slot.Quantity - ReservedInSlot(characterId, slot.SlotIndex));
        }

        return available >= quantity;
    }

    private sealed record CharacterHold(Guid TradeId, int Gold, Dictionary<int, int> SlotQuantities);
}
