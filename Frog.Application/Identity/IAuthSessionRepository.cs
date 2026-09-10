namespace Frog.Application.Identity;

/// <summary>Jeton opaque côté client ; seul le hachage est persisté.</summary>
public sealed record AuthSessionRecord(
    Guid Id,
    Guid AccountId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset LastSeenAtUtc);

public enum AuthSessionIssueStatus
{
    Issued,
    AccountNotFound,
}

public sealed record AuthSessionIssueResult(AuthSessionIssueStatus Status, string? Token = null, AuthSessionRecord? Session = null);

public enum AuthSessionValidationStatus
{
    Valid,
    NotFound,
    Expired,
    Revoked,
}

public sealed record AuthSessionValidationResult(AuthSessionValidationStatus Status, AuthSessionRecord? Session = null);

public interface IAuthSessionRepository
{
    Task<AuthSessionIssueResult> IssueAsync(
        Guid accountId,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task<AuthSessionValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<bool> RevokeAllForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
