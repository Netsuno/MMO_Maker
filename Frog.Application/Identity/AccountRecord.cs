namespace Frog.Application.Identity;

/// <summary>Compte joueur persisté (identité + empreinte mot de passe).</summary>
public sealed record AccountRecord(
    Guid Id,
    string Username,
    string PasswordHash,
    DateTimeOffset CreatedAtUtc);
