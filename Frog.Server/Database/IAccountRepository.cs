using Frog.Server.Models;

namespace Frog.Server.Database;

public interface IAccountRepository
{
    bool TryGetByUsername(string username, out Account account);
    bool Create(string username, string password);
}
