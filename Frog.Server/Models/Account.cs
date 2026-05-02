namespace Frog.Server.Models;

public sealed class Account
{
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public required string PasswordSalt { get; init; }
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
}
