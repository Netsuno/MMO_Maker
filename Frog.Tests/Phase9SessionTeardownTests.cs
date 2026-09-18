using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Server.Gameplay;
using Frog.Server.Network;
using Frog.Server.Persistence;
using Frog.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Frog.Tests;

public sealed class Phase9SessionTeardownTests
{
    [Fact]
    public async Task TearDown_SavesStateCancelsExecutionsAndIsIdempotent()
    {
        var connections = new ConnectionManager();
        var clients = new ClientRegistry();
        var store = new InMemoryPlayerStateStore();
        var cleanup = new RecordingRuntimeCleanup();
        var sender = new PacketSender(NullLogger<PacketSender>.Instance);
        var lifecycle = new PlayerLifecycleNotifier(sender, clients);
        var teardown = new SessionTeardown(connections, clients, lifecycle, store, cleanup);

        Assert.True(connections.TryCreateSession("p9kick", out var session));
        session!.CharacterId = "char-p9-1";
        session.CharacterGuid = Guid.NewGuid();
        session.CurrentMapId = 4;
        session.PixelX = 128;
        session.PixelY = 256;

        await teardown.TearDownAsync(session.Id, SessionTeardownOptions.KickBan);
        await teardown.TearDownAsync(session.Id, SessionTeardownOptions.KickBan);
        await teardown.TearDownByUsernameAsync("p9kick", SessionTeardownOptions.KickBan);

        Assert.False(connections.TryGetSessionByUsername("p9kick", out _));
        Assert.Single(cleanup.Cancelled);
        Assert.Equal(session.CharacterGuid, cleanup.Cancelled[0]);
        Assert.True(store.TryGetForCharacter("char-p9-1", out var saved));
        Assert.Equal(4, saved.MapId);
        Assert.Equal(128, saved.X);
        Assert.Equal(256, saved.Y);
    }

    [Fact]
    public async Task TearDownByUsername_MissingSession_IsNoOp()
    {
        var connections = new ConnectionManager();
        var clients = new ClientRegistry();
        var teardown = new SessionTeardown(
            connections,
            clients,
            new PlayerLifecycleNotifier(new PacketSender(NullLogger<PacketSender>.Instance), clients),
            new InMemoryPlayerStateStore(),
            new RecordingRuntimeCleanup());

        await teardown.TearDownByUsernameAsync("nobody", SessionTeardownOptions.KickBan);
        Assert.Empty(connections.GetActiveSessions());
    }

    [Fact]
    public void ConnectionManager_TryRemoveSession_IsIdempotent()
    {
        var manager = new ConnectionManager();
        Assert.True(manager.TryCreateSession("once", out var session));
        Assert.True(manager.TryRemoveSession(session!.Id, out var removed));
        Assert.Same(session, removed);
        Assert.False(manager.TryRemoveSession(session.Id, out var missing));
        Assert.Null(missing);
        Assert.True(manager.TryCreateSession("once", out _));
    }

