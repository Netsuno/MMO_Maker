using System;
using System.Text.Json;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventRouteTests
{
    [Fact]
    public void Hello_StaysVersion11()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Fact]
    public void LegacyWaypointJson_RoundTripsWithoutStepKind()
    {
        const string json = """[{"tileX":2,"tileY":3,"waitMs":250}]""";
        Assert.True(MapEventRouteWaypointCodec.TryDeserialize(json, out var waypoints, out var error), error);
        var step = Assert.Single(waypoints);
        Assert.Equal(2, step.TileX);
        Assert.Equal(3, step.TileY);
        Assert.Equal(250, step.WaitMs);
        Assert.Null(step.StepKind);
        Assert.Equal(MapEventRouteStepKinds.Move, MapEventRouteStepKinds.Canonical(step.StepKind));

        var written = MapEventRouteWaypointCodec.Serialize(waypoints);
        Assert.DoesNotContain("stepKind", written, StringComparison.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(written);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public void PageRoute_RoundTripsRepeatWaitAndDirection()
    {
        var page = RoutePage();
        Assert.True(page.Validate(out var error), error);

        var json = MapEventPagesCodec.SerializePages([page]);
        Assert.Contains("\"routeRepeat\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"stepKind\":\"wait\"", json, StringComparison.Ordinal);
        Assert.Contains("\"stepKind\":\"down\"", json, StringComparison.Ordinal);
        Assert.True(MapEventPagesCodec.TryDeserializePages(json, out var pages, out var readError), readError);
        var back = Assert.Single(pages);
        Assert.Equal(false, back.RouteRepeat);
        Assert.True(back.RouteSkipIfBlocked);
        Assert.Equal(MapEventRouteStepKinds.Wait, back.RouteWaypoints[1].StepKind);
        Assert.Equal(MapEventRouteStepKinds.Down, back.RouteWaypoints[2].StepKind);
        Assert.Equal(400, back.RouteWaypoints[1].WaitMs);
    }

    [Fact]
    public void MissingRouteRepeat_StillLoops()
    {
        const string json = """
            [{"pageOrder":0,"movementKind":"route","routeWaypoints":[{"tileX":1,"tileY":1,"waitMs":10},{"tileX":1,"tileY":2,"waitMs":10}]}]
            """;
        Assert.True(MapEventPagesCodec.TryDeserializePages(json, out var pages, out var error), error);
        Assert.Null(Assert.Single(pages).RouteRepeat);
        Assert.True(MapEventRouteBinding.Repeats(pages[0].RouteRepeat));
    }

    [Fact]
    public void UnknownStepKind_FailsValidation()
    {
        var page = RoutePage();
        page.RouteWaypoints =
        [
            new MapEventRouteWaypoint { TileX = 1, TileY = 1, WaitMs = 10 },
            new MapEventRouteWaypoint { TileX = 1, TileY = 2, WaitMs = 10, StepKind = "script" },
        ];
        Assert.False(page.Validate(out var error));
        Assert.Contains("inconnu", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyPageRoute_UsesEditedPageOverFixedPlacement()
    {
        var entry = new MapEventWireEntry
        {
            PlacementId = 7,
            MovementKind = MapEventMovementKinds.Fixed,
            RouteWaypoints = [],
            TileX = 1,
            TileY = 1,
        };
        MapEventRouteBinding.ApplyPageRoute(entry, RoutePage());
        Assert.Equal(MapEventMovementKinds.Route, entry.MovementKind);
        Assert.Equal(false, entry.RouteRepeat);
        Assert.True(entry.RouteSkipIfBlocked);
        Assert.Equal(3, entry.RouteWaypoints!.Count);
        Assert.Equal(MapEventRouteStepKinds.Wait, entry.RouteWaypoints[1].StepKind);
    }

    [Fact]
    public void ApplyPageRoute_KeepsPlacementRouteThatAlreadyRuns()
    {
        var entry = new MapEventWireEntry
        {
            MovementKind = MapEventMovementKinds.Route,
            RouteWaypoints =
            [
                new MapEventRouteWaypoint { TileX = 8, TileY = 2, WaitMs = 250 },
                new MapEventRouteWaypoint { TileX = 9, TileY = 2, WaitMs = 250 },
            ],
        };
        MapEventRouteBinding.ApplyPageRoute(entry, RoutePage());
        Assert.Equal(8, entry.RouteWaypoints![0].TileX);
        Assert.Null(entry.RouteRepeat);
        Assert.False(entry.RouteSkipIfBlocked);
    }

    [Fact]
    public void ApplyPageRoute_LeavesPlacementRouteWhenPageIsFixed()
    {
        var entry = new MapEventWireEntry
        {
            MovementKind = MapEventMovementKinds.Route,
            RouteWaypoints =
            [
                new MapEventRouteWaypoint { TileX = 4, TileY = 0, WaitMs = 250 },
                new MapEventRouteWaypoint { TileX = 4, TileY = 1, WaitMs = 250 },
            ],
        };
        MapEventRouteBinding.ApplyPageRoute(
            entry,
            new MapEventPageDefinition { MovementKind = MapEventMovementKinds.Fixed });
        Assert.Equal(MapEventMovementKinds.Route, entry.MovementKind);
        Assert.Equal(2, entry.RouteWaypoints!.Count);
        Assert.Null(entry.RouteRepeat);
    }

    [Fact]
    public void WireEntry_OmitsDefaultRouteOptions()
    {
        var json = JsonSerializer.Serialize(new MapEventWireEntry
        {
            PlacementId = 1,
            CatalogId = 1,
            Slug = "a",
            DisplayName = "A",
            TileX = 0,
            TileY = 0,
        });
        Assert.DoesNotContain("routeRepeat", json, StringComparison.Ordinal);
        Assert.DoesNotContain("routeSkipIfBlocked", json, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    private static MapEventPageDefinition RoutePage() => new()
    {
        PageOrder = 0,
        TriggerKind = Phase8MapEventTriggerKinds.Action,
        MovementKind = MapEventMovementKinds.Route,
        RouteRepeat = false,
        RouteSkipIfBlocked = true,
        RouteWaypoints =
        [
            new MapEventRouteWaypoint { TileX = 1, TileY = 1, WaitMs = 250 },
            new MapEventRouteWaypoint { WaitMs = 400, StepKind = MapEventRouteStepKinds.Wait },
            new MapEventRouteWaypoint { WaitMs = 250, StepKind = MapEventRouteStepKinds.Down },
        ],
    };
}
