using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventMovementServiceTests
{
    [Fact]
    public void TickMap_AdvancesRouteToNextWaypoint()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(mapId: 1, [placement]);
        service.TickMap(1);
        var runtime = service.ApplyRuntimePositions(1, [placement]).Single();
        Assert.Equal(4, runtime.TileX);
        Assert.Equal(1, runtime.TileY);
    }

    [Fact]
    public void TickMap_ReturnsTrueOnlyWhenPlacementTileChanges()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(1, [placement]);
        Assert.True(service.TickMap(1));
        Assert.False(service.TickMap(1));
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.True(service.TickMap(1));
    }

    [Fact]
    public void TickMap_EmptyMap_ReturnsFalse()
    {
        var service = new MapEventMovementService();
        Assert.False(service.TickMap(1));
    }

    [Fact]
    public void IsTileBlockedByEvent_ReturnsTrueOnRuntimeTile()
    {
        var service = new MapEventMovementService();
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(1, [placement]);
        service.TickMap(1);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
    }

    [Fact]
    public void IsTileBlockedByEvent_IgnoresPlacementWhenBlocksCollisionFalse()
    {
        var service = new MapEventMovementService();
        var placement = CreateRoutePlacement(4, 0, blocksCollision: false);
        service.SyncMapPlacements(1, [placement]);
        Assert.False(service.IsTileBlockedByEvent(1, 4, 0));
    }

    [Fact]
    public void ClearMap_ClearsCollisionState()
    {
        var service = new MapEventMovementService();
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(1, [placement]);
        service.TickMap(1);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
        service.ClearMap(1);
        Assert.False(service.IsTileBlockedByEvent(1, 4, 1));
    }

    [Fact]
    public void TickMap_RespectsWaypointWaitMsBeforeReturnLeg()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(
            4,
            0,
            [
                new MapEventRouteWaypoint { TileX = 4, TileY = 0, WaitMs = 250 },
                new MapEventRouteWaypoint { TileX = 4, TileY = 1, WaitMs = 500 },
            ]);
        service.SyncMapPlacements(1, [placement]);
        service.TickMap(1);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
        clock.Advance(TimeSpan.FromMilliseconds(200));
        service.TickMap(1);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
    }

    [Fact]
    public void TickMap_DoesNotAdvanceOntoOccupiedPlayerTile()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(1, [placement]);
        service.TickMap(1, new HashSet<(int, int)> { (4, 1) });
        var runtime = service.ApplyRuntimePositions(1, [placement]).Single();
        Assert.Equal(0, runtime.TileY);
    }

    [Fact]
    public async Task TickMapAsync_ConcurrentCalls_AdvanceRouteOnlyOnce()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(1, [placement]);

        await Task.WhenAll(
            service.TickMapAsync(1, null),
            service.TickMapAsync(1, null));

        var runtime = service.ApplyRuntimePositions(1, [placement]).Single();
        Assert.Equal(1, runtime.TileY);
    }

    [Fact]
    public void ResolveRuntimePlacements_UsesMovedTileForInteraction()
    {
        var service = new MapEventMovementService();
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(1, [placement]);
        service.TickMap(1);
        var runtime = service.ResolveRuntimePlacements(1, [placement]).Single();
        Assert.Equal(4, runtime.TileX);
        Assert.Equal(1, runtime.TileY);
        Assert.False(service.IsTileBlockedByEvent(1, 4, 0));
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
    }

    [Fact]
    public async Task ResolveRuntimePlacements_ConcurrentWithTick_ReturnsConsistentCoordinates()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        service.SyncMapPlacements(1, [placement]);

        var tasks = Enumerable.Range(0, 32).Select(async _ =>
        {
            await service.TickMapAsync(1, null);
            var runtime = service.ResolveRuntimePlacements(1, [placement]).Single();
            Assert.True(runtime.TileY is 0 or 1);
            if (runtime.TileY == 1)
            {
                Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
            }
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentHeartbeatsAndReads_ShareOnePublishedSnapshot()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        service.RegisterOccupant(1, playerA);
        service.RegisterOccupant(1, playerB);
        service.SyncMapPlacements(1, [placement]);

        var tasks = Enumerable.Range(0, 24).Select(async i =>
        {
            if (i % 3 == 0)
            {
                await service.TickMapAsync(1, null);
            }

            Assert.True(service.TryGetPublishedPlacementForTest(1, placement.PlacementId, out var tileX, out var tileY, out var blockedHere));
            Assert.Equal(4, tileX);
            Assert.True(tileY is 0 or 1);
            Assert.True(blockedHere);

            var resolved = service.ResolveRuntimePlacements(1, [placement]).Single();
            var applied = service.ApplyRuntimePositions(1, [placement]).Single();
            Assert.True(resolved.TileY is 0 or 1);
            Assert.True(applied.TileY is 0 or 1);
        });

        await Task.WhenAll(tasks);

        var settled = service.ResolveRuntimePlacements(1, [placement]).Single();
        var appliedSettled = service.ApplyRuntimePositions(1, [placement]).Single();
        Assert.Equal(settled.TileX, appliedSettled.TileX);
        Assert.Equal(settled.TileY, appliedSettled.TileY);
        Assert.Equal(settled.TileY == 0, service.IsTileBlockedByEvent(1, 4, 0));
        Assert.Equal(settled.TileY == 1, service.IsTileBlockedByEvent(1, 4, 1));
        Assert.Equal(2, service.OccupantCountForTest(1));
    }

    [Fact]
    public void UnregisterOccupant_WhenAnotherPlayerRemains_DoesNotResetRoutePosition()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        var remaining = Guid.NewGuid();
        var leaving = Guid.NewGuid();
        service.RegisterOccupant(1, remaining);
        service.RegisterOccupant(1, leaving);
        service.SyncMapPlacements(1, [placement]);
        service.TickMap(1);

        var moved = service.ApplyRuntimePositions(1, [placement]).Single();
        Assert.Equal(1, moved.TileY);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));

        service.UnregisterOccupant(leaving, mapId: 1);

        Assert.True(service.HasOccupantForTest(1, remaining));
        Assert.False(service.HasOccupantForTest(1, leaving));
        Assert.Equal(1, service.OccupantCountForTest(1));

        var afterLeave = service.ResolveRuntimePlacements(1, [placement]).Single();
        Assert.Equal(moved.TileX, afterLeave.TileX);
        Assert.Equal(moved.TileY, afterLeave.TileY);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
        Assert.False(service.IsTileBlockedByEvent(1, 4, 0));
        Assert.Equal(1, service.ApplyRuntimePositions(1, [placement]).Single().TileY);
    }

    [Fact]
    public void UnregisterOccupant_LastPlayerLeaves_DoesNotResetPublishedPositions()
    {
        var service = new MapEventMovementService();
        var placement = CreateRoutePlacement(4, 0);
        var player = Guid.NewGuid();
        service.RegisterOccupant(1, player);
        service.SyncMapPlacements(1, [placement]);
        service.TickMap(1);

        service.UnregisterOccupant(player, mapId: 1);

        Assert.Equal(0, service.OccupantCountForTest(1));
        var runtime = service.ApplyRuntimePositions(1, [placement]).Single();
        Assert.Equal(4, runtime.TileX);
        Assert.Equal(1, runtime.TileY);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
    }

    [Fact]
    public void TickMap_AfterPlayerLeavesOccupiedTile_AllowsRouteAdvance()
    {
        var clock = new SteppingClock();
        var service = new MapEventMovementService(clock);
        var placement = CreateRoutePlacement(4, 0);
        var blocker = Guid.NewGuid();
        var remaining = Guid.NewGuid();
        service.RegisterOccupant(1, blocker);
        service.RegisterOccupant(1, remaining);
        service.SyncMapPlacements(1, [placement]);

        var occupied = new HashSet<(int, int)> { (4, 1) };
        service.TickMap(1, occupied);
        Assert.Equal(0, service.ApplyRuntimePositions(1, [placement]).Single().TileY);

        service.UnregisterOccupant(blocker, mapId: 1);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        service.TickMap(1, new HashSet<(int, int)> { (2, 2) });

        var runtime = service.ResolveRuntimePlacements(1, [placement]).Single();
        Assert.Equal(1, runtime.TileY);
        Assert.True(service.IsTileBlockedByEvent(1, 4, 1));
        Assert.True(service.HasOccupantForTest(1, remaining));
    }

    private static MapEventWireEntry CreateRoutePlacement(
        int startX,
        int startY,
        IReadOnlyList<MapEventRouteWaypoint>? waypoints = null,
        bool blocksCollision = true) =>
        new()
        {
            PlacementId = 1,
            CatalogId = 42,
            Slug = "route-test",
            DisplayName = "Route test",
            TileX = startX,
            TileY = startY,
            MovementKind = MapEventMovementKinds.Route,
            RouteWaypoints = waypoints ??
            [
                new MapEventRouteWaypoint { TileX = 4, TileY = 0, WaitMs = 250 },
                new MapEventRouteWaypoint { TileX = 4, TileY = 1, WaitMs = 250 },
            ],
            BlocksCollision = blocksCollision,
        };

    private sealed class SteppingClock : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan delta) => _utcNow += delta;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
