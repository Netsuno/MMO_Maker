namespace Frog.Application.Gameplay;

public interface ITradeHoldQuery
{
    bool CanRemoveFromSlot(Guid characterId, int slotIndex, int quantity, int slotQuantity);

    bool CanSpendGold(Guid characterId, int amount, int currentGold);

    bool CanSpendItem(Guid characterId, Guid itemId, int quantity, InventorySnapshot inventory);
}

public sealed class NullTradeHoldQuery : ITradeHoldQuery
{
    public static NullTradeHoldQuery Instance { get; } = new();

    public bool CanRemoveFromSlot(Guid characterId, int slotIndex, int quantity, int slotQuantity)
        => slotQuantity >= quantity && quantity > 0;

    public bool CanSpendGold(Guid characterId, int amount, int currentGold) => currentGold >= amount;

    public bool CanSpendItem(Guid characterId, Guid itemId, int quantity, InventorySnapshot inventory) => true;
}
