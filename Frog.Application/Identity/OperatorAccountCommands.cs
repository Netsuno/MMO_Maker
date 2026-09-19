namespace Frog.Application.Identity;

/// <summary>Commandes opérateur hors bande TCP (P10-5 C). Create n'accorde jamais le rôle GM.</summary>
public sealed class OperatorAccountCommands(
    IAccountRepository accounts,
    IAuthSessionRepository sessions,
    IOperatorDirectory operators,
    IAccountSanctionStore sanctions)
{
    public Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
        => accounts.TryCreateAsync(username, password, cancellationToken);

    public async Task<bool> ResetPasswordAsync(
        string username,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return false;
        }

        if (!await accounts.UpdatePasswordAsync(username, newPassword, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await sessions.RevokeAllForAccountAsync(account.Id, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RevokeSessionsAsync(string username, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return false;
        }

        await sessions.RevokeAllForAccountAsync(account.Id, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<OperatorGrantResult> GrantOperatorAsync(
        string username,
        string grantedBy,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new OperatorGrantResult(OperatorGrantStatus.AccountNotFound);
        }

        return await operators.GrantAsync(account.Id, grantedBy, note, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RevokeOperatorAsync(string username, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return false;
        }

        return await operators.RevokeAsync(account.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsOperatorAsync(string username, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return false;
        }

        return await operators.IsOperatorAsync(account.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplySanctionAsync(
        string targetUsername,
        string actorUsername,
        string kind,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var target = await accounts.FindByUsernameAsync(targetUsername, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Compte cible introuvable.");
        var actor = await accounts.FindByUsernameAsync(actorUsername, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Compte acteur introuvable.");
        if (!await operators.IsOperatorAsync(actor.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("L'acteur n'est pas operateur.");
        }

        await sanctions.ApplyAsync(target.Id, kind, actor.Id, reason, expiresAtUtc: null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> LiftSanctionAsync(
        string targetUsername,
        string actorUsername,
        string kind,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var target = await accounts.FindByUsernameAsync(targetUsername, cancellationToken).ConfigureAwait(false);
        var actor = await accounts.FindByUsernameAsync(actorUsername, cancellationToken).ConfigureAwait(false);
        if (target is null || actor is null)
        {
            return false;
        }

        if (!await operators.IsOperatorAsync(actor.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("L'acteur n'est pas operateur.");
        }

        return await sanctions.RevokeAsync(target.Id, kind, actor.Id, reason, cancellationToken).ConfigureAwait(false);
    }

    public static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        Span<byte> bytes = stackalloc byte[20];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }
}
