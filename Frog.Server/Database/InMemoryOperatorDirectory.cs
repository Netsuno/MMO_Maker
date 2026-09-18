using System.Collections.Concurrent;
using Frog.Application.Identity;

namespace Frog.Server.Database;

/// <summary>Répertoire opérateurs en mémoire (playtest / tests). Vide par défaut — aucun compte n'est GM.</summary>
public sealed class InMemoryOperatorDirectory(IAccountRepository accounts) : IOperatorDirectory
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly ConcurrentDictionary<Guid, OperatorState> _operators = new();

    public Task<bool> IsOperatorAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            _operators.TryGetValue(accountId, out var state) && state.RevokedAtUtc is null);
    }

    public async Task<OperatorGrantResult> GrantAsync(
        Guid accountId,
        string grantedBy,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty
            || string.IsNullOrWhiteSpace(grantedBy)
            || grantedBy.Trim().Length > 64
            || (note is { Length: > 256 }))
        {
            return new OperatorGrantResult(OperatorGrantStatus.InvalidInput);
        }

        var account = await _accounts.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new OperatorGrantResult(OperatorGrantStatus.AccountNotFound);
        }

        _operators.AddOrUpdate(
            accountId,
            _ => new OperatorState { GrantedBy = grantedBy.Trim(), Note = note, RevokedAtUtc = null },
            (_, existing) =>
            {
                existing.GrantedBy = grantedBy.Trim();
                existing.Note = note;
                existing.RevokedAtUtc = null;
                return existing;
            });
        return new OperatorGrantResult(OperatorGrantStatus.Granted);
    }

    public Task<bool> RevokeAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (!_operators.TryGetValue(accountId, out var state) || state.RevokedAtUtc is not null)
        {
            return Task.FromResult(false);
        }

        state.RevokedAtUtc = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    private sealed class OperatorState
    {
        public string GrantedBy { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTimeOffset? RevokedAtUtc { get; set; }
    }
}
