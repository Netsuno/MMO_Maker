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
            var items = await db.PlayerGroundItems
                .AsNoTracking()
                .Where(i => i.MapId == mapId && i.TakenAtUtc == null)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return (IReadOnlyList<GroundItemRecord>)items.Select(PlayerEntityMapper.ToGroundItemRecord).ToArray();
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

            var onMap = await db.PlayerGroundItems
                .CountAsync(i => i.MapId == mapId && i.TakenAtUtc == null, ct)
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

            // Atomic claim: exactly one concurrent UPDATE wins when taken_at_utc IS NULL.
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE player.ground_items AS g
                    SET taken_at_utc = {now}
                    WHERE g.id = {groundItemId}
                      AND g.taken_at_utc IS NULL
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
                .AsNoTracking()
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

            return new GroundItemMutationResult(GroundItemMutationStatus.OutOfRange);
        }, cancellationToken);
}
