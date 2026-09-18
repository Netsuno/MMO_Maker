using System.Collections.Concurrent;
using Frog.Application.Identity;
using Frog.Core.Protocol;

namespace Frog.Server.Database;

/// <summary>Sanctions en mémoire (playtest / tests). Vide par défaut.</summary>
public sealed class InMemoryAccountSanctionStore : IAccountSanctionStore
{
    private readonly ConcurrentDictionary<Guid, SanctionRow> _rows = new();
    private readonly ConcurrentQueue<ModerationEventRecord> _events = new();
    private readonly TimeProvider _clock;
    private readonly object _gate = new();

    public InMemoryAccountSanctionStore(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public Task<bool> HasActiveAsync(
        Guid accountId,
        string kind,
        CancellationToken cancellationToken = default)
        => Task.FromResult(GetActive(accountId, kind) is not null);

    public Task<AccountSanctionRecord?> GetActiveAsync(
        Guid accountId,
        string kind,
        CancellationToken cancellationToken = default)
        => Task.FromResult(GetActive(accountId, kind));

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
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            var existing = FindUnrevoked(accountId, kind);
            if (existing is null)
            {
                var id = Guid.NewGuid();
                _rows[id] = new SanctionRow
                {
                    Record = new AccountSanctionRecord(
                        id,
                        accountId,
                        kind,
                        reasonText,
                        actorAccountId,
                        now,
                        expiresAtUtc,
                        null),
                };
            }
            else
            {
                existing.Record = existing.Record with
                {
                    Reason = reasonText,
                    ActorAccountId = actorAccountId,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = expiresAtUtc,
                    RevokedAtUtc = null,
                };
            }

            _events.Enqueue(new ModerationEventRecord(
                Guid.NewGuid(),
                now,
                actorAccountId,
                accountId,
                kind,
                reasonText,
                null));
        }

        return Task.CompletedTask;
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
        var now = _clock.GetUtcNow();
        var revoked = false;
        lock (_gate)
        {
            var existing = FindUnrevoked(accountId, kind);
            if (existing is not null)
            {
                existing.Record = existing.Record with { RevokedAtUtc = now };
                revoked = true;
            }

            _events.Enqueue(new ModerationEventRecord(
                Guid.NewGuid(),
                now,
                actorAccountId,
                accountId,
                action,
                reasonText,
                null));
        }

        return Task.FromResult(revoked);
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

        _events.Enqueue(new ModerationEventRecord(
            Guid.NewGuid(),
            _clock.GetUtcNow(),
            actorAccountId,
            targetAccountId,
            action.Trim(),
            NormalizeReason(reason),
            detailsJson));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModerationEventRecord>> ListEventsForTargetAsync(
        Guid targetAccountId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModerationEventRecord> list = _events
            .Where(e => e.TargetAccountId == targetAccountId)
            .OrderBy(e => e.AtUtc)
            .ToArray();
        return Task.FromResult(list);
    }

    private AccountSanctionRecord? GetActive(Guid accountId, string kind)
    {
        if (accountId == Guid.Empty || !SanctionKinds.IsPersistedKind(kind))
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            foreach (var row in _rows.Values)
            {
                var s = row.Record;
                if (s.AccountId != accountId || s.Kind != kind || s.RevokedAtUtc is not null)
                {
                    continue;
                }

                if (s.ExpiresAtUtc is { } exp && exp <= now)
                {
                    continue;
                }

                return s;
            }
        }

        return null;
    }

    private SanctionRow? FindUnrevoked(Guid accountId, string kind)
    {
        foreach (var row in _rows.Values)
        {
            if (row.Record.AccountId == accountId
                && row.Record.Kind == kind
                && row.Record.RevokedAtUtc is null)
            {
                return row;
            }
        }

        return null;
    }

    private static string NormalizeReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return ModerateWire.DefaultReason;
        }

        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }

    private sealed class SanctionRow
    {
        public required AccountSanctionRecord Record { get; set; }
    }
}
