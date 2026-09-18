namespace Frog.Persistence.PostgreSql.Entities.Auth;

/// <summary>Privilège opérateur (GM) — table <c>auth.operators</c>, 1:1 avec <c>auth.accounts</c>.</summary>
public sealed class OperatorEntity
{
    public Guid AccountId { get; set; }

    public AccountEntity Account { get; set; } = null!;

    public DateTimeOffset GrantedAtUtc { get; set; }

    public string GrantedBy { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
