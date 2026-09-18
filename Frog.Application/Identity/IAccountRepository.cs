namespace Frog.Application.Identity;

public interface IAccountRepository
{
    Task<AccountRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<AccountRecord?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<AccountCreateResult> TryCreateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Remplace le hash. L'opérateur n'a pas besoin de l'ancien mot de passe.</summary>
    Task<bool> UpdatePasswordAsync(
        string username,
        string newPassword,
        CancellationToken cancellationToken = default);
}
