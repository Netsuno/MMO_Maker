using Frog.Application.Identity;
using Frog.Core.Security;
using Frog.Server.Observability;
using Frog.Server.Security;

namespace Frog.Server.Services;

public sealed class AuthService
{
    private readonly IAccountRepository _accountRepository;
    private readonly AuthRateLimiter _rateLimiter;
    private readonly ServerOpsMetrics? _opsMetrics;

    public AuthService(
        IAccountRepository accountRepository,
        AuthRateLimiter rateLimiter,
        ServerOpsMetrics? opsMetrics = null)
    {
        _accountRepository = accountRepository;
        _rateLimiter = rateLimiter;
        _opsMetrics = opsMetrics;
    }

    public async Task<(bool Success, AccountRecord? Account, bool RateLimited)> TryAuthenticateAsync(
        string username,
        string password,
        string remoteEndPoint,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryAllow(remoteEndPoint, username))
        {
            _opsMetrics?.RecordRateLimitHit("login");
            return (false, null, true);
        }

        if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidLoginPassword(password))
        {
            PasswordHasher.VerifyOrTimingSafeReject(password, null, null);
            _rateLimiter.RegisterFailure(remoteEndPoint, username);
            return (false, null, false);
        }

        var account = await _accountRepository.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        var ok = VerifyStoredPassword(password, account?.PasswordHash);
        if (!ok)
        {
            _rateLimiter.RegisterFailure(remoteEndPoint, username);
            return (false, null, false);
        }

        _rateLimiter.RegisterSuccess(remoteEndPoint, username);
        return (true, account, false);
    }

    public Task<AccountCreateResult> RegisterAccountAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
        => RegisterAccountAsync(username, password, remoteEndPoint: AuthRateLimitKey.Unknown, cancellationToken);

    public async Task<AccountCreateResult> RegisterAccountAsync(
        string username,
        string password,
        string remoteEndPoint,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryAllow(remoteEndPoint, username))
        {
            _opsMetrics?.RecordRateLimitHit("login");
            return new AccountCreateResult(AccountCreateStatus.RateLimited);
        }

        if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidPassword(password))
        {
            _rateLimiter.RegisterFailure(remoteEndPoint, username);
            return new AccountCreateResult(AccountCreateStatus.InvalidInput);
        }

        var created = await _accountRepository.TryCreateAsync(username, password, cancellationToken)
            .ConfigureAwait(false);
        if (created.Status != AccountCreateStatus.Created)
        {
            _rateLimiter.RegisterFailure(remoteEndPoint, username);
            return created;
        }

        _rateLimiter.RegisterSuccess(remoteEndPoint, username);
        return created;
    }

    public void RegisterAuthFailure(string remoteEndPoint, string? username)
        => _rateLimiter.RegisterFailure(remoteEndPoint, username);

    public bool TryAllowAuth(string remoteEndPoint, string? username, string metricKind)
    {
        var allowed = _rateLimiter.TryAllow(remoteEndPoint, username);
        if (!allowed)
        {
            _opsMetrics?.RecordRateLimitHit(metricKind);
        }

        return allowed;
    }

    public bool TryAllowReconnect(string remoteEndPoint, string? username = null)
    {
        var allowed = _rateLimiter.TryAllow(remoteEndPoint, username);
        if (!allowed)
        {
            _opsMetrics?.RecordRateLimitHit("reconnect");
        }

        return allowed;
    }

    public void RegisterReconnectFailure(string remoteEndPoint, string? username = null)
        => _rateLimiter.RegisterFailure(remoteEndPoint, username);

    public void RegisterReconnectSuccess(string remoteEndPoint, string? username = null)
        => _rateLimiter.RegisterSuccess(remoteEndPoint, username);

    private static bool VerifyStoredPassword(string password, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return PasswordHasher.VerifyOrTimingSafeReject(password, null, null);
        }

        if (stored.Contains('|', StringComparison.Ordinal))
        {
            var parts = stored.Split('|', 2);
            return PasswordHasher.VerifyOrTimingSafeReject(password, parts[0], parts[1]);
        }

        return PasswordHasher.VerifyOrTimingSafeReject(password, stored, null);
    }
}
