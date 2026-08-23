using System.Collections.Concurrent;
using Frog.Core.Identity;
using Frog.Core.Utils;
using Frog.Server.Models;

namespace Frog.Server.Database;

public sealed class AccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, Account> _accounts = new(AccountUsername.Comparer);

    public AccountRepository()
    {
        // Compte de bootstrap pour le Sprint 1.
        var (hash, salt) = HashHelper.HashPassword("demo");
        var account = new Account
        {
            Username = "demo",
            PasswordHash = hash,
            PasswordSalt = salt
        };

        _accounts.TryAdd(account.Username, account);
    }

    public bool TryGetByUsername(string username, out Account account)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return _accounts.TryGetValue(username, out account!);
    }

    public bool Create(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        var (hash, salt) = HashHelper.HashPassword(password);
        var account = new Account
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt
        };

        return _accounts.TryAdd(username, account);
    }
}
