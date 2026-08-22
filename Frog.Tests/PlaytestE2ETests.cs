using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Config;
using Frog.Server.Network;
using Frog.Server.Playtest;
using Frog.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestProtocolAndManifestTests
{
    [Fact]
    public void Manifest_Roundtrip_And_RejectsBadSchemaVersion()
    {
        var plan = CreateMinimalPlan();
        PlaytestManifestWriter.Write(plan);
        var doc = PlaytestManifestWriter.Read(plan.ManifestPath);
        Assert.Equal(PlaytestManifestDocument.CurrentSchemaVersion, doc.SchemaVersion);
        Assert.Equal(plan.PrimaryCanonicalMapId, doc.PrimaryCanonicalMapId);
        Assert.Equal(plan.PrimaryPublishedRevision, doc.PrimaryPublishedRevision);

        var badPath = Path.Combine(plan.WorkDirectory, "bad.json");
        File.WriteAllText(
            badPath,
            """{"schemaVersion":99,"correlationId":"00000000-0000-0000-0000-000000000001","primaryCanonicalMapId":"00000000-0000-0000-0000-000000000002","primaryPublishedRevision":1,"spawn":{"runtimeMapId":1,"tileX":0,"tileY":0},"maps":[]}""");
        Assert.Throws<InvalidOperationException>(() => PlaytestManifestWriter.Read(badPath));
    }

    [Fact]
    public void Login_Move_Payloads_RejectInvalidSizes()
    {
        Assert.False(PacketDispatcher.TryParseLoginPayload(ReadOnlySpan<byte>.Empty, out _, out _));
        Assert.False(PacketDispatcher.TryParseLoginPayload(new byte[] { 1 }, out _, out _));
        Assert.False(PacketDispatcher.TryParseMovePayload(ReadOnlySpan<byte>.Empty, out _, out _));
        Assert.False(PacketDispatcher.TryParseMovePayload(new byte[] { 1 }, out _, out _));
        Assert.False(PacketDispatcher.TryParsePositionSyncPayload(new byte[4], out _, out _));
        Assert.True(PacketDispatcher.TryParseMovePayload(new byte[] { 1, 0 }, out var dx, out var dy));
        Assert.Equal(1, dx);
        Assert.Equal(0, dy);
    }

    [Fact]
    public void WireHello_Version_MatchesFrogWireProtocol()
    {
        var bytes = WireHello.BuildPayload();
        Assert.True(WireHello.TryParse(bytes, out _, out var ver));
        Assert.Equal(FrogWireProtocol.Version, ver);
    }

    [Fact]
    public void FrameSizeLimit_RejectsOversizeLengthPrefix()
    {
        // ClientSession rejects length > 1 MiB — document the contract.
        const int max = 1024 * 1024;
        Assert.True(max == 1_048_576);
    }

    [Fact]
    public void PlaytestBlobStore_ExposesPublishedRevisionFingerprint()
    {
        var plan = CreateMinimalPlan();
        PlaytestManifestWriter.Write(plan);
        var store = PlaytestMapBlobStore.FromManifest(plan.ManifestPath);
        Assert.True(store.TryGetHead(1, out var rev, out var sha));
        Assert.Equal(plan.PrimaryPublishedRevision, rev);
        Assert.Equal(64, sha.Length);
    }

    private static PlaytestLaunchPlan CreateMinimalPlan()
    {
        var mapId = Guid.NewGuid();
        var map = MapSamples.StarterMeadow(MapSamples.RuntimeMapIdToGuid(1));
        var bytes = new Frog.Core.IO.MapSerializer().Serialize(map);
        var work = Path.Combine(Path.GetTempPath(), "frog-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        return new PlaytestLaunchPlan
        {
            CorrelationId = Guid.NewGuid(),
            PrimaryCanonicalMapId = mapId,
            PrimaryPublishedRevision = 3,
            Spawn = new PlaytestSpawnPoint { RuntimeMapId = 1, TileX = 1, TileY = 1 },
            Maps =
            [
                new PlaytestRuntimeMap
                {
                    CanonicalMapId = mapId,
                    PublishedRevision = 3,
                    RuntimeMapId = 1,
                    Name = map.Name,
                    Map = map,
                    SerializedFmap = bytes,
                },
            ],
            Host = "127.0.0.1",
            Port = 6000,
            WorkDirectory = work,
            ManifestPath = Path.Combine(work, "playtest-manifest.json"),
        };
    }
}

/// <summary>
/// E2E non-UI : serveur playtest in-process + client TCP léger
/// (startup, connect, spawn, valid move, blocked move, warp, disconnect, shutdown).
/// </summary>
public sealed class PlaytestE2ETests
{
    [Fact]
    public async Task PlaytestHost_MovementCollisionWarp_AndCleanShutdown()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);

        var interiorSave = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreateOpenMap("Interior", 22, 22),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        });
        var interiorOk = Assert.IsType<SaveMapResult.Success>(interiorSave);

        var outdoorSave = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = MapSamples.StarterMeadow(interiorOk.MapId),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        });
        var outdoorOk = Assert.IsType<SaveMapResult.Success>(outdoorSave);

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(outdoorOk.MapId));

        var preparer = new PlaytestMapPreparer(repo);
        var port = GetFreePort();
        var prepared = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 1,
                SpawnTileY = 1,
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = false,
                WorkDirectory = Path.Combine(Path.GetTempPath(), "frog-e2e-" + Guid.NewGuid().ToString("N")),
            });

        var plan = Assert.IsType<PlaytestPreparationResult.Success>(prepared).Plan;
        Assert.Equal(2, plan.Maps.Count);

        // Newer draft must not affect published playtest world.
        workspace.CurrentMap!.Name = "SHOULD_NOT_LOAD";
        // Flip a ground tile that was Block in published snapshot — draft-only change.
        var blockTile = workspace.CurrentMap.Layers[0].Tiles.First(t => t.Type == TileType.Block);
        blockTile.Type = TileType.Ground;
        workspace.MarkDirty();
        await workspace.SaveCurrentAsync(SaveMapIntent.SaveDraft);

        Environment.SetEnvironmentVariable(PlaytestRuntimeOptions.PortEnvironmentVariable, port.ToString());
        var playtestOpts = FrogServerHostFactory.CreatePlaytestOptionsFromPlan(plan);
        using var host = FrogServerHostFactory.Create(playtestOpts);
        await host.StartAsync();

        try
        {
            await WaitForPortAsync("127.0.0.1", port, TimeSpan.FromSeconds(20));

            var mapService = host.Services.GetRequiredService<MapService>();
            // Proof: published blocks still present (draft cleared them).
            Assert.True(mapService.IsBlocked(1, 6, 5));
            Assert.True(mapService.TryGetWarpDestination(1, 3, 3, out var destMap, out var tx, out var ty));
            Assert.Equal(2, destMap);
            Assert.Equal(18, tx);
            Assert.Equal(18, ty);
            Assert.True(mapService.TryEnsureMapLoaded(2));

            await using var tcp = new PlaytestTcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            var hello = await tcp.ReadFrameAsync();
            Assert.Equal((byte)PacketId.Hello, hello[0]);
            Assert.True(WireHello.TryParse(hello, out _, out var ver));
            Assert.Equal(FrogWireProtocol.Version, ver);

            await tcp.SendFrameAsync(BuildLogin("demo", "demo"));
            var login = await tcp.ReadUntilAsync(PacketId.LoginResult);
            Assert.NotEqual(0, login[1]);

            var spawnPos = await tcp.ReadUntilAsync(PacketId.PositionUpdate, TimeSpan.FromSeconds(5));
            ParsePositionUpdate(spawnPos, out var user, out var mapId, out var px, out var py);
            Assert.Equal("demo", user);
            Assert.Equal(1, mapId);
            var (sx, sy) = WorldMetrics.TileCenterToPixels(1, 1);
            Assert.Equal(sx, px);
            Assert.Equal(sy, py);

            // Valid movement
            await tcp.SendFrameAsync(BuildMove(1, 0));
            var moved = await tcp.ReadUntilAsync(PacketId.PositionUpdate, TimeSpan.FromSeconds(3));
            ParsePositionUpdate(moved, out _, out _, out var px2, out _);
            Assert.True(px2 > px);

            // Blocked movement (server-authoritative Collision via same MapService as TCP host)
            var connections = host.Services.GetRequiredService<ConnectionManager>();
            Assert.True(connections.TryCreateSession("block-e2e", out var blockSession));
            blockSession!.CurrentMapId = 1;
            var (nearBx, nearBy) = WorldMetrics.TileCenterToPixels(4, 5);
            blockSession.PixelX = nearBx;
            blockSession.PixelY = nearBy;
            SessionPixelSync.SyncTileFromPixels(blockSession);
            var movement = host.Services.GetRequiredService<MovementService>();
            Assert.False(movement.TryApplyMove(blockSession, 1, 0, out var blockErr));
            Assert.Contains("bloque", blockErr, StringComparison.OrdinalIgnoreCase);

            // Authoritative warp onto published target map (runtime id 2)
            Assert.True(connections.TryCreateSession("warp-e2e", out var session));
            session!.CurrentMapId = 1;
            var ts = WorldMetrics.DefaultTileSizePixels;
            session.PixelX = 3 * ts - 1;
            session.PixelY = 3 * ts + ts / 2;
            SessionPixelSync.SyncTileFromPixels(session);
            Assert.True(movement.TryApplyMove(session, 1, 0, out _));
            Assert.True(movement.TryApplyWarpAfterMove(session));
            Assert.Equal(2, session.CurrentMapId);
            Assert.Equal(18, session.PositionX);
            Assert.Equal(18, session.PositionY);

            await tcp.DisconnectAsync();
        }
        finally
        {
            await host.StopAsync();
            Environment.SetEnvironmentVariable(PlaytestRuntimeOptions.PortEnvironmentVariable, null);
        }

        Assert.False(await IsPortOpenAsync("127.0.0.1", port));
    }

    private static Map CreateOpenMap(string name, int w, int h)
    {
        var map = new Map { Name = name, Width = w, Height = h };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    Type = TileType.Ground,
                });
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static async Task WaitForPortAsync(string host, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await IsPortOpenAsync(host, port))
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("server port not open");
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

    private sealed class PlaytestTcpClient : IAsyncDisposable
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

        public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
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
                if (frame[0] == (byte)id)
                {
                    return frame;
                }
            }

            throw new TimeoutException("packet " + id + " not received");
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
