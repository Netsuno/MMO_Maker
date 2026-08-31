namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class EconomyRequestIdEntity
{
    public Guid CharacterId { get; set; }

    public string Operation { get; set; } = string.Empty;

    public Guid RequestId { get; set; }

    public byte[] RequestFingerprint { get; set; } = [];

    public string ResultJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
