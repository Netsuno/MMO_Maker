using Frog.Application.Identity;
using Frog.Persistence.PostgreSql.Entities.Ops;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Ops;

public sealed class PostgresAccountSanctionStore : IAccountSanctionStore
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresAccountSanctionStore(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<bool> HasActiveAsync(
        Guid accountId,
        string kind,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty || !SanctionKinds.IsPersistedKind(kind))
        {
            return Task.FromResult(false);
        }

        var now = _clock.GetUtcNow();
        return _gate.ExecuteAsync(async (db, ct) =>
            await db.OpsAccountSanctions
                .AsNoTracking()
                .AnyAsync(
                    s => s.AccountId == accountId
                         && s.Kind == kind
                         && s.RevokedAtUtc == null
                         && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now),
                    ct)
                .ConfigureAwait(false), cancellationToken);
    }

    public Task<AccountSanctionRecord?> GetActiveAsync(
        Guid accountId,
        string kind,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty || !SanctionKinds.IsPersistedKind(kind))
        {
            return Task.FromResult<AccountSanctionRecord?>(null);
        }

        var now = _clock.GetUtcNow();
        return _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.OpsAccountSanctions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.AccountId == accountId
                         && s.Kind == kind
                         && s.RevokedAtUtc == null
                         && (s.ExpiresAtUtc == null || s.ExpiresAtUtc > now),
                    ct)
                .ConfigureAwait(false);
            return entity is null ? null : MapSanction(entity);
        }, cancellationToken);
    }

    public Task ApplyAsync(
        Guid accountId,
        string kind,
        Guid actorAccountId,
        string reason,
        DateTimeOffset? expiresAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty
            || actorAccountId == Guid.Empty
            || !SanctionKinds.IsPersistedKind(kind))
        {
            throw new ArgumentException("Sanction apply: invalid account or kind.");
        }

        var reasonText = NormalizeReason(reason);
        return _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            var entity = await db.OpsAccountSanctions
                .FirstOrDefaultAsync(
                    s => s.AccountId == accountId && s.Kind == kind && s.RevokedAtUtc == null,
                    ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                entity = new AccountSanctionEntity
                {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Kind = kind,
                    Reason = reasonText,
                    ActorAccountId = actorAccountId,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = expiresAtUtc,
                    RevokedAtUtc = null,
                };
                db.OpsAccountSanctions.Add(entity);
            }
            else
            {
                entity.Reason = reasonText;
                entity.ActorAccountId = actorAccountId;
                entity.CreatedAtUtc = now;
                entity.ExpiresAtUtc = expiresAtUtc;
            }

            db.OpsModerationEvents.Add(new ModerationEventEntity
            {
                Id = Guid.NewGuid(),
                AtUtc = now,
                ActorAccountId = actorAccountId,
                TargetAccountId = accountId,
                Action = kind,
                Reason = reasonText,
                DetailsJson = null,
            });

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<bool> RevokeAsync(
        Guid accountId,
        string kind,
        Guid actorAccountId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty || !SanctionKinds.IsPersistedKind(kind))
        {
            return Task.FromResult(false);
        }

        var reasonText = NormalizeReason(reason);
        var action = kind == SanctionKinds.Mute ? ModerationEventActions.Unmute : ModerationEventActions.Unban;
        return _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            var entity = await db.OpsAccountSanctions
                .FirstOrDefaultAsync(
                    s => s.AccountId == accountId && s.Kind == kind && s.RevokedAtUtc == null,
                    ct)
                .ConfigureAwait(false);
            var revoked = false;
            if (entity is not null)
            {
                entity.RevokedAtUtc = now;
                revoked = true;
            }

            db.OpsModerationEvents.Add(new ModerationEventEntity
            {
                Id = Guid.NewGuid(),
                AtUtc = now,
                ActorAccountId = actorAccountId,
                TargetAccountId = accountId,
                Action = action,
                Reason = reasonText,
                DetailsJson = null,
            });

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return revoked;
        }, cancellationToken);
    }

    public Task RecordEventAsync(
        Guid actorAccountId,
        Guid targetAccountId,
        string action,
        string reason,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        if (actorAccountId == Guid.Empty || targetAccountId == Guid.Empty || string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Moderation event: invalid actor, target, or action.");
        }

        var reasonText = NormalizeReason(reason);
        return _gate.ExecuteAsync(async (db, ct) =>
        {
            db.OpsModerationEvents.Add(new ModerationEventEntity
            {
                Id = Guid.NewGuid(),
                AtUtc = _clock.GetUtcNow(),
                ActorAccountId = actorAccountId,
                TargetAccountId = targetAccountId,
                Action = action.Trim(),
                Reason = reasonText,
                DetailsJson = detailsJson,
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ModerationEventRecord>> ListEventsForTargetAsync(
        Guid targetAccountId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.OpsModerationEvents
                .AsNoTracking()
                .Where(e => e.TargetAccountId == targetAccountId)
                .OrderBy(e => e.AtUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            IReadOnlyList<ModerationEventRecord> mapped = rows.Select(MapEvent).ToArray();
            return mapped;
        }, cancellationToken);

    private static string NormalizeReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Frog.Core.Protocol.ModerateWire.DefaultReason;
        }

        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }

    private static AccountSanctionRecord MapSanction(AccountSanctionEntity entity)
        => new(
            entity.Id,
            entity.AccountId,
            entity.Kind,
            entity.Reason,
            entity.ActorAccountId,
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc,
            entity.RevokedAtUtc);

    private static ModerationEventRecord MapEvent(ModerationEventEntity entity)
        => new(
            entity.Id,
            entity.AtUtc,
            entity.ActorAccountId,
            entity.TargetAccountId,
            entity.Action,
            entity.Reason,
            entity.DetailsJson);
}
