using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Config;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// E2E TCP réel : login → spawn → block → move → warp A→B → warp B→C → map request → disconnect → shutdown.
/// Aucun appel direct à MovementService pour les assertions.
/// </summary>
[Collection(PlaytestProcessCollectionDefinition.Name)]
public sealed class PlaytestE2ETests
{
    [Fact]
    public async Task PlaytestHost_TcpMovementCollision_TwoWarps_AndCleanShutdown()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);

        var mapC = CreateOpenMap("C", 8, 8);
        var saveC = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = mapC,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));

        var mapB = CreateOpenMap("B", 8, 8);
        SetWarp(mapB, 1, 0, saveC.MapId, 2, 2);
        var saveB = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = mapB,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));

        var mapA = CreateOpenMap("A", 8, 8);
        SetBlock(mapA, 0, 1);
        SetWarp(mapA, 1, 0, saveB.MapId, 0, 0);
        var saveA = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = mapA,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(saveA.MapId));

        var preparer = new PlaytestMapPreparer(repo);
        var port = GetFreePort();
        var prepared = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 0,
                SpawnTileY = 0,
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = false,
            });
        var plan = Assert.IsType<PlaytestPreparationResult.Success>(prepared).Plan;
        Assert.Equal(3, plan.Maps.Count);
        var runtimeA = plan.Maps.Single(m => m.Name == "A").RuntimeMapId;
        var runtimeB = plan.Maps.Single(m => m.Name == "B").RuntimeMapId;
        var runtimeC = plan.Maps.Single(m => m.Name == "C").RuntimeMapId;
        Assert.Equal(1, runtimeA);
        Assert.NotEqual(runtimeB, runtimeC);

        var playtestOpts = FrogServerHostFactory.CreatePlaytestOptionsFromPlan(plan);
        using var host = FrogServerHostFactory.Create(playtestOpts);
        await host.StartAsync();

        try
        {
            await using var tcp = new PlaytestTcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            var hello = await tcp.ReadFrameAsync();
            Assert.Equal((byte)PacketId.Hello, hello[0]);
            Assert.True(WireHello.TryParse(hello, out _, out var ver));
            Assert.Equal(FrogWireProtocol.Version, ver);

            await tcp.SendFrameAsync(BuildLogin("demo", "demo"));
            Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.LoginResult))[1]);

            var spawn = await tcp.ReadUntilAsync(PacketId.PositionUpdate, TimeSpan.FromSeconds(5));
            ParsePositionUpdate(spawn, out _, out var mapId, out var px, out var py);
            Assert.Equal(runtimeA, mapId);
            var (sx, sy) = WorldMetrics.TileCenterToPixels(0, 0);
            Assert.Equal(sx, px);
            Assert.Equal(sy, py);

            // Rejected movement into blocked tile (0,1) before leaving spawn column.
            var blocked = false;
            for (var i = 1; i <= 24 && !blocked; i++)
            {
                var stepY = sy + i * 8;
                await tcp.SendFrameAsync(BuildPositionSync(sx, stepY));
                await Task.Delay(80);
                foreach (var f in await tcp.DrainFramesAsync(TimeSpan.FromMilliseconds(120)))
                {
                    if (f[0] == (byte)PacketId.Error)
                    {
                        blocked = true;
                    }
                    else if (f[0] == (byte)PacketId.PositionUpdate)
                    {
                        ParsePositionUpdate(f, out _, out _, out px, out py);
                    }
                }
            }

            Assert.True(blocked, "expected TCP Error when entering blocked tile");

            // Cool rate gate, then successful east movement.
            await Task.Delay(1100);
            var movedEast = false;
            for (var i = 0; i < 20 && !movedEast; i++)
            {
                await tcp.SendFrameAsync(BuildMove(1, 0));
                await Task.Delay(80);
                foreach (var f in await tcp.DrainFramesAsync(TimeSpan.FromMilliseconds(150)))
                {
                    if (f[0] == (byte)PacketId.PositionUpdate)
                    {
                        ParsePositionUpdate(f, out _, out mapId, out var nx, out py);
                        if (nx > px)
                        {
                            movedEast = true;
                            px = nx;
                        }
                    }
                }
            }

            Assert.True(movedEast, "expected TCP PositionUpdate after successful MoveRequest");

            // First warp A→B.
            var onB = await StepUntilMapIdAsync(tcp, runtimeB, moveDx: 1, moveDy: 0, TimeSpan.FromSeconds(10));
            Assert.True(onB, $"expected warp A→B via TCP PositionUpdate mapId={runtimeB}");

            await tcp.SendFrameAsync(new byte[] { (byte)PacketId.MapRequest });
            var mapDataB = await tcp.ReadUntilAnyAsync(
                [PacketId.MapData, PacketId.MapAlreadySynced],
                TimeSpan.FromSeconds(5));
            Assert.True(mapDataB[0] is (byte)PacketId.MapData or (byte)PacketId.MapAlreadySynced);

            await Task.Delay(1100);

            // Second consecutive warp B→C (landed at 0,0; warp at 1,0).
            var onC = await StepUntilMapIdAsync(tcp, runtimeC, moveDx: 1, moveDy: 0, TimeSpan.FromSeconds(10));
            Assert.True(onC, $"expected second warp B→C via TCP mapId={runtimeC}");

            await tcp.SendFrameAsync(new byte[] { (byte)PacketId.MapRequest });
            var mapDataC = await tcp.ReadUntilAnyAsync(
                [PacketId.MapData, PacketId.MapAlreadySynced],
                TimeSpan.FromSeconds(5));
            Assert.True(mapDataC[0] is (byte)PacketId.MapData or (byte)PacketId.MapAlreadySynced);

            await tcp.DisconnectAsync();
        }
        finally
        {
            await host.StopAsync();
            try
            {
                PlaytestWorkspacePaths.TryDeleteOwnedWorkspace(
                    plan.WorkDirectory,
                    plan.CorrelationId,
                    out _);
            }
            catch
            {
                // best-effort
            }
        }

        Assert.False(await IsPortOpenAsync("127.0.0.1", port));
    }

    private static async Task<bool> StepUntilMapIdAsync(
        PlaytestTcpClient tcp,
        int expectedMapId,
        sbyte moveDx,
        sbyte moveDy,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await tcp.SendFrameAsync(BuildMove(moveDx, moveDy));
            await Task.Delay(80);
            foreach (var f in await tcp.DrainFramesAsync(TimeSpan.FromMilliseconds(200)))
            {
                if (f[0] == (byte)PacketId.PositionUpdate)
                {
                    ParsePositionUpdate(f, out _, out var mid, out _, out _);
                    if (mid == expectedMapId)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Map CreateOpenMap(string name, int w, int h)
    {
        var map = new Map { Name = name, Width = w, Height = h };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                ground.Tiles.Add(new Tile { X = x, Y = y, TilesetId = 1, Type = TileType.Ground });
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private static void SetBlock(Map map, int x, int y)
    {
        var t = map.Layers[0].Tiles.First(tile => tile.X == x && tile.Y == y);
        t.Type = TileType.Block;
    }

    private static void SetWarp(Map map, int x, int y, Guid target, int tx, int ty)
    {
        var t = map.Layers[0].Tiles.First(tile => tile.X == x && tile.Y == y);
        t.Type = TileType.Warp;
        t.WarpTargetMapId = target;
        t.WarpTargetX = tx;
        t.WarpTargetY = ty;
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static async Task<bool> IsPortOpenAsync(string host, int port)
    {
        try
        {
            using var c = new TcpClient();
            using var cts = new CancellationTokenSource(200);
            await c.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] BuildLogin(string user, string pass)
    {
        var ub = Encoding.UTF8.GetBytes(user);
        var pb = Encoding.UTF8.GetBytes(pass);
        var payload = new byte[1 + 1 + ub.Length + 1 + pb.Length];
        payload[0] = (byte)PacketId.LoginRequest;
        payload[1] = (byte)ub.Length;
        ub.CopyTo(payload, 2);
        payload[2 + ub.Length] = (byte)pb.Length;
        pb.CopyTo(payload, 3 + ub.Length);
        return payload;
    }

    private static byte[] BuildMove(sbyte dx, sbyte dy)
        => [(byte)PacketId.MoveRequest, unchecked((byte)dx), unchecked((byte)dy)];

    private static byte[] BuildPositionSync(int px, int py)
    {
        var payload = new byte[1 + 8];
        payload[0] = (byte)PacketId.PositionSyncRequest;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), px);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(5), py);
        return payload;
    }

    private static void ParsePositionUpdate(byte[] frame, out string username, out int mapId, out int px, out int py)
    {
        Assert.Equal((byte)PacketId.PositionUpdate, frame[0]);
        var ulen = frame[1];
        username = Encoding.UTF8.GetString(frame, 2, ulen);
        var o = 2 + ulen;
        mapId = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(o));
        px = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(o + 4));
        py = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(o + 8));
    }

    internal sealed class PlaytestTcpClient : IAsyncDisposable
    {
        private TcpClient? _tcp;
        private NetworkStream? _stream;

        public async Task ConnectAsync(string host, int port)
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync(host, port);
            _stream = _tcp.GetStream();
        }

        public async Task SendFrameAsync(byte[] payload)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await _stream!.WriteAsync(frame);
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
        {
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            Assert.InRange(len, 1, 1024 * 1024);
            var payload = new byte[len];
            await ReadExactAsync(payload, cts.Token);
            return payload;
        }

        public async Task<byte[]?> TryReadFrameAsync(TimeSpan timeout)
        {
            try
            {
                return await ReadFrameAsync(timeout);
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<byte[]>> DrainFramesAsync(TimeSpan budget)
        {
            var frames = new List<byte[]>();
            var deadline = DateTime.UtcNow + budget;
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var frame = await TryReadFrameAsync(remaining);
                if (frame is null)
                {
                    break;
                }

                frames.Add(frame);
            }

            return frames;
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
            => await ReadUntilAnyAsync([id], timeout);

        public async Task<byte[]> ReadUntilAnyAsync(PacketId[] ids, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var frame = await ReadFrameAsync(remaining);
                if (ids.Any(id => frame[0] == (byte)id))
                {
                    return frame;
                }
            }

            throw new TimeoutException("expected packet not received");
        }

        public Task DisconnectAsync()
        {
            _tcp?.Close();
            return Task.CompletedTask;
        }

        private async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
                if (n == 0)
                {
                    throw new EndOfStreamException();
                }

                read += n;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            _stream?.Dispose();
            _tcp?.Dispose();
        }
    }
}
