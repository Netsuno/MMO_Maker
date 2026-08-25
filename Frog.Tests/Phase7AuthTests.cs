using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Identity;
using Frog.Core.Security;
using Frog.Server.Database;
using Frog.Server.Network;
using Frog.Server.Security;
using Frog.Server.Services;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7AuthTests
{
    [Fact]
    public void PasswordHasher_RoundTrip_AndLegacyCompat()
    {
        var hash = PasswordHasher.HashPassword("secret-pass");
        Assert.StartsWith("$frog-v1$pbkdf2-sha256$", hash, StringComparison.Ordinal);
        Assert.True(PasswordHasher.VerifyPassword("secret-pass", hash));
        Assert.False(PasswordHasher.VerifyPassword("wrong", hash));
    }

    [Fact]
    public void PasswordHasher_TimingSafeReject_WhenAccountMissing()
    {
        Assert.False(PasswordHasher.VerifyOrTimingSafeReject("probe", null, null));
    }

    [Fact]
    public async Task AuthService_RejectsDuplicateAccount()
    {
        var repo = new InMemoryAccountRepository();
        var auth = new AuthService(repo, new LoginRateLimiter());
        var first = await auth.RegisterAccountAsync("player-one", "password123");
        var second = await auth.RegisterAccountAsync("player-one", "password123");
        Assert.Equal(AccountCreateStatus.Created, first.Status);
        Assert.Equal(AccountCreateStatus.DuplicateUsername, second.Status);
    }

    [Fact]
    public async Task AuthService_RejectsInvalidInput()
    {
        var repo = new InMemoryAccountRepository();
        var auth = new AuthService(repo, new LoginRateLimiter());
        var shortPassword = await auth.RegisterAccountAsync("valid-user", "short");
        var badUser = await auth.RegisterAccountAsync("x", "password123");
        Assert.Equal(AccountCreateStatus.InvalidInput, shortPassword.Status);
        Assert.Equal(AccountCreateStatus.InvalidInput, badUser.Status);
    }

    [Fact]
    public async Task AuthService_GenericFailure_DoesNotRevealMissingAccount()
    {
        var repo = new InMemoryAccountRepository();
        var auth = new AuthService(repo, new LoginRateLimiter(maxFailures: 20));
        var missing = await auth.TryAuthenticateAsync("ghost-user", "password123", "k1");
        var wrong = await auth.TryAuthenticateAsync("demo", "wrong-pass", "k2");
        Assert.False(missing.Success);
        Assert.False(wrong.Success);
    }

    [Fact]
    public async Task AuthSession_RevokedAndExpiredTokensRejected()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var repo = new TestAuthSessionRepository(clock);
        var accountId = Guid.NewGuid();
        var issued = await repo.IssueAsync(accountId, TimeSpan.FromHours(1));
        Assert.Equal(AuthSessionIssueStatus.Issued, issued.Status);
        Assert.False(string.IsNullOrEmpty(issued.Token));

        var valid = await repo.ValidateTokenAsync(issued.Token!);
        Assert.Equal(AuthSessionValidationStatus.Valid, valid.Status);

        Assert.True(await repo.RevokeAsync(issued.Session!.Id));
        var revoked = await repo.ValidateTokenAsync(issued.Token!);
        Assert.Equal(AuthSessionValidationStatus.Revoked, revoked.Status);

        var issued2 = await repo.IssueAsync(accountId, TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(5));
        var expired = await repo.ValidateTokenAsync(issued2.Token!);
        Assert.Equal(AuthSessionValidationStatus.Expired, expired.Status);
    }

    [Fact]
    public void LoginRateLimiter_BlocksAfterFailures()
    {
        var limiter = new LoginRateLimiter(maxFailures: 3, window: TimeSpan.FromMinutes(5));
        const string key = "127.0.0.1";
        Assert.True(limiter.TryAllow(key));
        limiter.RegisterFailure(key);
        limiter.RegisterFailure(key);
        limiter.RegisterFailure(key);
        Assert.False(limiter.TryAllow(key));
        limiter.RegisterSuccess(key);
        Assert.True(limiter.TryAllow(key));
    }

    [Fact]
    public void AuthLogReasons_AreGenericNotCredentialSpecific()
    {
        Assert.DoesNotContain("password", "invalid_credentials", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", "duplicate_or_invalid", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseLoginPayload_RejectsOversizedFields()
    {
        var hugeUser = new byte[1 + 64 + 1 + 8];
        hugeUser[0] = 64;
        for (var i = 0; i < 64; i++)
        {
            hugeUser[1 + i] = (byte)'a';
        }

        hugeUser[65] = 8;
        Assert.False(PacketDispatcher.TryParseLoginPayload(hugeUser, out _, out _));
    }

    [Fact]
    public void TryParseReconnectPayload_ValidatesLength()
    {
        Assert.False(PacketDispatcher.TryParseReconnectPayload(ReadOnlySpan<byte>.Empty, out _));
        var token = Encoding.UTF8.GetBytes("opaque-token-value");
        var payload = new byte[2 + token.Length];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 2), (ushort)token.Length);
        token.CopyTo(payload.AsSpan(2));
        Assert.True(PacketDispatcher.TryParseReconnectPayload(payload, out var parsed));
        Assert.Equal("opaque-token-value", parsed);
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class TestAuthSessionRepository(TimeProvider clock) : IAuthSessionRepository
    {
        private readonly Dictionary<string, AuthSessionRecord> _sessions = new();

        public Task<AuthSessionIssueResult> IssueAsync(Guid accountId, TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            var now = clock.GetUtcNow();
            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var session = new AuthSessionRecord(Guid.NewGuid(), accountId, now, now.Add(lifetime), null, now);
            _sessions[token] = session;
            return Task.FromResult(new AuthSessionIssueResult(AuthSessionIssueStatus.Issued, token, session));
        }

        public Task<AuthSessionValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryGetValue(token, out var session))
            {
                return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.NotFound));
            }

            if (session.RevokedAtUtc is not null)
            {
                return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.Revoked));
            }

            if (session.ExpiresAtUtc <= clock.GetUtcNow())
            {
                return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.Expired));
            }

            return Task.FromResult(new AuthSessionValidationResult(AuthSessionValidationStatus.Valid, session));
        }

        public Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            foreach (var pair in _sessions)
            {
                if (pair.Value.Id != sessionId || pair.Value.RevokedAtUtc is not null)
                {
                    continue;
                }

                _sessions[pair.Key] = pair.Value with { RevokedAtUtc = clock.GetUtcNow() };
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> RevokeAllForAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
