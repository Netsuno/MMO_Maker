using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;

namespace Frog.Persistence.IntegrationTests.Support;

internal static class Phase8MovementTestHelpers
{
    public static async Task SendHeartbeatAsync(Phase7TcpTestClient client)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildHeartbeat());
        _ = await client.ReadUntilAsync(PacketId.HeartbeatAck);
    }

    public static async Task SendHeartbeatAndDrainAsync(Phase7TcpTestClient client)
    {
        await SendHeartbeatAsync(client);
        await client.DrainPendingAsync(TimeSpan.FromMilliseconds(150));
    }

    public static async Task<byte[]> TryMoveToTileExpectingErrorAsync(
        Phase7TcpTestClient client,
        int targetX,
        int targetY)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMapRequest());
        _ = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);

        var (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(targetX, targetY);
        await Task.Delay(1100);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildPositionSync(pixelX, pixelY));
        return await client.ReadUntilAsync(PacketId.Error, TimeSpan.FromSeconds(5));
    }

    public static async Task TeleportToTileAsync(
        Phase7TcpTestClient client,
        int targetX,
        int targetY,
        bool drainSideEffects = true)
    {
        var (targetPx, targetPy) = WorldMetrics.TileCenterToPixels(targetX, targetY);
        // Server anti-cheat caps per-sync travel (~228 px). Repeat hops until we reach the tile.
        const int maxHops = 8;
        for (var hop = 0; hop < maxHops; hop++)
        {
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMapRequest());
            _ = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);

            await Task.Delay(1100);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildPositionSync(targetPx, targetPy));
            var moveResult = await client.ReadUntilAnyAsync(
                [PacketId.PositionUpdate, PacketId.Error],
                TimeSpan.FromSeconds(5));
            if (moveResult[0] == (byte)PacketId.Error &&
                Phase8WireDecoders.TryDecodeError(moveResult, out var moveError))
            {
                throw new InvalidOperationException($"PositionSync to ({targetX},{targetY}) failed: {moveError}");
            }

            if (moveResult[0] == (byte)PacketId.PositionUpdate
                && Phase7WireDecoders.TryDecodePositionUpdate(moveResult, out _, out _, out var px, out var py))
            {
                var tileX = px / WorldMetrics.DefaultTileSizePixels;
                var tileY = py / WorldMetrics.DefaultTileSizePixels;
                if (tileX == targetX && tileY == targetY)
                {
                    break;
                }
            }
        }

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
