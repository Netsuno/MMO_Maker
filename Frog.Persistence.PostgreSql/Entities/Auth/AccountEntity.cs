namespace Frog.Persistence.PostgreSql.Entities.Auth;

public sealed class AccountEntity
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<AuthSessionEntity> Sessions { get; set; } = new List<AuthSessionEntity>();
}
