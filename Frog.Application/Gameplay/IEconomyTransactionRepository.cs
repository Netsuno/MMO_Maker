namespace Frog.Application.Gameplay;

public sealed record EconomyCommittedState(
    int Gold,
    int BankGold,
    InventorySnapshot Inventory,
    BankSnapshot Bank);

public sealed record EconomyBuyResult(
    bool Success,
    string Message,
    EconomyCommittedState? State = null,
    bool IdempotentReplay = false)
{
    public static EconomyBuyResult Ok(EconomyCommittedState state, bool idempotentReplay = false)
        => new(true, "Achat reussi.", state, idempotentReplay);

    public static EconomyBuyResult Fail(string message)
        => new(false, message);
}

public sealed record EconomySellResult(
    bool Success,
    string Message,
    EconomyCommittedState? State = null,
    bool IdempotentReplay = false)
{
    public static EconomySellResult Ok(EconomyCommittedState state, bool idempotentReplay = false)
        => new(true, "Vente reussie.", state, idempotentReplay);

    public static EconomySellResult Fail(string message)
        => new(false, message);
}

public sealed record EconomyBankItemResult(
    bool Success,
    string Message,
    EconomyCommittedState? State = null,
    bool IdempotentReplay = false)
{
    public static EconomyBankItemResult Ok(EconomyCommittedState state, bool idempotentReplay = false)
        => new(true, "Operation reussie.", state, idempotentReplay);

    public static EconomyBankItemResult Fail(string message)
        => new(false, message);
}

public sealed record EconomyBankGoldResult(
    bool Success,
    string Message,
    EconomyCommittedState? State = null,
    bool IdempotentReplay = false)
{
    public static EconomyBankGoldResult Ok(EconomyCommittedState state, bool idempotentReplay = false)
        => new(true, "Operation reussie.", state, idempotentReplay);

    public static EconomyBankGoldResult Fail(string message)
        => new(false, message);
}

/// <summary>Opérations économie atomiques (inventaire, banque, or, stock boutique).</summary>
public interface IEconomyTransactionRepository
{
    Task<EconomyBuyResult> TryBuyAsync(
        Guid characterId,
        Guid shopId,
        Guid itemId,
        int quantity,
        int unitPrice,
        int maxStack,
        int? publishedStockLimit,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<EconomySellResult> TrySellAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int unitSellPrice,
        int maxStack,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<EconomyBankItemResult> TryBankDepositItemAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int maxStack,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<EconomyBankItemResult> TryBankWithdrawItemAsync(
        Guid characterId,
        int bankSlotIndex,
        int quantity,
        int maxStack,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<EconomyBankGoldResult> TryBankDepositGoldAsync(
        Guid characterId,
        int amount,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<EconomyBankGoldResult> TryBankWithdrawGoldAsync(
        Guid characterId,
        int amount,
        Guid requestId,
        CancellationToken cancellationToken = default);
}
