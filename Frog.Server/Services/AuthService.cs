using Frog.Core.Utils;
using Frog.Server.Database;

namespace Frog.Server.Services;

public sealed class AuthService(IAccountRepository accountRepository)
{
    private readonly IAccountRepository _accountRepository = accountRepository;

    public bool ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (!_accountRepository.TryGetByUsername(username, out var account))
        {
            return false;
        }

        return HashHelper.VerifyPassword(password, account.PasswordHash, account.PasswordSalt);
    }

    public bool RegisterAccount(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        return _accountRepository.Create(username, password);
    }
}
