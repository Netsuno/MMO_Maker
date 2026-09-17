using Frog.Core.Gameplay;

namespace Frog.Application.Gameplay;

public sealed record InventoryTransferPickupResult(
    bool Success,
    string Message,
    InventorySnapshot? Inventory = null,
    Guid? ItemId = null)
{
    public static InventoryTransferPickupResult Ok(InventorySnapshot inventory, Guid itemId)
        => new(true, "Ramasse.", inventory, itemId);

    public static InventoryTransferPickupResult Fail(string message)
        => new(false, message);
}

public sealed record InventoryTransferDropResult(
    bool Success,
    string Message,
    InventorySnapshot? Inventory = null,
    GroundItemRecord? GroundItem = null)
{
    public static InventoryTransferDropResult Ok(InventorySnapshot inventory, GroundItemRecord groundItem)
        => new(true, "Depose.", inventory, groundItem);

    public static InventoryTransferDropResult Fail(string message)
        => new(false, message);
}

public sealed record InventoryTransferEquipResult(
    bool Success,
    string Message,
    InventorySnapshot? Inventory = null,
    EquipmentRecord? Equipment = null)
{
    public static InventoryTransferEquipResult Ok(InventorySnapshot inventory, EquipmentRecord equipment)
        => new(true, "Equipe.", inventory, equipment);

    public static InventoryTransferEquipResult Fail(string message)
        => new(false, message);
}

public sealed record InventoryTransferUnequipResult(
    bool Success,
    string Message,
    InventorySnapshot? Inventory = null,
    EquipmentRecord? Equipment = null)
{
    public static InventoryTransferUnequipResult Ok(InventorySnapshot inventory, EquipmentRecord equipment)
        => new(true, "Desequipe.", inventory, equipment);

    public static InventoryTransferUnequipResult Fail(string message)
        => new(false, message);
}

/// <summary>Transferts inventaire / equipement / sol atomiques (PostgreSQL unit-of-work).</summary>
public interface IInventoryTransferRepository
{
    Task<InventoryTransferPickupResult> TryPickupAsync(
        Guid characterId,
        Guid groundItemId,
        int sessionMapId,
        int sessionPixelX,
        int sessionPixelY,
        int maxPickupDistancePixels,
        CancellationToken cancellationToken = default);

    Task<InventoryTransferDropResult> TryDropAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int sessionMapId,
        int sessionPixelX,
        int sessionPixelY,
        CancellationToken cancellationToken = default);

    Task<InventoryTransferEquipResult> TryEquipAsync(
        Guid characterId,
        int inventorySlotIndex,
        CancellationToken cancellationToken = default);

    Task<InventoryTransferUnequipResult> TryUnequipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        CancellationToken cancellationToken = default);
}
