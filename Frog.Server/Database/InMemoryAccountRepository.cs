using Frog.Application.Identity;
using Frog.Core.Identity;
using Frog.Core.Security;

namespace Frog.Server.Database;

/// <summary>Dépôt comptes en mémoire (playtest / bootstrap).</summary>
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AccountRecord> _accounts =
        new(AccountUsername.Comparer);

    public InMemoryAccountRepository()
    {
        var hash = PasswordHasher.HashPassword("demo");
        _accounts.TryAdd(
            "demo",
            new AccountRecord(Guid.Parse("11111111-1111-1111-1111-111111111111"), "demo", hash, DateTimeOffset.UtcNow));
    }

    public Task<AccountRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (!AccountInputRules.IsValidUsername(username))
        {
            return Task.FromResult<AccountRecord?>(null);
        }

        _accounts.TryGetValue(username.Trim(), out var record);
        return Task.FromResult(record);
    }

    public Task<AccountRecord?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        AccountRecord? found = null;
        foreach (var pair in _accounts)
        {
            if (pair.Value.Id == accountId)
            {
                found = pair.Value;
                break;
            }
        }

        return Task.FromResult(found);
    }

    public Task<AccountCreateResult> TryCreateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidPassword(password))
        {
            return Task.FromResult(new AccountCreateResult(AccountCreateStatus.InvalidInput));
        }

        var normalized = username.Trim();
        var record = new AccountRecord(
            Guid.NewGuid(),
            normalized,
            PasswordHasher.HashPassword(password),
            DateTimeOffset.UtcNow);
        return Task.FromResult(
            _accounts.TryAdd(normalized, record)
                ? new AccountCreateResult(AccountCreateStatus.Created, record.Id)
                : new AccountCreateResult(AccountCreateStatus.DuplicateUsername));
    }
}
