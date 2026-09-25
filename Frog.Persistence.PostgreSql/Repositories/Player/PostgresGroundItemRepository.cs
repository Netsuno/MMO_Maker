using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresGroundItemRepository : IGroundItemRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresGroundItemRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<IReadOnlyList<GroundItemRecord>> ListOnMapAsync(
        int mapId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var cutoff = GroundLootLifetime.Cutoff(_clock.GetUtcNow());
            var items = await db.PlayerGroundItems
                .AsNoTracking()
                .Where(i => i.MapId == mapId && i.TakenAtUtc == null && i.CreatedAtUtc > cutoff)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return (IReadOnlyList<GroundItemRecord>)items.Select(PlayerEntityMapper.ToGroundItemRecord).ToArray();
        }, cancellationToken);

    public Task<int> PurgeExpiredAsync(int mapId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            var cutoff = GroundLootLifetime.Cutoff(now);
            var expired = await db.PlayerGroundItems
                .Where(i => i.MapId == mapId && i.TakenAtUtc == null && i.CreatedAtUtc <= cutoff)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (expired.Count == 0)
            {
                return 0;
            }

            foreach (var row in expired)
            {
                row.TakenAtUtc = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return expired.Count;
        }, cancellationToken);

    public Task<GroundItemMutationResult> DropAsync(
        int mapId,
        int pixelX,
        int pixelY,
        Guid itemId,
        int quantity,
        Guid? ownerCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (quantity <= 0 || itemId == Guid.Empty)
            {
                return new GroundItemMutationResult(GroundItemMutationStatus.InvalidQuantity);
            }

            var now = _clock.GetUtcNow();
            var cutoff = GroundLootLifetime.Cutoff(now);
            var expired = await db.PlayerGroundItems
                .Where(i => i.MapId == mapId && i.TakenAtUtc == null && i.CreatedAtUtc <= cutoff)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var row in expired)
            {
                row.TakenAtUtc = now;
            }

            if (expired.Count > 0)
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            var onMap = await db.PlayerGroundItems
                .CountAsync(i => i.MapId == mapId && i.TakenAtUtc == null && i.CreatedAtUtc > cutoff, ct)
                .ConfigureAwait(false);
            if (onMap >= GameplayLimits.MaxGroundItemsPerMap)
            {
                return new GroundItemMutationResult(GroundItemMutationStatus.MapFull);
            }

            var entity = new GroundItemEntity
            {
                Id = Guid.NewGuid(),
                MapId = mapId,
                PixelX = pixelX,
                PixelY = pixelY,
                ItemId = itemId,
                Quantity = quantity,
                OwnerCharacterId = ownerCharacterId,
                CreatedAtUtc = _clock.GetUtcNow(),
            };

            db.PlayerGroundItems.Add(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new GroundItemMutationResult(
                GroundItemMutationStatus.Ok,
                PlayerEntityMapper.ToGroundItemRecord(entity));
        }, cancellationToken);

    public Task<GroundItemMutationResult> TryPickupAsync(
        Guid groundItemId,
        Guid pickerCharacterId,
        int pickerPixelX,
        int pickerPixelY,
        int rangePixels,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            _ = pickerCharacterId;
            var rangeSq = (long)rangePixels * rangePixels;
            var now = _clock.GetUtcNow();
            var cutoff = GroundLootLifetime.Cutoff(now);

            // Atomic claim: exactly one concurrent UPDATE wins when taken_at_utc IS NULL.
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE player.ground_items AS g
                    SET taken_at_utc = {now}
                    WHERE g.id = {groundItemId}
                      AND g.taken_at_utc IS NULL
                      AND g.created_at_utc > {cutoff}
                      AND (
                            (CAST(g.pixel_x AS bigint) - {pickerPixelX}) * (CAST(g.pixel_x AS bigint) - {pickerPixelX})
                          + (CAST(g.pixel_y AS bigint) - {pickerPixelY}) * (CAST(g.pixel_y AS bigint) - {pickerPixelY})
                          ) <= {rangeSq}
                    """,
                    ct)
                .ConfigureAwait(false);

            if (claimed == 1)
            {
                var taken = await db.PlayerGroundItems
                    .AsNoTracking()
                    .FirstAsync(i => i.Id == groundItemId, ct)
                    .ConfigureAwait(false);
                return new GroundItemMutationResult(
                    GroundItemMutationStatus.Ok,
                    PlayerEntityMapper.ToGroundItemRecord(taken));
            }

            var existing = await db.PlayerGroundItems
                .FirstOrDefaultAsync(i => i.Id == groundItemId, ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return new GroundItemMutationResult(GroundItemMutationStatus.NotFound);
            }

            if (existing.TakenAtUtc is not null)
            {
                return new GroundItemMutationResult(GroundItemMutationStatus.AlreadyTaken);
            }

            if (GroundLootLifetime.IsExpired(existing.CreatedAtUtc, now))
            {
                existing.TakenAtUtc = now;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return new GroundItemMutationResult(GroundItemMutationStatus.NotFound);
            }

            return new GroundItemMutationResult(GroundItemMutationStatus.OutOfRange);
        }, cancellationToken);
}
