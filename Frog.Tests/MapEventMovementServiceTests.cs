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
