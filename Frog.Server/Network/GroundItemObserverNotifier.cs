using Frog.Application.Gameplay;
using Frog.Core.Protocol;
using Frog.Server.Services;

namespace Frog.Server.Network;

/// <summary>Pousse <see cref="Frog.Core.Enums.PacketId.GroundItemsSnapshot"/> à tous les joueurs de la carte.</summary>
public sealed class GroundItemObserverNotifier(
    IGroundItemRepository groundItems,
    ConnectionManager connections,
    ClientRegistry clients,
    PacketSender sender)
{
    private readonly IGroundItemRepository _groundItems = groundItems;
    private readonly ConnectionManager _connections = connections;
    private readonly ClientRegistry _clients = clients;
    private readonly PacketSender _sender = sender;

    public async Task BroadcastAsync(int mapId, CancellationToken cancellationToken = default)
    {
        var items = await _groundItems.ListOnMapAsync(mapId, cancellationToken).ConfigureAwait(false);
        var wire = items.Select(item => new GroundItemWire
        {
            GroundItemId = item.Id,
            ItemId = item.ItemId,
            Quantity = item.Quantity,
            PixelX = item.PixelX,
            PixelY = item.PixelY,
        }).ToArray();

        foreach (var session in _connections.GetActiveSessions())
        {
            if (session.CurrentMapId != mapId)
            {
                continue;
            }

            if (_clients.TryGet(session.Id, out var client) && client is not null)
            {
                await _sender.SendGroundItemsSnapshotAsync(client, mapId, wire, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
