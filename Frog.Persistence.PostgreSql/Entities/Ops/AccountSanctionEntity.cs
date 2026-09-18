using Frog.Persistence.PostgreSql.Entities.Auth;

namespace Frog.Persistence.PostgreSql.Entities.Ops;

/// <summary>État mute/ban — table <c>ops.account_sanctions</c>.</summary>
public sealed class AccountSanctionEntity
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public AccountEntity Account { get; set; } = null!;

    public string Kind { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public Guid ActorAccountId { get; set; }

    public AccountEntity ActorAccount { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
