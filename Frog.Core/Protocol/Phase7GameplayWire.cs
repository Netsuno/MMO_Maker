namespace Frog.Core.Protocol;

public sealed class InventorySlotWire
{
    public int SlotIndex { get; init; }
    public Guid? ItemId { get; init; }
    public int Quantity { get; init; }
}

public sealed class InventorySnapshotWire
{
    public Guid? EquippedWeaponItemId { get; init; }
    public Guid? EquippedArmorItemId { get; init; }
    public IReadOnlyList<InventorySlotWire> Slots { get; init; } = Array.Empty<InventorySlotWire>();
}

public sealed class BankSlotWire
{
    public int SlotIndex { get; init; }
    public Guid? ItemId { get; init; }
    public int Quantity { get; init; }
}

public sealed class BankSnapshotWire
{
    public int BankGold { get; init; }
    public IReadOnlyList<BankSlotWire> Slots { get; init; } = Array.Empty<BankSlotWire>();
}

public sealed class GroundItemWire
{
    public Guid GroundItemId { get; init; }
    public Guid ItemId { get; init; }
    public int Quantity { get; init; }
    public int PixelX { get; init; }
    public int PixelY { get; init; }
}
