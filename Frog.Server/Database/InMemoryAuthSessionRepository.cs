using System.Collections.Concurrent;
using System.Security.Cryptography;
using Frog.Application.Identity;

namespace Frog.Server.Database;

/// <summary>Sessions auth en mémoire (playtest / sans PostgreSQL).</summary>
public sealed class InMemoryAuthSessionRepository : IAuthSessionRepository
{
    private readonly ConcurrentDictionary<string, (AuthSessionRecord Session, string Token)> _byToken = new();
    private readonly ConcurrentDictionary<Guid, string> _tokenBySessionId = new();

    public Task<AuthSessionIssueResult> IssueAsync(
        Guid accountId,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var token = PostgresAuthSessionRepositoryStub.GenerateToken();
        var session = new AuthSessionRecord(
            Guid.NewGuid(),
            accountId,
            now,
            now.Add(lifetime),
            null,
            now);
        _byToken[token] = (session, token);
        _tokenBySessionId[session.Id] = token;
        return Task.FromResult(new AuthSessionIssueResult(AuthSessionIssueStatus.Issued, token, session));
    }

    public Task<AuthSessionValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!_byToken.TryGetValue(token, out var entry))
        {
            return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.NotFound));
        }

        if (entry.Session.RevokedAtUtc is not null)
        {
            return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.Revoked));
        }

        if (entry.Session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.Expired));
        }

        return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.Valid, entry.Session));
    }

    public Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (!_tokenBySessionId.TryGetValue(sessionId, out var token)
            || !_byToken.TryGetValue(token, out var entry))
        {
            return Task.FromResult(false);
        }

        if (entry.Session.RevokedAtUtc is not null)
        {
            return Task.FromResult(false);
        }

        var revoked = entry.Session with { RevokedAtUtc = DateTimeOffset.UtcNow };
        _byToken[token] = (revoked, token);
        return Task.FromResult(true);
    }

    public Task<bool> RevokeAllForAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var any = false;
        foreach (var pair in _byToken)
        {
            if (pair.Value.Session.AccountId != accountId || pair.Value.Session.RevokedAtUtc is not null)
            {
                continue;
            }

            any = true;
            _byToken[pair.Key] = (pair.Value.Session with { RevokedAtUtc = DateTimeOffset.UtcNow }, pair.Key);
        }

        return Task.FromResult(any);
    }

    public Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (_tokenBySessionId.TryGetValue(sessionId, out var token)
            && _byToken.TryGetValue(token, out var entry)
            && entry.Session.RevokedAtUtc is null)
        {
            _byToken[token] = (entry.Session with { LastSeenAtUtc = DateTimeOffset.UtcNow }, token);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Évite une référence serveur → Persistence pour la génération de jeton en mémoire.</summary>
internal static class PostgresAuthSessionRepositoryStub
{
    internal static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
