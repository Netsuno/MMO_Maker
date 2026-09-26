using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class MapEventPlacementResizeTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public MapEventPlacementResizeTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Shift_SlidesAdjacentSameEvent_ClipsRoute_LeavesPublishedSnapshot()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var mapRepo = new PostgresMapRepository(gate);
        var eventRepo = new PostgresMapEventRepository(gate);

        var eventSave = await eventRepo.SaveAsync(new SaveMapEventRequest
        {
            Definition = new MapEventDefinition
            {
                Name = "Décalage",
                Pages = [new MapEventPageDefinition { PageOrder = 0 }],
            },
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        var eventId = Assert.IsType<SaveMapEventResult.Success>(eventSave).EventId;

        var map = new Map { Name = "DécalageCarte", Width = 8, Height = 8 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var mapSave = await mapRepo.SaveAsync(new SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        });
        var mapId = Assert.IsType<SaveMapResult.Success>(mapSave).MapId;

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var route = MapEventRouteWaypointCodec.Serialize(
        [
            new MapEventRouteWaypoint { TileX = 1, TileY = 1, WaitMs = 40 },
            new MapEventRouteWaypoint { TileX = 6, TileY = 1, WaitMs = 80 },
        ]);
        await gate.ExecuteAsync(async (db, ct) =>
        {
            db.MapEventPlacements.Add(new MapEventPlacementEntity
            {
                Id = firstId,
                MapId = mapId,
                EventDefinitionId = eventId,
                TileX = 1,
                TileY = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Fixed,
                RouteWaypointsJson = route,
            });
            db.MapEventPlacements.Add(new MapEventPlacementEntity
            {
                Id = secondId,
                MapId = mapId,
                EventDefinitionId = eventId,
                TileX = 2,
                TileY = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Fixed,
                RouteWaypointsJson = "[]",
            });
            await db.SaveChangesAsync(ct);
        });

        var publish = await mapRepo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Intent = SaveMapIntent.Publish,
        });
        Assert.IsType<SaveMapResult.Success>(publish);

        var shift = await gate.ExecuteAsync((db, ct) => MapEventPlacementResize.ShiftAsync(
            db,
            mapId,
            new MapResizeShiftEdit { Width = 4, Height = 4, DeltaX = 1, DeltaY = 1 },
            ct));
        Assert.True(shift.Ok, shift.Error);
        Assert.Equal(2, shift.EventsKept);
        Assert.Equal(0, shift.EventsRemoved);
        Assert.Equal(1, shift.WaypointsKept);
        Assert.Equal(1, shift.WaypointsRemoved);

        await gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.MapEventPlacements.AsNoTracking()
                .Where(p => p.MapId == mapId)
                .OrderBy(p => p.TileX)
                .ToListAsync(ct);
            Assert.Equal(2, rows.Count);
            Assert.Equal(firstId, rows[0].Id);
            Assert.Equal(2, rows[0].TileX);
            Assert.Equal(1, rows[0].TileY);
            Assert.True(MapEventRouteWaypointCodec.TryDeserialize(rows[0].RouteWaypointsJson, out var waypoints, out var jsonError), jsonError);
            var kept = Assert.Single(waypoints);
            Assert.Equal(2, kept.TileX);
            Assert.Equal(2, kept.TileY);
            Assert.Equal(40, kept.WaitMs);
            Assert.Equal(3, rows[1].TileX);
            Assert.Equal(1, rows[1].TileY);
            Assert.Equal(eventId, rows[0].EventDefinitionId);
            Assert.Equal(eventId, rows[1].EventDefinitionId);
        });

        int runtimeMapId = 0;
        await gate.ExecuteAsync(async (db, ct) =>
        {
            runtimeMapId = await db.RuntimeMapBindings.AsNoTracking()
                .Where(b => b.MapId == mapId)
                .Select(b => b.RuntimeMapId)
                .SingleAsync(ct);
        });
        var published = await eventRepo.GetPlacementsForRuntimeMapAsync(runtimeMapId);
        Assert.Equal(2, published.Count);
        Assert.Contains(published, p => p.TileX == 1 && p.TileY == 0);
        Assert.Contains(published, p => p.TileX == 2 && p.TileY == 0);

        var clip = await gate.ExecuteAsync((db, ct) => MapEventPlacementResize.ShiftAsync(
            db,
            mapId,
            new MapResizeShiftEdit { Width = 3, Height = 3, DeltaX = 0, DeltaY = 0 },
            ct));
        Assert.True(clip.Ok, clip.Error);
        Assert.Equal(1, clip.EventsKept);
        Assert.Equal(1, clip.EventsRemoved);

        await gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.MapEventPlacements.AsNoTracking().Where(p => p.MapId == mapId).ToListAsync(ct);
            var only = Assert.Single(rows);
            Assert.Equal(firstId, only.Id);
            Assert.Equal(2, only.TileX);
            Assert.Equal(1, only.TileY);
        });

        var before = new MapEventPlacementDefinition
        {
            Id = firstId,
            MapId = mapId,
            EventDefinitionId = eventId,
            TileX = 2,
            TileY = 1,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            MovementKind = MapEventMovementKinds.Fixed,
            RouteWaypoints = [new MapEventRouteWaypoint { TileX = 2, TileY = 2, WaitMs = 40 }],
        };
        var deleted = new MapEventPlacementDefinition
        {
            Id = secondId,
            MapId = mapId,
            EventDefinitionId = eventId,
            TileX = 3,
            TileY = 1,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            MovementKind = MapEventMovementKinds.Fixed,
        };
        var restored = await gate.ExecuteAsync((db, ct) => MapEventPlacementResize.RestoreAsync(
            db,
            mapId,
            [before, deleted],
            deleteIds: [],
            ct));
        Assert.True(restored.Ok, restored.Error);
        await gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.MapEventPlacements.AsNoTracking()
                .Where(p => p.MapId == mapId)
                .OrderBy(p => p.TileX)
                .ToListAsync(ct);
            Assert.Equal(2, rows.Count);
            Assert.Equal(2, rows[0].TileX);
            Assert.Equal(3, rows[1].TileX);
            Assert.Equal(secondId, rows[1].Id);
        });
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Shift_RejectsUnreadableRoute_WithoutMovingTiles()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var mapRepo = new PostgresMapRepository(gate);
        var eventRepo = new PostgresMapEventRepository(gate);
        var eventSave = await eventRepo.SaveAsync(new SaveMapEventRequest
        {
            Definition = new MapEventDefinition
            {
                Name = "Route cassée",
                Pages = [new MapEventPageDefinition { PageOrder = 0 }],
            },
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        var eventId = Assert.IsType<SaveMapEventResult.Success>(eventSave).EventId;
        var map = new Map { Name = "RouteCassée", Width = 4, Height = 4 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var mapId = Assert.IsType<SaveMapResult.Success>(await mapRepo.SaveAsync(new SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        })).MapId;
        var placementId = Guid.NewGuid();
        await gate.ExecuteAsync(async (db, ct) =>
        {
            db.MapEventPlacements.Add(new MapEventPlacementEntity
            {
                Id = placementId,
                MapId = mapId,
                EventDefinitionId = eventId,
                TileX = 1,
                TileY = 1,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Fixed,
                RouteWaypointsJson = "{}",
            });
            await db.SaveChangesAsync(ct);
        });

        var shift = await gate.ExecuteAsync((db, ct) => MapEventPlacementResize.ShiftAsync(
            db,
            mapId,
            new MapResizeShiftEdit { Width = 4, Height = 4, DeltaX = 1, DeltaY = 0 },
            ct));
        Assert.False(shift.Ok);
        Assert.Contains("illisible", shift.Error, StringComparison.Ordinal);

        await gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.MapEventPlacements.AsNoTracking().SingleAsync(p => p.Id == placementId, ct);
            Assert.Equal(1, row.TileX);
            Assert.Equal(1, row.TileY);
            Assert.False(MapEventRouteWaypointCodec.TryDeserialize(row.RouteWaypointsJson, out _, out _));
        });
    }
}
