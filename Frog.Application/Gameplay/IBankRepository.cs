namespace Frog.Application.Gameplay;

public sealed record BankSlotRecord(int SlotIndex, Guid? ItemId, int Quantity);

public sealed record BankSnapshot(Guid CharacterId, IReadOnlyList<BankSlotRecord> Slots);

public enum BankMutationStatus
{
    Ok,
    Full,
    InvalidQuantity,
    InvalidSlot,
    InsufficientFunds,
    CharacterNotFound,
    ItemNotFound,
}

public sealed record BankMutationResult(
    BankMutationStatus Status,
    BankSnapshot? Snapshot = null,
    int? NewGold = null,
    string? ErrorMessage = null);

public interface IBankRepository
{
    Task<BankSnapshot> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<BankMutationResult> DepositItemAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        int maxStack,
        CancellationToken cancellationToken = default);

    Task<BankMutationResult> WithdrawItemAsync(
        Guid characterId,
        int slotIndex,
        int quantity,
        CancellationToken cancellationToken = default);
}
