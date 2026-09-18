using Frog.Application.Identity;
using Frog.Persistence.PostgreSql.Entities.Auth;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Auth;

public sealed class PostgresOperatorDirectory : IOperatorDirectory
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresOperatorDirectory(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<bool> IsOperatorAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return _gate.ExecuteAsync(async (db, ct) =>
            await db.AuthOperators
                .AsNoTracking()
                .AnyAsync(o => o.AccountId == accountId && o.RevokedAtUtc == null, ct)
                .ConfigureAwait(false), cancellationToken);
    }

    public Task<OperatorGrantResult> GrantAsync(
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
            return Task.FromResult(new OperatorGrantResult(OperatorGrantStatus.InvalidInput));
        }

        var by = grantedBy.Trim();
        return _gate.ExecuteAsync(async (db, ct) =>
        {
            var exists = await db.AuthAccounts
                .AnyAsync(a => a.Id == accountId, ct)
                .ConfigureAwait(false);
            if (!exists)
            {
                return new OperatorGrantResult(OperatorGrantStatus.AccountNotFound);
            }

            var entity = await db.AuthOperators
                .FirstOrDefaultAsync(o => o.AccountId == accountId, ct)
                .ConfigureAwait(false);
            var now = _clock.GetUtcNow();
            if (entity is null)
            {
                db.AuthOperators.Add(new OperatorEntity
                {
                    AccountId = accountId,
                    GrantedAtUtc = now,
                    GrantedBy = by,
                    Note = note,
                    RevokedAtUtc = null,
                });
            }
            else
            {
                entity.GrantedAtUtc = now;
                entity.GrantedBy = by;
                entity.Note = note;
                entity.RevokedAtUtc = null;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new OperatorGrantResult(OperatorGrantStatus.Granted);
        }, cancellationToken);
    }

    public Task<bool> RevokeAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.AuthOperators
                .FirstOrDefaultAsync(o => o.AccountId == accountId && o.RevokedAtUtc == null, ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return false;
            }

            entity.RevokedAtUtc = _clock.GetUtcNow();
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);
}
