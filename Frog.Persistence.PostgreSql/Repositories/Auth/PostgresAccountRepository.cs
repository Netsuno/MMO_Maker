using Frog.Application.Identity;
using Frog.Core.Identity;
using Frog.Core.Security;
using Frog.Persistence.PostgreSql.Entities.Auth;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Auth;

public sealed class PostgresAccountRepository : IAccountRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresAccountRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<AccountRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (!AccountInputRules.IsValidUsername(username))
            {
                return null;
            }

            var normalized = username.Trim();
            var entity = await db.AuthAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => EF.Functions.ILike(a.Username, normalized),
                    ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToRecord(entity);
        }, cancellationToken);

    public Task<AccountRecord?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.AuthAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToRecord(entity);
        }, cancellationToken);

    public Task<AccountCreateResult> TryCreateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidPassword(password))
            {
                return new AccountCreateResult(AccountCreateStatus.InvalidInput);
            }

            var normalized = username.Trim();
            var exists = await db.AuthAccounts
                .AnyAsync(a => EF.Functions.ILike(a.Username, normalized), ct)
                .ConfigureAwait(false);
            if (exists)
            {
                return new AccountCreateResult(AccountCreateStatus.DuplicateUsername);
            }

            var entity = new AccountEntity
            {
                Id = Guid.NewGuid(),
                Username = normalized,
                PasswordHash = PasswordHasher.HashPassword(password),
                CreatedAtUtc = _clock.GetUtcNow(),
            };

            db.AuthAccounts.Add(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new AccountCreateResult(AccountCreateStatus.Created, entity.Id);
        }, cancellationToken);

    private static AccountRecord ToRecord(AccountEntity entity)
        => new(entity.Id, entity.Username, entity.PasswordHash, entity.CreatedAtUtc);
}
