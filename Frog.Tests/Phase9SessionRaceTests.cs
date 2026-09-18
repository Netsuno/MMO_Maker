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
using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Core.Identity;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Gameplay;
using Frog.Server.Network;
using Frog.Server.Persistence;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class Phase9SessionRaceTests
{
    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(ModerationAction.Kick)]
    [InlineData(ModerationAction.Ban)]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_ModerationDuringPlayerPacket_ClosesTcpNotifiesAndCleansOnce(
        ModerationAction action)
    {
        var port = GetFreePort();
        var countingStore = new CountingPlayerStateStore();
        var countingCleanup = new CountingRuntimeCleanup();
        using var host = CreateInMemoryHost(port, countingStore, countingCleanup);
        await host.StartAsync();
        PacketDispatcher? dispatcher = null;
        SessionTeardown? teardown = null;
        try
        {
            var gm = UniqueUser("gm");
            var player = UniqueUser("pl");
            var other = UniqueUser("ot");
            const string password = "password123";

            await using var gmClient = new TcpProbe();
            await using var playerClient = new TcpProbe();
            await using var otherClient = new TcpProbe();

            await RegisterLoginSelectAsync(gmClient, port, gm, password, "GmHero");
            await RegisterLoginSelectAsync(playerClient, port, player, password, "PlHero");
            await RegisterLoginSelectAsync(otherClient, port, other, password, "OtHero");

            var accounts = host.Services.GetRequiredService<IAccountRepository>();
            var operators = host.Services.GetRequiredService<IOperatorDirectory>();
            var gmAccount = await accounts.FindByUsernameAsync(gm);
            Assert.NotNull(gmAccount);
            Assert.Equal(
                OperatorGrantStatus.Granted,
                (await operators.GrantAsync(gmAccount!.Id, "sql", "p9-c2 race")).Status);

            var connections = host.Services.GetRequiredService<ConnectionManager>();
            var clients = host.Services.GetRequiredService<ClientRegistry>();
            Assert.True(connections.TryGetSessionByUsername(player, out var live) && live is not null);
            var characterId = live!.CharacterId!;
            var characterGuid = live.CharacterGuid!.Value;
            var mapId = live.CurrentMapId;
            var pixelX = live.PixelX;
            var pixelY = live.PixelY;
            countingCleanup.Inner = host.Services.GetRequiredService<Phase8GameplayHandlers>();

            dispatcher = host.Services.GetRequiredService<PacketDispatcher>();
            teardown = host.Services.GetRequiredService<SessionTeardown>();

            var playerAtPacket = new ManualResetEventSlim(false);
            var packetMayObserveInactive = new ManualResetEventSlim(false);
            var packetSawInactive = new ManualResetEventSlim(false);
            Guid? playerConnectionId = null;

            dispatcher.BeforeTryGetActiveSession = client =>
            {
                if (playerConnectionId is null
                    && AccountUsername.Equals(client.Username, player))
                {
                    playerConnectionId = client.ConnectionId;
                    playerAtPacket.Set();
                    WaitGate(packetMayObserveInactive, "session dropped before packet lookup");
                }
            };
            dispatcher.AfterTryGetActiveSession = (client, active) =>
            {
                if (playerConnectionId is Guid id
                    && client.ConnectionId == id
                    && packetMayObserveInactive.IsSet
                    && !active)
                {
                    packetSawInactive.Set();
                }
            };
            teardown.AfterSessionRemovedBeforeClientLookup = _ =>
            {
                packetMayObserveInactive.Set();
                WaitGate(packetSawInactive, "in-flight packet unregistered inactive session");
            };

            var packetTask = playerClient.SendFrameAsync([(byte)PacketId.HeartbeatRequest]);
            WaitGate(playerAtPacket, "player packet paused");

            await gmClient.SendFrameAsync(BuildModerate(action, player, action == ModerationAction.Ban ? "cheat" : "afk"));
            var moderate = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
            Assert.True(TryDecodeStatus(moderate, out var ok, out _));
            Assert.True(ok);

            await packetTask;
            await ExpectSessionClosedAsync(playerClient);

            var leave = await otherClient.ReadUntilAsync(PacketId.PlayerLeave);
            Assert.Equal((byte)PacketId.PlayerLeave, leave[0]);
            Assert.Equal(player, Encoding.UTF8.GetString(leave, 2, leave[1]));

            Assert.False(connections.TryGetSessionByUsername(player, out _));
            Assert.DoesNotContain(
                clients.GetAllAuthenticatedClients(),
                c => AccountUsername.Equals(c.Username, player));
            Assert.Equal(1, countingCleanup.Cancels);
            Assert.Equal(1, countingStore.Upserts);
            Assert.True(countingStore.TryGetForCharacter(characterId, out var saved));
            Assert.Equal(mapId, saved.MapId);
            Assert.Equal(pixelX, saved.X);
            Assert.Equal(pixelY, saved.Y);
            Assert.True(characterGuid != Guid.Empty);

            if (action == ModerationAction.Ban)
            {
                await using var banned = new TcpProbe();
                await banned.ConnectAsync("127.0.0.1", port);
                _ = await banned.ReadFrameAsync();
                await banned.SendFrameAsync(BuildLogin(player, password));
                var login = await banned.ReadUntilAsync(PacketId.LoginResult);
                Assert.True(TryDecodeStatus(login, out var loginOk, out var loginMsg));
                Assert.False(loginOk);
                Assert.Equal(ModerationMessages.Banned, loginMsg);
            }
        }
        finally
        {
            ClearHooks(dispatcher, teardown);
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_SimultaneousSameAccountReconnects_OneLiveSessionNoOrphan()
    {
        var port = GetFreePort();
        var countingStore = new CountingPlayerStateStore();
        var countingCleanup = new CountingRuntimeCleanup();
        using var host = CreateInMemoryHost(port, countingStore, countingCleanup);
        await host.StartAsync();
        PacketDispatcher? dispatcher = null;
        try
        {
            var player = UniqueUser("pl");
            var observer = UniqueUser("ob");
            const string password = "password123";

            await using var original = new TcpProbe();
            await using var observerClient = new TcpProbe();
            var login = await RegisterLoginSelectAsync(original, port, player, password, "PlHero");
            await RegisterLoginSelectAsync(observerClient, port, observer, password, "ObHero");

            countingCleanup.Inner = host.Services.GetRequiredService<Phase8GameplayHandlers>();
            dispatcher = host.Services.GetRequiredService<PacketDispatcher>();
            var connections = host.Services.GetRequiredService<ConnectionManager>();
            var clients = host.Services.GetRequiredService<ClientRegistry>();

            using var rendezvous = new Barrier(2);
            dispatcher.BeforeSameAccountSessionReplace = () =>
            {
                if (!rendezvous.SignalAndWait(BarrierTimeout))
                {
                    throw new TimeoutException("timed out waiting for dual reconnect rendezvous");
                }
            };

            await using var reconnectA = new TcpProbe();
            await using var reconnectB = new TcpProbe();
            var attemptA = ReconnectAsync(reconnectA, port, login.Token);
            var attemptB = ReconnectAsync(reconnectB, port, login.Token);
            await Task.WhenAll(attemptA, attemptB);

            await ExpectSessionClosedAsync(original);

            Assert.True(connections.TryGetSessionByUsername(player, out var live) && live is not null);
            Assert.Equal(
                1,
                connections.GetActiveSessions().Count(s => AccountUsername.Equals(s.Username, player)));

            var bound = clients.GetAllAuthenticatedClients()
                .Where(c => AccountUsername.Equals(c.Username, player))
                .ToList();
            Assert.Single(bound);
            Assert.NotNull(bound[0].AuthenticatedSession);
            Assert.Equal(live!.Id, bound[0].AuthenticatedSession!.Id);

            var sessionIds = clients.GetAllAuthenticatedClients()
                .Select(c => c.AuthenticatedSession?.Id)
                .Where(id => id is not null)
                .Cast<Guid>()
                .ToList();
            Assert.Equal(sessionIds.Count, sessionIds.Distinct().Count());

            var stateA = await ProbeReconnectTcpAsync(reconnectA);
            var stateB = await ProbeReconnectTcpAsync(reconnectB);
            var states = new[] { stateA, stateB };
            Assert.Equal(1, states.Count(s => s == ReconnectTcpState.Alive));
            Assert.Equal(1, states.Count(s => s == ReconnectTcpState.Closed));
            Assert.DoesNotContain(ReconnectTcpState.OpenWithoutSession, states);

            Assert.True(countingCleanup.Cancels >= 1);
            Assert.True(countingStore.Upserts >= 1);

            try
            {
                _ = await observerClient.ReadUntilAsync(PacketId.PlayerLeave, TimeSpan.FromMilliseconds(250));
                Assert.Fail("reconnect displace must not broadcast PlayerLeave");
            }
            catch (TimeoutException)
            {
            }
        }
        finally
        {
            ClearHooks(dispatcher, teardown: null);
            await host.StopAsync();
        }
    }

    private static IHost CreateInMemoryHost(
        int port,
        CountingPlayerStateStore store,
        CountingRuntimeCleanup cleanup)
        => FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                    services.AddSingleton(store);
                    services.AddSingleton<IPlayerStateStore>(store);
                    services.AddSingleton(cleanup);
                    services.AddSingleton<ICharacterRuntimeCleanup>(cleanup);
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

    private static async Task<(string Token, byte[] LoginFrame)> RegisterAndLoginAsync(
        TcpProbe client,
        int port,
        string user,
        string password)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(BuildRegister(user, password));
        var reg = await client.ReadUntilAsync(PacketId.RegisterResult);
        Assert.True(TryDecodeStatus(reg, out var regOk, out _));
        Assert.True(regOk);
        await client.SendFrameAsync(BuildLogin(user, password));
        var login = await client.ReadUntilAsync(PacketId.LoginResult);
        Assert.True(TryDecodeStatus(login, out var loginOk, out var token));
        Assert.True(loginOk);
        await client.DrainPendingAsync();
        return (token, login);
    }

    private static async Task<(string Token, byte[] LoginFrame)> RegisterLoginSelectAsync(
        TcpProbe client,
        int port,
        string user,
        string password,
        string characterName)
    {
        var login = await RegisterAndLoginAsync(client, port, user, password);
        await client.SendFrameAsync(BuildCharacterCreate(characterName, Phase7ContentSeed.DefaultClassId));
        var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
        Assert.True(create.Length > 3 && create[1] != 0);
        var characterId = Encoding.UTF8.GetString(create, 3, create[2]);
        await client.SendFrameAsync(BuildCharacterSelect(characterId));
        var select = await client.ReadUntilAsync(PacketId.CharacterSelectResult);
        Assert.True(select.Length > 1 && select[1] != 0);
        await client.DrainPendingAsync();
        return login;
    }

    private static async Task ReconnectAsync(TcpProbe client, int port, string token)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(BuildReconnect(token));
        try
        {
            _ = await client.ReadUntilAsync(PacketId.ReconnectResult);
            await client.DrainPendingAsync();
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or SocketException)
        {
        }
    }

    private static async Task<ReconnectTcpState> ProbeReconnectTcpAsync(TcpProbe client)
    {
        try
        {
            await client.SendFrameAsync([(byte)PacketId.HeartbeatRequest]);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var frame = await client.ReadFrameAsync(remaining);
                if (frame.Length > 0 && frame[0] == (byte)PacketId.HeartbeatAck)
                {
                    return ReconnectTcpState.Alive;
                }
            }

            return ReconnectTcpState.OpenWithoutSession;
        }
        catch (TimeoutException)
        {
            return ReconnectTcpState.OpenWithoutSession;
        }
        catch (OperationCanceledException)
        {
            return ReconnectTcpState.OpenWithoutSession;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or SocketException)
        {
            return ReconnectTcpState.Closed;
        }
    }

    private static async Task ExpectSessionClosedAsync(TcpProbe client)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        try
        {
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                _ = await client.ReadFrameAsync(remaining);
            }

            throw new TimeoutException("TCP still open after teardown");
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or SocketException)
        {
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("TCP still open after teardown");
        }
    }

    private static void WaitGate(ManualResetEventSlim gate, string name)
    {
        if (!gate.Wait(BarrierTimeout))
        {
            throw new TimeoutException("timed out waiting for " + name);
        }
    }

    private static void ClearHooks(PacketDispatcher? dispatcher, SessionTeardown? teardown)
    {
        if (dispatcher is not null)
        {
            dispatcher.BeforeTryGetActiveSession = null;
            dispatcher.AfterTryGetActiveSession = null;
            dispatcher.BeforeSameAccountSessionReplace = null;
        }

        if (teardown is not null)
        {
            teardown.AfterSessionRemovedBeforeClientLookup = null;
        }
    }

    private static string UniqueUser(string prefix)
        => prefix + Guid.NewGuid().ToString("N")[..8];

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static byte[] BuildRegister(string user, string pass) => BuildLogin(user, pass, PacketId.RegisterRequest);

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

    private static byte[] BuildReconnect(string token)
    {
        var t = Encoding.UTF8.GetBytes(token);
        var payload = new byte[1 + 2 + t.Length];
        payload[0] = (byte)PacketId.ReconnectRequest;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)t.Length);
        t.CopyTo(payload, 3);
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

    private static byte[] BuildModerate(ModerationAction action, string target, string reason)
    {
        var body = ModerateWire.BuildRequest(action, target, reason);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.ModerateRequest;
        body.CopyTo(payload, 1);
        return payload;
    }

    private static bool TryDecodeStatus(byte[] payload, out bool success, out string message)
    {
        success = false;
        message = string.Empty;
        if (payload.Length < 3)
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

    private enum ReconnectTcpState
    {
        Alive,
        Closed,
        OpenWithoutSession,
    }

    private sealed class CountingPlayerStateStore : IPlayerStateStore
    {
        private readonly InMemoryPlayerStateStore _inner = new();
        private int _upserts;

        public int Upserts => Volatile.Read(ref _upserts);

        public bool TryGetForCharacter(string characterId, out PlayerWorldState state)
            => _inner.TryGetForCharacter(characterId, out state);

        public void UpsertForCharacter(string characterId, int mapId, int x, int y)
        {
            Interlocked.Increment(ref _upserts);
            _inner.UpsertForCharacter(characterId, mapId, x, y);
        }
    }

    private sealed class CountingRuntimeCleanup : ICharacterRuntimeCleanup
    {
        private int _cancels;

        public ICharacterRuntimeCleanup? Inner { get; set; }

        public int Cancels => Volatile.Read(ref _cancels);

        public void CancelForCharacter(Guid characterId)
        {
            Interlocked.Increment(ref _cancels);
            Inner?.CancelForCharacter(characterId);
        }
    }

    private sealed class TcpProbe : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
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
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var frame = new byte[4 + payload.Length];
                BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
                payload.CopyTo(frame, 4);
                await _stream!.WriteAsync(frame).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
        {
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await ReadExactAsync(payload, cts.Token);
            return payload;
        }

        public async Task DrainPendingAsync(TimeSpan? budget = null)
        {
            var deadline = DateTime.UtcNow + (budget ?? TimeSpan.FromMilliseconds(200));
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                try
                {
                    _ = await ReadFrameAsync(remaining);
                }
                catch
                {
                    break;
                }
            }
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                byte[] frame;
                try
                {
                    frame = await ReadFrameAsync(remaining);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (frame[0] == (byte)id)
                {
                    return frame;
                }
            }

            throw new TimeoutException("expected packet not received: " + id);
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
            _tcp?.Close();
            _stream?.Dispose();
            _tcp?.Dispose();
            await Task.CompletedTask;
        }
    }
}
