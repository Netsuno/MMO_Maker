namespace Frog.Application.Gameplay;

public sealed record InventorySlotRecord(int SlotIndex, Guid? ItemId, int Quantity);

public sealed record InventorySnapshot(Guid CharacterId, IReadOnlyList<InventorySlotRecord> Slots);

public enum InventoryMutationStatus
{
    Ok,
    Full,
    InvalidQuantity,
    InvalidSlot,
    ItemNotFound,
    NotStackable,
    CharacterNotFound,
}

public sealed record InventoryMutationResult(
    InventoryMutationStatus Status,
    InventorySnapshot? Snapshot = null,
    string? ErrorMessage = null);

public interface IInventoryRepository
{
    Task<InventorySnapshot> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<InventoryMutationResult> TryAddAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        int maxStack,
        CancellationToken cancellationToken = default);

    Task<InventoryMutationResult> TryRemoveAsync(
        Guid characterId,
        int slotIndex,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<InventoryMutationResult> ReplaceAllAsync(
        Guid characterId,
        IReadOnlyList<InventorySlotRecord> slots,
        CancellationToken cancellationToken = default);
}
