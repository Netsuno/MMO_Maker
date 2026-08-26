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
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var entity = await db.PlayerGroundItems
                .FirstOrDefaultAsync(i => i.Id == groundItemId && i.TakenAtUtc == null, ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                var exists = await db.PlayerGroundItems
                    .AsNoTracking()
                    .AnyAsync(i => i.Id == groundItemId, ct)
                    .ConfigureAwait(false);
                return new GroundItemMutationResult(
                    exists ? GroundItemMutationStatus.AlreadyTaken : GroundItemMutationStatus.NotFound);
            }

            var distSq = WorldMetrics.DistanceSquaredPixels(
                pickerPixelX,
                pickerPixelY,
                entity.PixelX,
                entity.PixelY);
            if (distSq > (long)rangePixels * rangePixels)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return new GroundItemMutationResult(GroundItemMutationStatus.OutOfRange);
            }

            var now = _clock.GetUtcNow();
            entity.TakenAtUtc = now;
            var affected = await db.SaveChangesAsync(ct).ConfigureAwait(false);
            if (affected == 0)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return new GroundItemMutationResult(GroundItemMutationStatus.AlreadyTaken);
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
            return new GroundItemMutationResult(
                GroundItemMutationStatus.Ok,
                PlayerEntityMapper.ToGroundItemRecord(entity));
        }, cancellationToken);
}
