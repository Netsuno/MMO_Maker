using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

public sealed class InMemoryGroundItemRepository : IGroundItemRepository
{
    private readonly ConcurrentDictionary<Guid, GroundItemRecord> _items = new();
    private readonly object _gate = new();
    private readonly TimeProvider _clock;

    public InMemoryGroundItemRepository(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public Task<IReadOnlyList<GroundItemRecord>> ListOnMapAsync(int mapId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            var list = _items.Values
                .Where(i => i.MapId == mapId && !GroundLootLifetime.IsExpired(i.CreatedAtUtc, now))
                .ToArray();
            return Task.FromResult<IReadOnlyList<GroundItemRecord>>(list);
        }
    }

    public Task<int> PurgeExpiredAsync(int mapId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(RemoveExpiredUnlocked(mapId));
        }
    }

    public Task<GroundItemMutationResult> DropAsync(
        int mapId,
        int pixelX,
        int pixelY,
        Guid itemId,
        int quantity,
        Guid? ownerCharacterId,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || itemId == Guid.Empty)
        {
            return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.InvalidQuantity));
        }

        lock (_gate)
        {
            RemoveExpiredUnlocked(mapId);
            var onMap = _items.Values.Count(i => i.MapId == mapId);
            if (onMap >= GameplayLimits.MaxGroundItemsPerMap)
            {
                return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.MapFull));
            }

            var record = new GroundItemRecord(
                Guid.NewGuid(),
                mapId,
                pixelX,
                pixelY,
                itemId,
                quantity,
                ownerCharacterId,
                _clock.GetUtcNow());
            _items[record.Id] = record;
            return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.Ok, record));
        }
    }

    public Task<GroundItemMutationResult> TryPickupAsync(
        Guid groundItemId,
        Guid pickerCharacterId,
        int pickerPixelX,
        int pickerPixelY,
        int rangePixels,
        CancellationToken cancellationToken = default)
    {
        _ = pickerCharacterId;
        lock (_gate)
        {
            if (!_items.TryGetValue(groundItemId, out var item))
            {
                return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.NotFound));
            }

            if (GroundLootLifetime.IsExpired(item.CreatedAtUtc, _clock.GetUtcNow()))
            {
                _items.TryRemove(groundItemId, out _);
                return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.NotFound));
            }

            var distSq = WorldMetrics.DistanceSquaredPixels(
                pickerPixelX,
                pickerPixelY,
                item.PixelX,
                item.PixelY);
            if (distSq > (long)rangePixels * rangePixels)
            {
                return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.OutOfRange));
            }

            if (!_items.TryRemove(groundItemId, out var taken))
            {
                return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.AlreadyTaken));
            }

            return Task.FromResult(new GroundItemMutationResult(GroundItemMutationStatus.Ok, taken));
        }
    }

    internal bool TryGetUntaken(Guid groundItemId, out GroundItemRecord? item)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(groundItemId, out var record)
                && !GroundLootLifetime.IsExpired(record.CreatedAtUtc, _clock.GetUtcNow()))
            {
                item = record;
                return true;
            }

            item = null;
            return false;
        }
    }

    internal bool TryRemoveUntaken(Guid groundItemId, out GroundItemRecord? removed)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(groundItemId, out var current)
                && GroundLootLifetime.IsExpired(current.CreatedAtUtc, _clock.GetUtcNow()))
            {
                _items.TryRemove(groundItemId, out _);
                removed = null;
                return false;
            }

            if (_items.TryRemove(groundItemId, out var record))
            {
                removed = record;
                return true;
            }

            removed = null;
            return false;
        }
    }

    internal void Restore(GroundItemRecord item)
    {
        lock (_gate)
        {
            _items[item.Id] = item;
        }
    }

    private int RemoveExpiredUnlocked(int mapId)
    {
        var now = _clock.GetUtcNow();
        var expired = _items.Values
            .Where(i => i.MapId == mapId && GroundLootLifetime.IsExpired(i.CreatedAtUtc, now))
            .Select(i => i.Id)
            .ToArray();
        foreach (var id in expired)
        {
            _items.TryRemove(id, out _);
        }

        return expired.Length;
    }
}
