using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

public sealed class InMemoryGroundItemRepository : IGroundItemRepository
{
    private readonly ConcurrentDictionary<Guid, GroundItemRecord> _items = new();
    private readonly object _gate = new();

    public Task<IReadOnlyList<GroundItemRecord>> ListOnMapAsync(int mapId, CancellationToken cancellationToken = default)
    {
        var list = _items.Values.Where(i => i.MapId == mapId).ToArray();
        return Task.FromResult<IReadOnlyList<GroundItemRecord>>(list);
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
                DateTimeOffset.UtcNow);
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
        lock (_gate)
        {
            if (!_items.TryGetValue(groundItemId, out var item))
            {
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
}
