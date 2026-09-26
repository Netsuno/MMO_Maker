using System;
using System.Collections.Generic;
using System.Linq;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapResizeShiftTests
{
    [Fact]
    public void RoundTrip_ShiftThenUnshift_KeepsTileAssetIds_OnV6_48()
    {
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var red = TileAssetId.FromStraightRgba(SolidRgba(200, 10, 10, 255));
        var blue = TileAssetId.FromStraightRgba(SolidRgba(10, 10, 200, 255));
        var warpMap = Guid.NewGuid();
        var map = MapFormat.CreateTileAssetMap("Clairière", 8, 8);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers.Add(new Layer { LayerType = LayerType.Mask });
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes });
        var ground = new Tile
        {
            Type = TileType.Ground,
            AssetId = red,
        };
        ground.Attributes.Add(new BlockAttribute());
        MapEditOperations.PaintTile(map, 0, 2, 3, ground);
        var groundTile = map.Layers[0].Tiles.Single();
        var mask = new Tile
        {
            Type = TileType.Warp,
            AssetId = blue,
            WarpTargetMapId = warpMap,
            WarpTargetX = 4,
            WarpTargetY = 5,
        };
        MapEditOperations.PaintTile(map, 1, 1, 1, mask);
        var warpTile = map.Layers[1].Tiles.Single();
        MapEditOperations.PaintTile(map, 2, 6, 6, new Tile { Type = TileType.Block, AssetId = red });

        var entity = new MapPlacedEntity
        {
            Id = Guid.NewGuid(),
            Kind = MapPlacedKind.Spawn,
            TileX = 2,
            TileY = 3,
            Name = "Apparition 1",
            Level = 1,
        };
        var npc = new MapPlacedEntity
        {
            Id = Guid.NewGuid(),
            Kind = MapPlacedKind.Npc,
            TileX = 4,
            TileY = 4,
            Name = "PNJ 1",
            Level = 3,
        };
        var entities = new List<MapPlacedEntity> { entity, npc };
        var prefabs = new List<PrefabPlacement>
        {
            new() { PrefabId = "banc", Facing = PrefabFacing.South, TileX = 1, TileY = 1 },
        };
        var eventId = Guid.NewGuid();
        var events = new List<MapEventPlacementDefinition>
        {
            new()
            {
                Id = Guid.NewGuid(),
                MapId = Guid.NewGuid(),
                EventDefinitionId = eventId,
                TileX = 2,
                TileY = 3,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Fixed,
                RouteWaypoints =
                [
                    new MapEventRouteWaypoint { TileX = 3, TileY = 3, WaitMs = 250 },
                    new MapEventRouteWaypoint { TileX = 7, TileY = 7, WaitMs = 10 },
                ],
            },
        };
        var spawn = new MapResizeShiftSpawn { X = 2, Y = 3 };
        var expand = new MapResizeShiftEdit { Width = 10, Height = 12, DeltaX = 1, DeltaY = 2 };

        Assert.True(MapResizeShift.TryPreview(map, expand, entities, prefabs, events, spawn, out var preview, out var previewError));
        Assert.Null(previewError);
        Assert.True(preview.Changed);
        Assert.Equal(0, preview.TilesRemoved);
        Assert.Equal(2, map.Layers[0].Tiles[0].X);
        Assert.Equal(red, map.Layers[0].Tiles[0].AssetId);

        var mutated = false;
        Assert.True(MapResizeShift.TryApply(
            map,
            expand,
            entities,
            prefabs,
            events,
            spawn,
            out var report,
            out var error,
            beforeMutate: () => mutated = true));
        Assert.Null(error);
        Assert.True(mutated);
        Assert.Equal(10, map.Width);
        Assert.Equal(12, map.Height);
        Assert.Equal(TileGraphicIdentity.TileAsset, map.GraphicIdentity);
        Assert.Equal(48, map.TileSizePixels);
        Assert.Equal(3, report.TilesKept);
        Assert.Equal(0, report.TilesRemoved);

        var keptGround = map.Layers[0].Tiles.Single();
        Assert.Same(groundTile, keptGround);
        Assert.Equal(3, keptGround.X);
        Assert.Equal(5, keptGround.Y);
        Assert.Equal(red, keptGround.AssetId);
        Assert.Equal(0, keptGround.SrcX);
        Assert.Equal(0, keptGround.TilesetId);
        Assert.IsType<BlockAttribute>(Assert.Single(keptGround.Attributes));

        Assert.Same(warpTile, map.Layers[1].Tiles.Single());
        Assert.Equal(2, warpTile.X);
        Assert.Equal(3, warpTile.Y);
        Assert.Equal(blue, warpTile.AssetId);
        Assert.Equal(warpMap, warpTile.WarpTargetMapId);
        Assert.Equal(4, warpTile.WarpTargetX);
        Assert.Equal(5, warpTile.WarpTargetY);

        Assert.Equal(3, entity.TileX);
        Assert.Equal(5, entity.TileY);
        Assert.Equal(5, npc.TileX);
        Assert.Equal(6, npc.TileY);
        Assert.Equal(2, prefabs[0].TileX);
        Assert.Equal(3, prefabs[0].TileY);
        Assert.Equal(3, events[0].TileX);
        Assert.Equal(5, events[0].TileY);
        Assert.Equal(4, events[0].RouteWaypoints[0].TileX);
        Assert.Equal(5, events[0].RouteWaypoints[0].TileY);
        Assert.Equal(250, events[0].RouteWaypoints[0].WaitMs);
        Assert.Equal(8, events[0].RouteWaypoints[1].TileX);
        Assert.Equal(3, spawn.X);
        Assert.Equal(5, spawn.Y);
        Assert.True(map.Validate(out var validateError), validateError);

        var bytes = new MapSerializer().Serialize(map);
        Assert.Equal(MapSerializer.TileAssetMapFileFormatVersion, bytes[4]);
        var roundTrip = new MapSerializer().Deserialize(bytes);
        Assert.Equal(TileGraphicIdentity.TileAsset, roundTrip.GraphicIdentity);
        Assert.Equal(48, roundTrip.TileSizePixels);
        Assert.Equal(red, roundTrip.Layers[0].Tiles.Single().AssetId);
        Assert.Equal(0, roundTrip.Layers[0].Tiles.Single().SrcX);

        var shrink = new MapResizeShiftEdit { Width = 8, Height = 8, DeltaX = -1, DeltaY = -2 };
        Assert.True(MapResizeShift.TryApply(map, shrink, entities, prefabs, events, spawn, out _, out _));
        Assert.Equal(8, map.Width);
        Assert.Equal(8, map.Height);
        Assert.Equal(2, keptGround.X);
        Assert.Equal(3, keptGround.Y);
        Assert.Equal(red, keptGround.AssetId);
        Assert.Equal(1, warpTile.X);
        Assert.Equal(1, warpTile.Y);
        Assert.Equal(4, warpTile.WarpTargetX);
        Assert.Equal(2, entity.TileX);
        Assert.Equal(3, entity.TileY);
        Assert.Equal(4, npc.TileX);
        Assert.Equal(1, prefabs[0].TileX);
        Assert.Equal(2, events[0].TileX);
        Assert.Equal(3, events[0].RouteWaypoints[0].TileX);
        Assert.Equal(250, events[0].RouteWaypoints[0].WaitMs);
        Assert.Equal(2, spawn.X);
        Assert.Equal(3, spawn.Y);
        Assert.Equal(48, map.TileSizePixels);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    [Fact]
    public void Shift_Plus1Minus2_MovesPaintedTileFrom2_2_To3_0()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        var assetId = TileAssetId.FromStraightRgba(SolidRgba(9, 8, 7, 255));
        var map = MapFormat.CreateTileAssetMap("Bois", 8, 6);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var stamp = new Tile { Type = TileType.Ground, AssetId = assetId };
        MapEditOperations.PaintTile(map, 0, 2, 2, stamp);

        // PaintTile pose un clone. Le tampon garde X = 0 ; seul le clone de la couche est décalé.
        var placed = Assert.Single(map.Layers[0].Tiles);
        Assert.NotSame(stamp, placed);
        Assert.Equal(2, placed.X);
        Assert.Equal(2, placed.Y);
        Assert.Equal(assetId, placed.AssetId);

        var edit = new MapResizeShiftEdit { Width = 10, Height = 9, DeltaX = 1, DeltaY = -2 };
        Assert.True(MapResizeShift.TryApply(map, edit, null, null, null, null, out var report, out var error));
        Assert.Null(error);
        Assert.Same(placed, Assert.Single(map.Layers[0].Tiles));
        Assert.Equal(3, placed.X);
        Assert.Equal(0, placed.Y);
        Assert.Equal(assetId, placed.AssetId);
        Assert.Equal(0, placed.SrcX);
        Assert.Equal(0, placed.TilesetId);
        Assert.Equal(0, report.TilesRemoved);
        Assert.Equal(10, map.Width);
        Assert.Equal(9, map.Height);
        Assert.Equal(48, map.TileSizePixels);
        Assert.Equal(TileGraphicIdentity.TileAsset, map.GraphicIdentity);
        Assert.Equal(0, stamp.X);
        Assert.Equal(0, stamp.Y);
        Assert.True(map.Validate(out var validateError), validateError);
    }

    [Fact]
    public void Shrink_ClipsOutside_AndDoesNotRewriteKeptAssetId()
    {
        var red = TileAssetId.FromStraightRgba(SolidRgba(1, 2, 3, 255));
        var green = TileAssetId.FromStraightRgba(SolidRgba(4, 5, 6, 255));
        var map = MapFormat.CreateTileAssetMap("Borne", 4, 4);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers.Add(new Layer { LayerType = LayerType.Mask });
        MapEditOperations.PaintTile(map, 0, 0, 0, new Tile { Type = TileType.Ground, AssetId = red });
        MapEditOperations.PaintTile(map, 0, 3, 3, new Tile { Type = TileType.Ground, AssetId = green });
        MapEditOperations.PaintTile(map, 1, 3, 0, new Tile { Type = TileType.Ground, AssetId = green });
        var kept = map.Layers[0].Tiles.Single(t => t.X == 0 && t.Y == 0);

        var entities = new List<MapPlacedEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Kind = MapPlacedKind.Object,
                TileX = 3,
                TileY = 1,
                Name = "Objet 1",
                Level = 1,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Kind = MapPlacedKind.Npc,
                TileX = 1,
                TileY = 1,
                Name = "PNJ 1",
                Level = 2,
            },
        };
        var prefabs = new List<PrefabPlacement>
        {
            new() { PrefabId = "large", TileX = 2, TileY = 2 },
            new() { PrefabId = "petit", TileX = 0, TileY = 0 },
        };
        var outside = new MapEventPlacementDefinition
        {
            Id = Guid.NewGuid(),
            MapId = Guid.NewGuid(),
            EventDefinitionId = Guid.NewGuid(),
            TileX = 3,
            TileY = 3,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            RouteWaypoints = [new MapEventRouteWaypoint { TileX = 1, TileY = 1, WaitMs = 5 }],
        };
        var inside = new MapEventPlacementDefinition
        {
            Id = Guid.NewGuid(),
            MapId = outside.MapId,
            EventDefinitionId = Guid.NewGuid(),
            TileX = 1,
            TileY = 0,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            RouteWaypoints =
            [
                new MapEventRouteWaypoint { TileX = 0, TileY = 0, WaitMs = 1 },
                new MapEventRouteWaypoint { TileX = 3, TileY = 0, WaitMs = 2 },
            ],
        };
        var events = new List<MapEventPlacementDefinition> { outside, inside };
        var spawn = new MapResizeShiftSpawn { X = 3, Y = 3 };
        var edit = new MapResizeShiftEdit { Width = 3, Height = 3, DeltaX = 0, DeltaY = 0 };
        (int Width, int Height)? Footprint(PrefabPlacement placement)
            => placement.PrefabId == "large" ? (2, 2) : null;

        Assert.True(MapResizeShift.TryPreview(map, edit, entities, prefabs, events, spawn, out var preview, out _, Footprint));
        Assert.True(preview.RemovedAnything);
        Assert.Contains("TileAssetId", MapResizeShift.FormatRemovalConfirm(preview), StringComparison.Ordinal);
        Assert.Contains("tuile", MapResizeShift.FormatRemovalConfirm(preview), StringComparison.Ordinal);

        Assert.True(MapResizeShift.TryApply(map, edit, entities, prefabs, events, spawn, out var report, out var error, Footprint));
        Assert.Null(error);
        Assert.Equal(3, map.Width);
        Assert.Equal(3, map.Height);
        Assert.Equal(48, map.TileSizePixels);
        Assert.Equal(TileGraphicIdentity.TileAsset, map.GraphicIdentity);
        Assert.Equal(1, report.TilesKept);
        Assert.Equal(2, report.TilesRemoved);
        Assert.Same(kept, Assert.Single(map.Layers[0].Tiles));
        Assert.Equal(red, kept.AssetId);
        Assert.Equal(0, kept.SrcX);
        Assert.Empty(map.Layers[1].Tiles);
        Assert.Equal("PNJ 1", Assert.Single(entities).Name);
        Assert.Equal(1, entities[0].TileX);
        Assert.Equal("petit", Assert.Single(prefabs).PrefabId);
        Assert.Same(inside, Assert.Single(events));
        Assert.Equal(1, inside.TileX);
        Assert.Equal(0, Assert.Single(inside.RouteWaypoints).TileX);
        Assert.Equal(2, report.WaypointsRemoved);
        Assert.False(spawn.IsSet);
        Assert.False(report.SpawnKept);
        Assert.Contains("départ retiré", MapResizeShift.FormatStatus(report), StringComparison.Ordinal);
        Assert.True(map.Validate(out var validateError), validateError);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Fact]
    public void InvalidOrNoOp_DoesNotMutate_SheetMapStaysV5()
    {
        var map = new Map { Name = "Feuille", Width = 4, Height = 4 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground, TilesetId = 2, SrcX = 16, SrcY = 32 });
        var tile = map.Layers[0].Tiles.Single();
        var called = false;

        Assert.False(MapResizeShift.TryApply(
            map,
            new MapResizeShiftEdit { Width = 0, Height = 4, DeltaX = 0, DeltaY = 0 },
            null,
            null,
            null,
            null,
            out _,
            out var error,
            beforeMutate: () => called = true));
        Assert.False(called);
        Assert.Contains("512", error, StringComparison.Ordinal);
        Assert.Equal(1, tile.X);
        Assert.Equal(16, tile.SrcX);
        Assert.Equal(TileGraphicIdentity.SheetSource, map.GraphicIdentity);
        Assert.Equal(0, map.TileSizePixels);

        Assert.False(MapResizeShift.TryApply(
            map,
            new MapResizeShiftEdit { Width = 4, Height = 4, DeltaX = 0, DeltaY = 0 },
            null,
            null,
            null,
            null,
            out _,
            out var none,
            beforeMutate: () => called = true));
        Assert.False(called);
        Assert.Null(none);
        Assert.Equal(1, tile.X);

        Assert.False(MapResizeShift.TryApply(
            map,
            new MapResizeShiftEdit { Width = 4, Height = 4, DeltaX = 600, DeltaY = 0 },
            null,
            null,
            null,
            null,
            out _,
            out var deltaError));
        Assert.Contains("décalage", deltaError, StringComparison.OrdinalIgnoreCase);

        var edit = new MapResizeShiftEdit { Width = 5, Height = 4, DeltaX = 1, DeltaY = 0 };
        Assert.True(MapResizeShift.TryApply(map, edit, null, null, null, null, out _, out _));
        Assert.Equal(2, tile.X);
        Assert.Equal(1, tile.Y);
        Assert.Equal(2, tile.TilesetId);
        Assert.Equal(16, tile.SrcX);
        Assert.Equal(32, tile.SrcY);
        Assert.True(tile.AssetId.IsNone);
        Assert.Equal(0, map.TileSizePixels);
        Assert.Equal(TileGraphicIdentity.SheetSource, map.GraphicIdentity);
        Assert.Equal(MapSerializer.MapFileFormatVersion, new MapSerializer().Serialize(map)[4]);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    [Fact]
    public void SameEventDefinition_ShiftPreservesDistinctTiles()
    {
        var shared = Guid.NewGuid();
        var mapId = Guid.NewGuid();
        var first = new MapEventPlacementDefinition
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            EventDefinitionId = shared,
            TileX = 1,
            TileY = 0,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
        };
        var second = new MapEventPlacementDefinition
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            EventDefinitionId = shared,
            TileX = 2,
            TileY = 0,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
        };
        var events = new List<MapEventPlacementDefinition> { first, second };
        MapResizeShift.ShiftPlacements(events, 1, 0, 8, 8, out var kept, out var removed, out _, out _);
        Assert.Equal(2, kept);
        Assert.Equal(0, removed);
        Assert.Equal(2, first.TileX);
        Assert.Equal(3, second.TileX);
        Assert.Equal(shared, first.EventDefinitionId);
        Assert.Equal(shared, second.EventDefinitionId);
    }

    private static byte[] SolidRgba(byte r, byte g, byte b, byte a)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = a;
        }

        return bytes;
    }
}
