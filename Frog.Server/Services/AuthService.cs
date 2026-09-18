using Frog.Application.Identity;
using Frog.Core.Security;
using Frog.Server.Observability;
using Frog.Server.Security;

namespace Frog.Server.Services;

public sealed class AuthService(
    IAccountRepository accountRepository,
    LoginRateLimiter rateLimiter,
    ServerOpsMetrics? opsMetrics = null)
{
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly LoginRateLimiter _rateLimiter = rateLimiter;
    private readonly ServerOpsMetrics? _opsMetrics = opsMetrics;

    public async Task<(bool Success, AccountRecord? Account, bool RateLimited)> TryAuthenticateAsync(
        string username,
        string password,
        string rateLimitKey,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryAllow(rateLimitKey))
        {
            _opsMetrics?.RecordRateLimitHit("login");
            return (false, null, true);
        }

        if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidLoginPassword(password))
        {
            PasswordHasher.VerifyOrTimingSafeReject(password, null, null);
            return (false, null, false);
        }

        var account = await _accountRepository.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        var ok = VerifyStoredPassword(password, account?.PasswordHash);
        if (!ok)
        {
            _rateLimiter.RegisterFailure(rateLimitKey);
            return (false, null, false);
        }

        _rateLimiter.RegisterSuccess(rateLimitKey);
        return (true, account, false);
    }

    public Task<AccountCreateResult> RegisterAccountAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidPassword(password))
        {
            return Task.FromResult(new AccountCreateResult(AccountCreateStatus.InvalidInput));
        }

        return _accountRepository.TryCreateAsync(username, password, cancellationToken);
    }

    public bool TryAllowReconnect(string rateLimitKey)
    {
        var allowed = _rateLimiter.TryAllow("reconnect:" + rateLimitKey);
        if (!allowed)
        {
            _opsMetrics?.RecordRateLimitHit("reconnect");
        }

        return allowed;
    }

    public void RegisterReconnectFailure(string rateLimitKey) => _rateLimiter.RegisterFailure("reconnect:" + rateLimitKey);

    public void RegisterReconnectSuccess(string rateLimitKey) => _rateLimiter.RegisterSuccess("reconnect:" + rateLimitKey);

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
