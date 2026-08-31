using System.Security.Cryptography;
using System.Text;
using Frog.Application.Identity;
using Frog.Persistence.PostgreSql.Entities.Auth;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Auth;

public sealed class PostgresAuthSessionRepository : IAuthSessionRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresAuthSessionRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<AuthSessionIssueResult> IssueAsync(
        Guid accountId,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var accountExists = await db.AuthAccounts
                .AnyAsync(a => a.Id == accountId, ct)
                .ConfigureAwait(false);
            if (!accountExists)
            {
                return new AuthSessionIssueResult(AuthSessionIssueStatus.AccountNotFound);
            }

            var token = GenerateToken();
            var now = _clock.GetUtcNow();
            var entity = new AuthSessionEntity
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                TokenHash = HashToken(token),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(lifetime),
                LastSeenAtUtc = now,
            };

            db.AuthSessions.Add(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new AuthSessionIssueResult(
                AuthSessionIssueStatus.Issued,
                token,
                ToRecord(entity));
        }, cancellationToken);

    public Task<AuthSessionValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthSessionValidationResult(AuthSessionValidationStatus.NotFound);
            }

            var hash = HashToken(token);
            var entity = await db.AuthSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TokenHash == hash, ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return new AuthSessionValidationResult(AuthSessionValidationStatus.NotFound);
            }

            if (entity.RevokedAtUtc is not null)
            {
                return new AuthSessionValidationResult(AuthSessionValidationStatus.Revoked);
            }

            var now = _clock.GetUtcNow();
            if (entity.ExpiresAtUtc <= now)
            {
                return new AuthSessionValidationResult(AuthSessionValidationStatus.Expired);
            }

            return new AuthSessionValidationResult(AuthSessionValidationStatus.Valid, ToRecord(entity));
        }, cancellationToken);

    public Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.AuthSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct).ConfigureAwait(false);
            if (entity is null || entity.RevokedAtUtc is not null)
            {
                return false;
            }

            entity.RevokedAtUtc = _clock.GetUtcNow();
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task<bool> RevokeAllForAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            var sessions = await db.AuthSessions
                .Where(s => s.AccountId == accountId && s.RevokedAtUtc == null)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (sessions.Count == 0)
            {
                return false;
            }

            foreach (var session in sessions)
            {
                session.RevokedAtUtc = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.AuthSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct).ConfigureAwait(false);
            if (entity is null || entity.RevokedAtUtc is not null)
            {
                return;
            }

            entity.LastSeenAtUtc = _clock.GetUtcNow();
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken);

    internal static byte[] HashToken(string token)
        => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static AuthSessionRecord ToRecord(AuthSessionEntity entity)
        => new(
            entity.Id,
            entity.AccountId,
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc,
            entity.RevokedAtUtc,
            entity.LastSeenAtUtc);
}
