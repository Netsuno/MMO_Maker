using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Public TCP proof: server route tick broadcasts <see cref="PacketId.MapEventsResult"/>
/// to every occupant, and display / collision / interaction follow A→B together.
/// </summary>
public sealed class MapEventMovementBroadcastTests
{
    private const int MapId = 1;
    private const int BlockStartX = 4;
    private const int BlockStartY = 0;
    private const int BlockEndX = 4;
    private const int BlockEndY = 1;
    private const int InteractStartX = 6;
    private const int InteractStartY = 0;
    private const int InteractEndX = 6;
    private const int InteractEndY = 1;
    private const long BlockPlacementId = 101;
    private const long InteractPlacementId = 102;
    private const string InteractDisplayName = "Route Interact";
    private const string InteractSlug = "route-interact";

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Heartbeat_BroadcastsMovedEventToAllClients_DisplayCollisionAndInteractionFollowAtoB()
    {
        var store = new ScriptedMapEventStore(CreatePlacements());
        var port = GetFreePort();
        using var host = FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                    services.AddSingleton<IMapEventStore>(store);
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                });
            })
            .Build();
        await host.StartAsync();
        try
        {
            await using var ticker = new Phase7InMemorySmokeE2ETests.Phase7TcpClient();
            await using var observer = new Phase7InMemorySmokeE2ETests.Phase7TcpClient();
            await EnterPlayingAsync(ticker, port, "TkA", "Ticker");
            await EnterPlayingAsync(observer, port, "ObB", "Observer");

            var tickerStart = await RequestMapEventsAsync(ticker);
            var observerStart = await RequestMapEventsAsync(observer);
            AssertPlacementAt(tickerStart, BlockPlacementId, BlockStartX, BlockStartY);
            AssertPlacementAt(observerStart, BlockPlacementId, BlockStartX, BlockStartY);
            AssertPlacementAt(tickerStart, InteractPlacementId, InteractStartX, InteractStartY);
            AssertPlacementAt(observerStart, InteractPlacementId, InteractStartX, InteractStartY);

            await TeleportToTileAsync(ticker, InteractStartX, InteractStartY);
            await ticker.SendFrameAsync([(byte)PacketId.InteractRequest]);
            var interactAtA = await ticker.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(TryDecodeStatus(interactAtA, PacketId.InteractResult, out var interactAOk, out var interactAMsg));
            Assert.True(interactAOk);
            Assert.Contains(InteractDisplayName, interactAMsg, StringComparison.Ordinal);
            Assert.Contains(InteractSlug, interactAMsg, StringComparison.Ordinal);

            await TeleportToTileAsync(ticker, 1, 2);
            await TeleportToTileAsync(observer, 2, 2);

            await ticker.SendFrameAsync([(byte)PacketId.HeartbeatRequest]);
            _ = await ticker.ReadUntilAsync(PacketId.HeartbeatAck);

            IReadOnlyList<MapEventWireEntry> tickerMoved;
            IReadOnlyList<MapEventWireEntry> observerMoved;
            try
            {
                tickerMoved = await ReadMapEventsAsync(ticker, TimeSpan.FromSeconds(8));
                observerMoved = await ReadMapEventsAsync(observer, TimeSpan.FromSeconds(8));
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    "expected unsolicited MapEventsResult on ticker and observer after route tick (display A→B broadcast)",
                    ex);
            }

            AssertPlacementAt(tickerMoved, BlockPlacementId, BlockEndX, BlockEndY);
            AssertPlacementAt(observerMoved, BlockPlacementId, BlockEndX, BlockEndY);
            AssertPlacementAt(tickerMoved, InteractPlacementId, InteractEndX, InteractEndY);
            AssertPlacementAt(observerMoved, InteractPlacementId, InteractEndX, InteractEndY);

            var blocked = await TryMoveToTileExpectingErrorAsync(observer, BlockEndX, BlockEndY);
            Assert.True(TryDecodeError(blocked, out var blockMsg), "collision at B must return Error");
            Assert.Contains("evenement", blockMsg, StringComparison.OrdinalIgnoreCase);

            await TeleportToTileAsync(observer, BlockStartX, BlockStartY);

            await TeleportToTileAsync(ticker, InteractEndX, InteractEndY);
            await ticker.SendFrameAsync([(byte)PacketId.InteractRequest]);
            var interactAtB = await ticker.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(TryDecodeStatus(interactAtB, PacketId.InteractResult, out var interactBOk, out var interactBMsg));
            Assert.True(interactBOk, "interaction must follow the event to B");
            Assert.Contains(InteractDisplayName, interactBMsg, StringComparison.Ordinal);

            await TeleportToTileAsync(ticker, InteractStartX, InteractStartY);
            await ticker.SendFrameAsync([(byte)PacketId.InteractRequest]);
            var interactLeftA = await ticker.ReadUntilAsync(PacketId.InteractResult);
            Assert.True(TryDecodeStatus(interactLeftA, PacketId.InteractResult, out var leftAOk, out var leftAMsg));
            Assert.False(leftAOk);
            Assert.Contains("interagir", leftAMsg, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IReadOnlyList<MapEventWireEntry> CreatePlacements() =>
    [
        new()
        {
            PlacementId = BlockPlacementId,
            CatalogId = 8106,
            Slug = "route-block",
            DisplayName = "Route Blocker",
            TileX = BlockStartX,
            TileY = BlockStartY,
            TriggerKind = MapEventTriggerKinds.Interact,
            MovementKind = MapEventMovementKinds.Route,
            RouteWaypoints =
            [
                new MapEventRouteWaypoint { TileX = BlockStartX, TileY = BlockStartY, WaitMs = 250 },
                new MapEventRouteWaypoint { TileX = BlockEndX, TileY = BlockEndY, WaitMs = 30_000 },
            ],
            BlocksCollision = true,
        },
        new()
        {
            PlacementId = InteractPlacementId,
            CatalogId = 8107,
            Slug = InteractSlug,
            DisplayName = InteractDisplayName,
            TileX = InteractStartX,
            TileY = InteractStartY,
            TriggerKind = MapEventTriggerKinds.Interact,
            MovementKind = MapEventMovementKinds.Route,
            RouteWaypoints =
            [
                new MapEventRouteWaypoint { TileX = InteractStartX, TileY = InteractStartY, WaitMs = 250 },
                new MapEventRouteWaypoint { TileX = InteractEndX, TileY = InteractEndY, WaitMs = 30_000 },
            ],
            BlocksCollision = false,
        },
    ];

    private static async Task EnterPlayingAsync(
        Phase7InMemorySmokeE2ETests.Phase7TcpClient tcp,
        int port,
        string userPrefix,
        string charName)
    {
        var user = $"{userPrefix}-{Guid.NewGuid():N}"[..16];
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(BuildRegister(user, "password123"));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.RegisterResult))[1]);
        await tcp.SendFrameAsync(BuildLogin(user, "password123"));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.LoginResult))[1]);
        await tcp.DrainPendingAsync();
        await tcp.SendFrameAsync(BuildCharacterCreate(charName, Phase7ContentSeed.DefaultClassId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        Assert.NotEqual(0, create[1]);
        var characterId = Encoding.UTF8.GetString(create, 3, create[2]);
        await tcp.SendFrameAsync(BuildCharacterSelect(characterId));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
        await tcp.DrainPendingAsync(TimeSpan.FromMilliseconds(400));
    }

    private static async Task<IReadOnlyList<MapEventWireEntry>> RequestMapEventsAsync(
        Phase7InMemorySmokeE2ETests.Phase7TcpClient tcp)
    {
        await tcp.SendFrameAsync([(byte)PacketId.MapEventsRequest]);
        return await ReadMapEventsAsync(tcp, TimeSpan.FromSeconds(8));
    }

    private static async Task<IReadOnlyList<MapEventWireEntry>> ReadMapEventsAsync(
        Phase7InMemorySmokeE2ETests.Phase7TcpClient tcp,
        TimeSpan timeout)
    {
        byte[] frame;
        try
        {
            frame = await tcp.ReadUntilAsync(PacketId.MapEventsResult, timeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"expected MapEventsResult within {timeout.TotalSeconds:0}s", ex);
        }

        Assert.True(TryDecodeMapEventsResult(frame, out var mapId, out var placements));
        Assert.Equal(MapId, mapId);
        return placements;
    }

    private static void AssertPlacementAt(
        IReadOnlyList<MapEventWireEntry> placements,
        long placementId,
        int tileX,
        int tileY)
    {
        var match = placements.SingleOrDefault(p => p.PlacementId == placementId);
        Assert.NotNull(match);
        Assert.Equal(tileX, match!.TileX);
        Assert.Equal(tileY, match.TileY);
    }

    private static async Task TeleportToTileAsync(
        Phase7InMemorySmokeE2ETests.Phase7TcpClient client,
        int targetX,
        int targetY)
    {
        var (targetPx, targetPy) = WorldMetrics.TileCenterToPixels(targetX, targetY);
        const int maxHops = 8;
        for (var hop = 0; hop < maxHops; hop++)
        {
            await client.SendFrameAsync([(byte)PacketId.MapRequest]);
            _ = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);
            await Task.Delay(1100);
            await client.SendFrameAsync(BuildPositionSync(targetPx, targetPy));
            var moveResult = await client.ReadUntilAnyAsync(
                [PacketId.PositionUpdate, PacketId.Error],
                TimeSpan.FromSeconds(5));
            if (moveResult[0] == (byte)PacketId.Error && TryDecodeError(moveResult, out var moveError))
            {
                throw new InvalidOperationException($"PositionSync to ({targetX},{targetY}) failed: {moveError}");
            }

            if (moveResult[0] == (byte)PacketId.PositionUpdate
                && TryDecodePositionUpdate(moveResult, out _, out _, out var px, out var py))
            {
                var tileX = px / WorldMetrics.DefaultTileSizePixels;
                var tileY = py / WorldMetrics.DefaultTileSizePixels;
                if (tileX == targetX && tileY == targetY)
                {
                    await client.DrainPendingAsync(TimeSpan.FromMilliseconds(150));
                    return;
                }
            }
        }

        throw new TimeoutException($"failed to reach tile ({targetX},{targetY}) via PositionSync");
    }

    private static async Task<byte[]> TryMoveToTileExpectingErrorAsync(
        Phase7InMemorySmokeE2ETests.Phase7TcpClient client,
        int targetX,
        int targetY)
    {
        await client.SendFrameAsync([(byte)PacketId.MapRequest]);
        _ = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);
        var (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(targetX, targetY);
        await Task.Delay(1100);
        await client.SendFrameAsync(BuildPositionSync(pixelX, pixelY));
        return await client.ReadUntilAsync(PacketId.Error, TimeSpan.FromSeconds(5));
    }

    private static byte[] BuildRegister(string user, string pass) =>
        BuildLogin(user, pass, PacketId.RegisterRequest);

    private static byte[] BuildLogin(string user, string pass, PacketId id = PacketId.LoginRequest)
    {
        var u = Encoding.UTF8.GetBytes(user);
        var p = Encoding.UTF8.GetBytes(pass);
        var payload = new byte[1 + 1 + u.Length + 1 + p.Length];
        payload[0] = (byte)id;
        payload[1] = (byte)u.Length;
        u.CopyTo(payload, 2);
        payload[2 + u.Length] = (byte)p.Length;
        p.CopyTo(payload, 3 + u.Length);
        return payload;
    }

    private static byte[] BuildCharacterCreate(string name, Guid classId)
    {
        var n = Encoding.UTF8.GetBytes(name);
        var payload = new byte[1 + 1 + n.Length + 16];
        payload[0] = (byte)PacketId.CharacterCreateRequest;
        payload[1] = (byte)n.Length;
        n.CopyTo(payload, 2);
        classId.TryWriteBytes(payload.AsSpan(2 + n.Length));
        return payload;
    }

    private static byte[] BuildCharacterSelect(string id)
    {
        var b = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildPositionSync(int px, int py)
    {
        var payload = new byte[1 + 8];
        payload[0] = (byte)PacketId.PositionSyncRequest;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), px);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(5), py);
        return payload;
    }

    private static bool TryDecodeMapEventsResult(
        ReadOnlySpan<byte> payload,
        out int mapId,
        out IReadOnlyList<MapEventWireEntry> placements)
    {
        mapId = 0;
        placements = Array.Empty<MapEventWireEntry>();
        if (payload.Length < 7 || payload[0] != (byte)PacketId.MapEventsResult)
        {
            return false;
        }

        mapId = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1));
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(5));
        if (payload.Length != 7 + len)
        {
            return false;
        }

        var json = Encoding.UTF8.GetString(payload.Slice(7, len));
        var parsed = JsonSerializer.Deserialize<List<MapEventWireEntry>>(json);
        if (parsed is null)
        {
            return false;
        }

        placements = parsed;
        return true;
    }

    private static bool TryDecodeStatus(byte[] payload, PacketId expected, out bool success, out string message)
    {
        success = false;
        message = string.Empty;
        if (payload.Length < 3 || payload[0] != (byte)expected)
        {
            return false;
        }

        success = payload[1] != 0;
        var len = payload[2];
        if (payload.Length != 3 + len)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(payload, 3, len);
        return true;
    }

    private static bool TryDecodeError(ReadOnlySpan<byte> payload, out string message)
    {
        message = string.Empty;
        if (payload.Length < 2 || payload[0] != (byte)PacketId.Error)
        {
            return false;
        }

        var len = payload[1];
        if (payload.Length != 2 + len)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(payload.Slice(2, len));
        return true;
    }

    private static bool TryDecodePositionUpdate(
        ReadOnlySpan<byte> payload,
        out string username,
        out int mapId,
        out int pixelX,
        out int pixelY)
    {
        username = string.Empty;
        mapId = pixelX = pixelY = 0;
        if (payload.Length < 2 || payload[0] != (byte)PacketId.PositionUpdate)
        {
            return false;
        }

        var ulen = payload[1];
        if (payload.Length < 2 + ulen + 12)
        {
            return false;
        }

        username = Encoding.UTF8.GetString(payload.Slice(2, ulen));
        var o = 2 + ulen;
        mapId = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o));
        pixelX = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o + 4));
        pixelY = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o + 8));
        return true;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class ScriptedMapEventStore(IReadOnlyList<MapEventWireEntry> placements) : IMapEventStore
    {
        public bool TryGetEventsWireJson(int mapId, out string json)
        {
            json = mapId == MapId ? JsonSerializer.Serialize(placements) : "[]";
            return true;
        }

        public bool TryGetPlacements(int mapId, out IReadOnlyList<MapEventWireEntry> result)
        {
            result = mapId == MapId ? placements : Array.Empty<MapEventWireEntry>();
            return true;
        }

        public Task<(bool Ok, IReadOnlyList<MapEventWireEntry> Placements)> GetPlacementsAsync(
            int mapId,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            IReadOnlyList<MapEventWireEntry> result = mapId == MapId ? placements : Array.Empty<MapEventWireEntry>();
            return Task.FromResult((true, result));
        }

        public void InvalidateAll()
        {
        }
    }
}
