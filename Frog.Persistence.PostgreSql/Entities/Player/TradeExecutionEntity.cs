namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class TradeExecutionEntity
{
    public Guid TradeId { get; set; }

    public Guid InitiatorCharacterId { get; set; }

    public Guid PartnerCharacterId { get; set; }

    public Guid CommitRequestId { get; set; }

    public string ContentsJson { get; set; } = string.Empty;

    public DateTimeOffset CommittedAtUtc { get; set; }
}
