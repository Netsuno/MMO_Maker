namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class EconomyRequestIdEntity
{
    public Guid RequestId { get; set; }

    public Guid CharacterId { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string ResultJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
