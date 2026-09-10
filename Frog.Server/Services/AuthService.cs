using Frog.Application.Identity;
using Frog.Core.Security;
using Frog.Server.Security;

namespace Frog.Server.Services;

public sealed class AuthService(
    IAccountRepository accountRepository,
    LoginRateLimiter rateLimiter)
{
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly LoginRateLimiter _rateLimiter = rateLimiter;

    public async Task<(bool Success, AccountRecord? Account)> TryAuthenticateAsync(
        string username,
        string password,
        string rateLimitKey,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryAllow(rateLimitKey))
        {
            return (false, null);
        }

        if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidLoginPassword(password))
        {
            PasswordHasher.VerifyOrTimingSafeReject(password, null, null);
            return (false, null);
        }

        var account = await _accountRepository.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        var ok = VerifyStoredPassword(password, account?.PasswordHash);
        if (!ok)
        {
            _rateLimiter.RegisterFailure(rateLimitKey);
            return (false, null);
        }

        _rateLimiter.RegisterSuccess(rateLimitKey);
        return (true, account);
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

    public bool TryAllowReconnect(string rateLimitKey) => _rateLimiter.TryAllow("reconnect:" + rateLimitKey);

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
