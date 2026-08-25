namespace Frog.Persistence.PostgreSql.Entities.Auth;

public sealed class AuthSessionEntity
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public AccountEntity Account { get; set; } = null!;

    /// <summary>SHA-256 du jeton opaque (jamais le jeton en clair).</summary>
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
