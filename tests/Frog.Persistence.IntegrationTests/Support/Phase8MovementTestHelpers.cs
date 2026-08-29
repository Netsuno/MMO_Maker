using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;

namespace Frog.Persistence.IntegrationTests.Support;

internal static class Phase8MovementTestHelpers
{
    public static async Task TeleportToTileAsync(
        Phase7TcpTestClient client,
        int targetX,
        int targetY,
        bool drainSideEffects = true)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMapRequest());
        _ = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);

        var (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(targetX, targetY);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildPositionSync(pixelX, pixelY));
        _ = await client.ReadUntilAsync(PacketId.PositionUpdate);
        if (drainSideEffects)
        {
            await client.DrainPendingAsync(TimeSpan.FromMilliseconds(150));
        }
    }

    /// <summary>
    /// Teleports onto a player_contact tile and waits for InventorySnapshot (do not drain side effects).
    /// </summary>
    public static async Task<byte[]> TeleportOntoContactAndReadInventoryAsync(
        Phase7TcpTestClient client,
        int targetX,
        int targetY)
    {
        await TeleportToTileAsync(client, targetX, targetY, drainSideEffects: false);
        return await client.ReadUntilAsync(PacketId.InventorySnapshot, TimeSpan.FromSeconds(5));
    }
}