    [Fact]
    public void MapEventExecutionTracker_ClearForCharacter_AllowsKickPathToReenter()
    {
        var tracker = new MapEventExecutionTracker();
        var characterId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        Assert.True(tracker.TryBeginParallel(characterId, 1, eventId, mapId: 1));
        tracker.RegisterWait(
            characterId,
            new PendingWaitResume(DateTimeOffset.UtcNow.AddMinutes(1), Array.Empty<MapEventCommandDefinition>()));
        tracker.ClearForCharacter(characterId);
        Assert.True(tracker.TryBeginParallel(characterId, 1, eventId, mapId: 1));
        Assert.Empty(tracker.TakeReadyWaits(characterId, DateTimeOffset.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task TearDown_RetainsTcpWhenConcurrentPacketUnregistersInactiveSession()
    {
        var connections = new ConnectionManager();
        var clients = new ClientRegistry();
        var store = new CountingPlayerStateStore();
        var cleanup = new RecordingRuntimeCleanup();
        var sender = new PacketSender(NullLogger<PacketSender>.Instance);
        var lifecycle = new PlayerLifecycleNotifier(sender, clients);
        var teardown = new SessionTeardown(connections, clients, lifecycle, store, cleanup);

        Assert.True(connections.TryCreateSession("p9race", out var session));
        session!.CharacterId = "char-p9-race";
        session.CharacterGuid = Guid.NewGuid();
        session.CurrentMapId = 3;
        session.PixelX = 64;
        session.PixelY = 96;

        await using var pair = await LoopbackPair.CreateAsync();
        pair.Session.AuthenticatedSession = session;
        clients.Register(session.Id, pair.Session);

        var packetUnregistered = new ManualResetEventSlim(false);
        teardown.AfterSessionRemovedBeforeClientLookup = id =>
        {
            Assert.Equal(session.Id, id);
            Assert.False(connections.IsSessionActive(id));
            // PacketDispatcher.TryGetActiveSession on an in-flight packet.
            clients.Unregister(id);
            pair.Session.AuthenticatedSession = null;
            packetUnregistered.Set();
        };

        await teardown.TearDownAsync(session.Id, SessionTeardownOptions.KickBan);

        Assert.True(packetUnregistered.IsSet);
        Assert.True(pair.Session.IsClosed);
        Assert.False(clients.TryGet(session.Id, out _));
        Assert.Empty(clients.GetAllAuthenticatedClients());
        Assert.False(connections.TryGetSessionByUsername("p9race", out _));
        Assert.Equal(1, cleanup.Cancelled.Count);
        Assert.Equal(1, store.Upserts);
        Assert.True(store.TryGetForCharacter("char-p9-race", out var saved));
        Assert.Equal(3, saved.MapId);
        await AssertTcpClosedAsync(pair.ClientStream);
    }

    [Fact]
    public async Task ConcurrentTearDown_SaveAndCancelOnce_ClosesTcp()
    {
        var connections = new ConnectionManager();
        var clients = new ClientRegistry();
        var store = new CountingPlayerStateStore();
        var cleanup = new RecordingRuntimeCleanup();
        var teardown = new SessionTeardown(
            connections,
            clients,
            new PlayerLifecycleNotifier(new PacketSender(NullLogger<PacketSender>.Instance), clients),
            store,
            cleanup);

        Assert.True(connections.TryCreateSession("p9twice", out var session));
        session!.CharacterId = "char-p9-twice";
        session.CharacterGuid = Guid.NewGuid();
        session.CurrentMapId = 2;
        session.PixelX = 8;
        session.PixelY = 16;

        await using var pair = await LoopbackPair.CreateAsync();
        pair.Session.AuthenticatedSession = session;
        clients.Register(session.Id, pair.Session);

        var firstRemoved = new ManualResetEventSlim(false);
        var secondStarted = new ManualResetEventSlim(false);
        teardown.AfterSessionRemovedBeforeClientLookup = _ =>
        {
            firstRemoved.Set();
            WaitGate(secondStarted, "second teardown start");
        };

        var first = Task.Run(
            () => teardown.TearDownAsync(session.Id, SessionTeardownOptions.KickBan));
        WaitGate(firstRemoved, "first session remove");
        var second = Task.Run(
            () => teardown.TearDownAsync(session.Id, SessionTeardownOptions.KickBan));
        secondStarted.Set();
        await Task.WhenAll(first, second);

        Assert.True(pair.Session.IsClosed);
        Assert.Equal(1, cleanup.Cancelled.Count);
        Assert.Equal(1, store.Upserts);
        Assert.False(connections.IsSessionActive(session.Id));
        Assert.Empty(clients.GetAllAuthenticatedClients());
        await AssertTcpClosedAsync(pair.ClientStream);
    }

    [Fact]
    public async Task TearDown_OldSessionDoesNotUnregisterReplacement()
    {
        var connections = new ConnectionManager();
        var clients = new ClientRegistry();
        var store = new CountingPlayerStateStore();
        var cleanup = new RecordingRuntimeCleanup();
        var teardown = new SessionTeardown(
            connections,
            clients,
            new PlayerLifecycleNotifier(new PacketSender(NullLogger<PacketSender>.Instance), clients),
            store,
            cleanup);

        Assert.True(connections.TryCreateSession("p9swap", out var original));
        original!.CharacterId = "char-old";
        original.CharacterGuid = Guid.NewGuid();

        await using var oldPair = await LoopbackPair.CreateAsync();
        await using var newPair = await LoopbackPair.CreateAsync();
        oldPair.Session.AuthenticatedSession = original;
        clients.Register(original.Id, oldPair.Session);

        teardown.AfterSessionRemovedBeforeClientLookup = _ =>
        {
            Assert.True(connections.TryCreateSession("p9swap", out var replacement));
            replacement!.CharacterId = "char-new";
            newPair.Session.AuthenticatedSession = replacement;
            clients.Register(replacement.Id, newPair.Session);
        };

        await teardown.TearDownAsync(original.Id, SessionTeardownOptions.ReconnectDisplace);

        Assert.True(oldPair.Session.IsClosed);
        Assert.False(newPair.Session.IsClosed);
        Assert.True(connections.TryGetSessionByUsername("p9swap", out var live));
        Assert.NotEqual(original.Id, live!.Id);
        Assert.True(clients.TryGet(live.Id, out var liveClient));
        Assert.Same(newPair.Session, liveClient);
        Assert.False(clients.TryGet(original.Id, out _));
        Assert.Equal(1, cleanup.Cancelled.Count);
        Assert.Equal(1, store.Upserts);
    }

    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(10);

    private static void WaitGate(ManualResetEventSlim gate, string name)
    {
        if (!gate.Wait(BarrierTimeout))
        {
            throw new TimeoutException("timed out waiting for " + name);
        }
    }

    private static async Task AssertTcpClosedAsync(NetworkStream stream)
    {
        using var cts = new CancellationTokenSource(BarrierTimeout);
        var buf = new byte[1];
        try
        {
            var read = await stream.ReadAsync(buf, cts.Token);
            Assert.Equal(0, read);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("TCP still open after teardown");
        }
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

    private sealed class LoopbackPair : IAsyncDisposable
    {
        public required ClientSession Session { get; init; }
        public required TcpClient Client { get; init; }
        public required NetworkStream ClientStream { get; init; }
        public required TcpListener Listener { get; init; }

        public static async Task<LoopbackPair> CreateAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accept = listener.AcceptTcpClientAsync();
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            var serverTcp = await accept;
            return new LoopbackPair
            {
                Listener = listener,
                Client = client,
                ClientStream = client.GetStream(),
                Session = new ClientSession(serverTcp),
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Session.DisposeAsync();
            ClientStream.Dispose();
            Client.Dispose();
            Listener.Stop();
        }
    }

    private sealed class RecordingRuntimeCleanup : ICharacterRuntimeCleanup
    {
        public List<Guid> Cancelled { get; } = new();

        public void CancelForCharacter(Guid characterId) => Cancelled.Add(characterId);
    }
}
