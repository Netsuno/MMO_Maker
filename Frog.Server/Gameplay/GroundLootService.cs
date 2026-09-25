using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Gameplay;
using Frog.Server.Models;
using Frog.Server.Services;

namespace Frog.Server.Gameplay;

/// <summary>
/// Dépose le butin de mort et ramasse les piles à portée via l'inventaire existant.
/// Durée de vie : <see cref="GroundLootLifetime.TimeToLive"/> (3 min), y compris les dépôts joueur.
/// </summary>
public sealed class GroundLootService
{
    private readonly IGroundItemRepository _ground;
    private readonly IPublishedItemCatalog _items;
    private readonly InventoryGameplayService _inventory;
    private readonly GroundLootTable _table;
    private readonly Func<int, int, int, bool> _isTileBlocked;
    private readonly Func<int, (bool Found, int Width, int Height)> _mapBounds;

    public GroundLootService(
        IGroundItemRepository groundItems,
        IPublishedItemCatalog items,
        InventoryGameplayService inventory,
        MapService maps,
        GroundLootTable table)
        : this(
            groundItems,
            items,
            inventory,
            table,
            (mapId, tileX, tileY) => maps.IsBlocked(mapId, tileX, tileY),
            mapId => maps.TryGetMapBounds(mapId, out var width, out var height)
                ? (true, width, height)
                : (false, 0, 0))
    {
    }

    internal GroundLootService(
        IGroundItemRepository groundItems,
        IPublishedItemCatalog items,
        InventoryGameplayService inventory,
        GroundLootTable table,
        Func<int, int, int, bool> isTileBlocked,
        Func<int, (bool Found, int Width, int Height)> mapBounds)
    {
        _ground = groundItems;
        _items = items;
        _inventory = inventory;
        _table = table;
        _isTileBlocked = isTileBlocked;
        _mapBounds = mapBounds;
    }

    public Task<IReadOnlyList<GroundItemRecord>> DropMonsterLootAsync(
        int mapId,
        int pixelX,
        int pixelY,
        Guid npcDefinitionId,
        CancellationToken cancellationToken = default)
        => DropStacksAsync(mapId, pixelX, pixelY, _table.ForMonster(npcDefinitionId), ownerCharacterId: null, cancellationToken);

    public Task<IReadOnlyList<GroundItemRecord>> DropPlayerLootAsync(
        int mapId,
        int pixelX,
        int pixelY,
        Guid? ownerCharacterId,
        CancellationToken cancellationToken = default)
        => DropStacksAsync(mapId, pixelX, pixelY, _table.PlayerDeath, ownerCharacterId, cancellationToken);

    public async Task<IReadOnlyList<PickupResult>> PickupOnCurrentTileAsync(Session session, CancellationToken cancellationToken = default)
    {
        if (!session.HasActiveCharacter() || session.IsDead)
        {
            return Array.Empty<PickupResult>();
        }

        var playerTile = GroundLootPlacement.PixelToTile(session.PixelX, session.PixelY);
        var items = await _ground.ListOnMapAsync(session.CurrentMapId, cancellationToken).ConfigureAwait(false);
        var here = items.Where(item => GroundLootPlacement.PixelToTile(item.PixelX, item.PixelY) == playerTile).ToArray();
        if (here.Length == 0)
        {
            return Array.Empty<PickupResult>();
        }

        var results = new List<PickupResult>(here.Length);
        foreach (var item in here)
        {
            results.Add(await _inventory.TryPickupAsync(session, item.Id, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<PickupResult?> TryPickupNearestInRangeAsync(Session session, CancellationToken cancellationToken = default)
    {
        if (!session.HasActiveCharacter() || session.IsDead)
        {
            return null;
        }

        var items = await _ground.ListOnMapAsync(session.CurrentMapId, cancellationToken).ConfigureAwait(false);
        GroundItemRecord? best = null;
        var bestDist = long.MaxValue;
        var range = GameplayLimits.GroundPickupRangePixels;
        var rangeSq = (long)range * range;
        foreach (var item in items)
        {
            var dist = WorldMetrics.DistanceSquaredPixels(session.PixelX, session.PixelY, item.PixelX, item.PixelY);
            if (dist <= rangeSq && dist < bestDist)
            {
                best = item;
                bestDist = dist;
            }
        }

        if (best is null)
        {
            return null;
        }

        return await _inventory.TryPickupAsync(session, best.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<GroundItemRecord>> DropStacksAsync(
        int mapId,
        int pixelX,
        int pixelY,
        IReadOnlyList<GroundLootStack> stacks,
        Guid? ownerCharacterId,
        CancellationToken cancellationToken)
    {
        if (stacks.Count == 0)
        {
            return Array.Empty<GroundItemRecord>();
        }

        var spots = GroundLootPlacement.ChooseDropPixels(
            pixelX,
            pixelY,
            stacks.Count,
            (tileX, tileY) => IsTileFree(mapId, tileX, tileY));
        var dropped = new List<GroundItemRecord>(stacks.Count);
        for (var i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (stack.Quantity <= 0 || stack.ItemId == Guid.Empty)
            {
                continue;
            }

            var published = await _items.LoadPublishedByIdAsync(stack.ItemId, cancellationToken).ConfigureAwait(false);
            if (published is null)
            {
                continue;
            }

            var quantity = Math.Min(stack.Quantity, Math.Max(1, published.MaxStack));
            var spot = spots[i];
            var result = await _ground.DropAsync(
                mapId,
                spot.PixelX,
                spot.PixelY,
                stack.ItemId,
                quantity,
                ownerCharacterId,
                cancellationToken).ConfigureAwait(false);
            if (result.Status == GroundItemMutationStatus.Ok && result.Item is not null)
            {
                dropped.Add(result.Item);
            }
        }

        return dropped;
    }

    private bool IsTileFree(int mapId, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0)
        {
            return false;
        }

        var bounds = _mapBounds(mapId);
        if (bounds.Found && (tileX >= bounds.Width || tileY >= bounds.Height))
        {
            return false;
        }

        return !_isTileBlocked(mapId, tileX, tileY);
    }
}
