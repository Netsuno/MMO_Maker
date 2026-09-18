using System;
using System.Collections.Generic;
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

    private sealed class RecordingRuntimeCleanup : ICharacterRuntimeCleanup
    {
        public List<Guid> Cancelled { get; } = new();

        public void CancelForCharacter(Guid characterId) => Cancelled.Add(characterId);
    }
}
