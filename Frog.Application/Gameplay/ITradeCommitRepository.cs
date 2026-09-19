namespace Frog.Application.Gameplay;

public sealed record TradeStackOffer(Guid ItemId, int Quantity);

public sealed record TradeCommitPartyState(Guid CharacterId, int Gold, InventorySnapshot Inventory);

public sealed record TradeExecutionRecord(
    Guid TradeId,
    Guid InitiatorId,
    Guid PartnerId,
    Guid CommitRequestId,
    string ContentsJson,
    DateTimeOffset CommittedAtUtc);

public sealed record TradeCommitResult(
    bool Success,
    string Message,
    bool IdempotentReplay = false,
    TradeCommitPartyState? Initiator = null,
    TradeCommitPartyState? Partner = null)
{
    public static TradeCommitResult Ok(
        TradeCommitPartyState initiator,
        TradeCommitPartyState partner,
        bool idempotentReplay = false)
        => new(true, idempotentReplay ? "Echange deja valide." : "Echange valide.", idempotentReplay, initiator, partner);

    public static TradeCommitResult Fail(string message) => new(false, message);
}

public interface ITradeCommitRepository
{
    Task<TradeCommitResult> TryCommitAsync(
        Guid tradeId,
        Guid requestId,
        Guid initiatorId,
        Guid partnerId,
        int initiatorGold,
        int partnerGold,
        IReadOnlyList<TradeStackOffer> initiatorItems,
        IReadOnlyList<TradeStackOffer> partnerItems,
        CancellationToken cancellationToken = default);

    Task<TradeCommitResult?> TryReplayAsync(
        Guid characterId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<TradeExecutionRecord?> FindExecutionAsync(Guid tradeId, CancellationToken cancellationToken = default);
}
