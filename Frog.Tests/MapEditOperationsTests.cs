using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEditOperationsTests
{
    [Fact]
    public void PaintTile_SetsDirtyCandidate_OnEditableLayer()
    {
        var map = CreateMap();
        var stamp = new Tile { Type = TileType.Ground, SrcX = 1, SrcY = 2, TilesetId = 1 };
        MapEditOperations.PaintTile(map, 0, 1, 1, stamp);
        var tile = map.Layers[0].Tiles.Single(t => t.X == 1 && t.Y == 1);
        Assert.Equal(TileType.Ground, tile.Type);
        Assert.Equal(1, tile.SrcX);
    }

    [Fact]
    public void PaintTile_DoesNotModifyLockedLayer()
    {
        var map = CreateMap();
        map.Layers[0].Locked = true;
        MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground });
        Assert.Empty(map.Layers[0].Tiles);
    }

    [Fact]
    public void PaintTile_CopiesBlockWarpAndResourceAttributes()
    {
        var map = CreateMap();
        var warpTarget = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var stamp = new Tile
        {
            Type = TileType.Warp,
            WarpTargetMapId = warpTarget,
            WarpTargetX = 2,
            WarpTargetY = 3,
            Attributes =
            {
                new BlockAttribute(),
                new WarpAttribute { TargetMapId = warpTarget, TargetX = 2, TargetY = 3 },
                new ResourceAttribute { ResourceId = 9 },
            },
        };

        MapEditOperations.PaintTile(map, 0, 1, 1, stamp);
        var tile = map.Layers[0].Tiles.Single();
        Assert.Equal(3, tile.Attributes.Count);
        Assert.Contains(tile.Attributes, attribute => attribute is BlockAttribute);
        var warp = Assert.IsType<WarpAttribute>(tile.Attributes[1]);
        Assert.Equal(warpTarget, warp.TargetMapId);
        Assert.Equal(9, Assert.IsType<ResourceAttribute>(tile.Attributes[2]).ResourceId);
        Assert.NotSame(stamp.Attributes[0], tile.Attributes[0]);

        map.Layers.Add(new Layer { LayerType = LayerType.Fringe });
        MapEditOperations.PaintTile(map, 1, 0, 0, new Tile { Type = TileType.Ground, SrcX = 5 });
        map.Layers[1].Locked = true;
        MapEditOperations.EraseRectangle(map, 1, 0, 0, 1, 1);
        Assert.Contains(map.Layers[1].Tiles, tile => tile.X == 0 && tile.Y == 0 && tile.SrcX == 5);
    }

    [Fact]
    public void PaintTile_PreservesTileAssetId_AndFloodFillDoesNotCrossIds()
    {
        var red = TileAssetId.FromStraightRgba(SolidRgba(255, 0, 0, 255));
        var blue = TileAssetId.FromStraightRgba(SolidRgba(0, 0, 255, 255));
        var map = MapFormat.CreateTileAssetMap("TileAsset", 2, 1);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        MapEditOperations.PaintTile(map, 0, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });
        MapEditOperations.PaintTile(map, 0, 1, 0, new Tile { AssetId = blue, Type = TileType.Ground });

        var left = map.Layers[0].Tiles.Single(tile => tile.X == 0);
        Assert.Equal(red, left.AssetId);
        Assert.Equal(0, left.TilesetId);
        Assert.Equal(0, left.SrcX);
        Assert.Equal(0, left.SrcY);

        MapEditOperations.FloodFill(map, 0, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });
        Assert.Equal(blue, map.Layers[0].Tiles.Single(tile => tile.X == 1).AssetId);
        Assert.Equal(TileAssetMetrics.TargetTileSizePixels, map.TileSizePixels);
    }

    [Fact]
    public void EraseTile_RemovesTile()
    {
        var map = CreateMap();
        MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground });
        MapEditOperations.EraseTile(map, 0, 1, 1);
        Assert.Empty(map.Layers[0].Tiles);
    }

    [Fact]
    public void EnumerateLine_IsOneTileWideAndIncludesBothEnds()
    {
        var diagonal = MapEditOperations.EnumerateLine(0, 0, 4, 2);
        Assert.Equal(new[] { (0, 0), (1, 0), (2, 1), (3, 1), (4, 2) }, diagonal);
        Assert.Equal(new[] { (2, 2) }, MapEditOperations.EnumerateLine(2, 2, 2, 2));
        Assert.Equal(new[] { (0, 0), (1, 0), (2, 0), (3, 0) }, MapEditOperations.EnumerateLine(0, 0, 3, 0));
        Assert.Equal(new[] { (1, 0), (1, 1), (1, 2) }, MapEditOperations.EnumerateLine(1, 0, 1, 2));

        var back = MapEditOperations.EnumerateLine(4, 2, 0, 0);
        Assert.Equal((4, 2), back[0]);
        Assert.Equal((0, 0), back[^1]);
        Assert.Equal(Math.Max(4, 2) + 1, back.Count);
        Assert.DoesNotContain((0, 2), back);
    }

    [Fact]
    public void PaintLine_PaintsOnlyBresenhamCells()
    {
        var map = CreateMap();
        var stamp = new Tile { Type = TileType.Ground, SrcX = 7, TilesetId = 3 };
        MapEditOperations.PaintLine(map, 0, 0, 0, 4, 2, stamp);

        var tiles = map.Layers[0].Tiles;
        Assert.Equal(5, tiles.Count);
        Assert.All(tiles, t => Assert.Equal(7, t.SrcX));
        Assert.Contains(tiles, t => t.X == 0 && t.Y == 0);
        Assert.Contains(tiles, t => t.X == 4 && t.Y == 2);
        Assert.DoesNotContain(tiles, t => t.X == 0 && t.Y == 2);
        Assert.DoesNotContain(tiles, t => t.X == 4 && t.Y == 0);
    }

    [Fact]
    public void PaintLine_SkipsLockedLayerAndOutOfBounds()
    {
        var map = CreateMap();
        map.Layers[0].Locked = true;
        MapEditOperations.PaintLine(map, 0, 0, 0, 2, 0, new Tile { Type = TileType.Ground });
        Assert.Empty(map.Layers[0].Tiles);

        map.Layers[0].Locked = false;
        MapEditOperations.PaintLine(map, 0, -1, 0, 2, 0, new Tile { Type = TileType.Ground, SrcX = 1 });
        Assert.Equal(3, map.Layers[0].Tiles.Count);
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X < 0);
    }

    [Fact]
    public void ConstrainToDominantAxis_PrefersHorizontalOnTie()
    {
        Assert.Equal((4, 0), MapEditOperations.ConstrainToDominantAxis(0, 0, 4, 2));
        Assert.Equal((0, 3), MapEditOperations.ConstrainToDominantAxis(0, 0, 2, 3));
        Assert.Equal((3, 0), MapEditOperations.ConstrainToDominantAxis(0, 0, 3, 3));
    }

    [Fact]
    public void PaintRectangle_FillsArea()
    {
        var map = CreateMap();
        var stamp = new Tile { Type = TileType.Ground };
        MapEditOperations.PaintRectangle(map, 0, 0, 0, 2, 2, stamp);
        Assert.Equal(9, map.Layers[0].Tiles.Count);
    }

    [Fact]
    public void EnumerateShape_RectangleBounds_OutlineAndReversedCorners()
    {
        Assert.Equal(11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var filled = MapEditOperations.EnumerateShape(0, 0, 3, 2);
        Assert.Equal(12, filled.Count);
        Assert.Contains((0, 0), filled);
        Assert.Contains((3, 2), filled);
        Assert.Contains((1, 1), filled);
        Assert.Equal(
            filled.OrderBy(c => c.Y).ThenBy(c => c.X),
            MapEditOperations.EnumerateShape(3, 2, 0, 0).OrderBy(c => c.Y).ThenBy(c => c.X));

        var outline = MapEditOperations.EnumerateShape(0, 0, 3, 2, new ShapeStampOptions { Outline = true });
        Assert.Equal(10, outline.Count);
        Assert.Contains((0, 0), outline);
        Assert.Contains((3, 2), outline);
        Assert.DoesNotContain((1, 1), outline);
        Assert.DoesNotContain((2, 1), outline);
        Assert.Equal(new[] { (4, 4) }, MapEditOperations.EnumerateShape(4, 4, 4, 4, new ShapeStampOptions { Outline = true }));
    }

    [Fact]
    public void EnumerateShape_EllipseIsInsideRectangle_AndOutlineDropsInterior()
    {
        var box = new HashSet<(int X, int Y)>(MapEditOperations.EnumerateShape(0, 0, 4, 2));
        var ellipse = MapEditOperations.EnumerateShape(0, 0, 4, 2, new ShapeStampOptions { Ellipse = true });
        Assert.NotEmpty(ellipse);
        Assert.All(ellipse, cell => Assert.Contains(cell, box));
        Assert.Contains((2, 1), ellipse);
        Assert.Contains((0, 1), ellipse);
        Assert.DoesNotContain((0, 0), ellipse);
        Assert.DoesNotContain((4, 2), ellipse);
        Assert.True(ellipse.Count < box.Count);

        var rim = MapEditOperations.EnumerateShape(0, 0, 4, 2, new ShapeStampOptions { Ellipse = true, Outline = true });
        Assert.Contains((0, 1), rim);
        Assert.DoesNotContain((2, 1), rim);
        Assert.DoesNotContain((0, 0), rim);
        Assert.All(rim, cell => Assert.Contains(cell, ellipse));
        Assert.True(rim.Count < ellipse.Count);
    }

    [Fact]
    public void PaintShape_ClipsToMap_OneCallback_SkipsLockedHiddenAndOtherLayers()
    {
        var map = CreateMap();
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe });
        MapEditOperations.PaintTile(map, 1, 0, 0, Sheet(9));

        var callbacks = 0;
        var changed = MapEditOperations.PaintShape(
            map,
            0,
            -2,
            -2,
            1,
            1,
            Sheet(4),
            default,
            () => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.Equal(4, changed);
        Assert.Equal(4, map.Layers[0].Tiles.Count);
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X < 0 || t.Y < 0);
        Assert.All(map.Layers[0].Tiles, t => Assert.Equal(4, t.SrcX));
        Assert.Equal(9, map.Layers[1].Tiles.Single().SrcX);

        map.Layers[0].Locked = true;
        Assert.Equal(0, MapEditOperations.PaintShape(map, 0, 0, 0, 2, 2, Sheet(7), default, () => callbacks++));
        Assert.Equal(1, callbacks);
        Assert.Equal(4, map.Layers[0].Tiles.Single(t => t.X == 0 && t.Y == 0).SrcX);

        map.Layers[0].Locked = false;
        map.Layers[0].Visible = false;
        Assert.Equal(0, MapEditOperations.PaintShape(map, 0, 0, 0, 2, 2, Sheet(7), default, () => callbacks++));
        Assert.Equal(1, callbacks);
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.SrcX == 7);
    }

    [Fact]
    public void PaintShape_OutlineAndEllipse_DoNotFillInterior_TileAssetIdKept()
    {
        var map = CreateMap();
        var callbacks = 0;
        var outline = MapEditOperations.PaintShape(
            map,
            0,
            0,
            0,
            3,
            2,
            Sheet(3),
            new ShapeStampOptions { Outline = true },
            () => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.Equal(10, outline);
        Assert.Null(FindTile(map, 0, 1, 1));
        Assert.Null(FindTile(map, 0, 2, 1));
        Assert.Equal(3, TileAt(map, 0, 0, 0).SrcX);
        Assert.Equal(3, TileAt(map, 0, 3, 2).SrcX);

        var red = TileAssetId.FromStraightRgba(SolidRgba(255, 0, 0, 255));
        var assets = MapFormat.CreateTileAssetMap("ellipse", 5, 3);
        assets.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var painted = MapEditOperations.PaintShape(
            assets,
            0,
            0,
            0,
            4,
            2,
            new Tile { AssetId = red, Type = TileType.Ground },
            new ShapeStampOptions { Ellipse = true });
        Assert.True(painted > 0);
        Assert.Equal(red, TileAt(assets, 0, 2, 1).AssetId);
        Assert.Equal(0, TileAt(assets, 0, 2, 1).TilesetId);
        Assert.Null(FindTile(assets, 0, 0, 0));
        Assert.Equal(TileAssetMetrics.TargetTileSizePixels, assets.TileSizePixels);
        Assert.DoesNotContain(assets.Layers[0].Tiles, t => t.X < 0 || t.Y < 0 || t.X >= assets.Width || t.Y >= assets.Height);
    }

    [Fact]
    public void FloodFill_StaysWithinMapBounds()
    {
        var map = CreateMap();
        var changed = MapEditOperations.FloodFill(map, 0, 0, 0, new Tile { Type = TileType.Ground, SrcX = 5 });
        Assert.Equal(map.Width * map.Height, changed);
        Assert.Equal(map.Width * map.Height, map.Layers[0].Tiles.Count);
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X < 0 || t.Y < 0 || t.X >= map.Width || t.Y >= map.Height);
        Assert.Equal(0, MapEditOperations.FloodFill(map, 0, -1, 0, new Tile { Type = TileType.Ground, SrcX = 5 }));
    }

    [Fact]
    public void FloodFill_Is4Connected_StopsAtDifferentTile_AndMapEdge()
    {
        Assert.Equal(11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);

        var map = CreateMap();
        MapEditOperations.PaintTile(map, 0, 0, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 1, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 0, 1, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 2, 1, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 2, 0, Sheet(9));

        var callbacks = 0;
        var changed = MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(4), default, () => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.Equal(3, changed);
        Assert.Equal(4, TileAt(map, 0, 0, 0).SrcX);
        Assert.Equal(4, TileAt(map, 0, 1, 0).SrcX);
        Assert.Equal(4, TileAt(map, 0, 0, 1).SrcX);
        Assert.Equal(1, TileAt(map, 0, 2, 1).SrcX);
        Assert.Equal(9, TileAt(map, 0, 2, 0).SrcX);

        Assert.Equal(0, MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(4), default, () => callbacks++));
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public void FloodFill_EraseClearsMatchingRegionOnly()
    {
        var map = CreateMap();
        MapEditOperations.PaintTile(map, 0, 0, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 1, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 2, 0, Sheet(8));

        var changed = MapEditOperations.FloodFill(map, 0, 0, 0, replacement: null);
        Assert.Equal(2, changed);
        Assert.Null(FindTile(map, 0, 0, 0));
        Assert.Null(FindTile(map, 0, 1, 0));
        Assert.Equal(8, TileAt(map, 0, 2, 0).SrcX);
        Assert.Equal(0, MapEditOperations.FloodFill(map, 0, 0, 0, replacement: null));
    }

    [Fact]
    public void FloodFill_SkipsLockedAndHiddenLayers()
    {
        var map = CreateMap();
        MapEditOperations.PaintTile(map, 0, 0, 0, Sheet(1));
        map.Layers[0].Locked = true;
        Assert.Equal(0, MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(4)));
        Assert.Equal(1, TileAt(map, 0, 0, 0).SrcX);

        map.Layers[0].Locked = false;
        map.Layers[0].Visible = false;
        Assert.Equal(0, MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(4)));
        Assert.Equal(1, TileAt(map, 0, 0, 0).SrcX);
    }

    [Fact]
    public void FloodFill_VisibleUnlockedLayers_SkipsHiddenLockedAndAttributesUnlessActive()
    {
        var map = CreateMap();
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe, Visible = true });
        map.Layers.Add(new Layer { LayerType = LayerType.Mask, Visible = true });
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe2, Visible = true });
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes, Visible = true });

        MapEditOperations.PaintTile(map, 0, 0, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 1, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 1, 0, 0, Sheet(2));
        MapEditOperations.PaintTile(map, 1, 1, 0, Sheet(2));
        MapEditOperations.PaintTile(map, 2, 0, 0, Sheet(3));
        MapEditOperations.PaintTile(map, 3, 0, 0, Sheet(4));
        MapEditOperations.PaintTile(map, 4, 0, 0, Sheet(5));
        MapEditOperations.PaintTile(map, 4, 1, 0, Sheet(5));
        map.Layers[2].Visible = false;
        map.Layers[3].Locked = true;

        var options = new FloodFillOptions { VisibleUnlockedLayers = true };
        var changed = MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(7), options);
        Assert.Equal(4, changed);
        Assert.Equal(7, TileAt(map, 0, 0, 0).SrcX);
        Assert.Equal(7, TileAt(map, 0, 1, 0).SrcX);
        Assert.Equal(7, TileAt(map, 1, 0, 0).SrcX);
        Assert.Equal(7, TileAt(map, 1, 1, 0).SrcX);
        Assert.Equal(3, TileAt(map, 2, 0, 0).SrcX);
        Assert.Equal(4, TileAt(map, 3, 0, 0).SrcX);
        Assert.Equal(5, TileAt(map, 4, 0, 0).SrcX);
        Assert.Equal(5, TileAt(map, 4, 1, 0).SrcX);

        var attrChanged = MapEditOperations.FloodFill(map, 4, 0, 0, Sheet(6), options);
        Assert.Equal(2, attrChanged);
        Assert.Equal(6, TileAt(map, 4, 0, 0).SrcX);
        Assert.Equal(6, TileAt(map, 4, 1, 0).SrcX);
        Assert.Equal(7, TileAt(map, 0, 0, 0).SrcX);
    }

    [Fact]
    public void FloodFill_RespectAttributes_StopsAtTileFlagsAndAttributesLayer()
    {
        var map = CreateMap();
        var plain = Sheet(1);
        var blocked = Sheet(1);
        blocked.Attributes.Add(new BlockAttribute());
        MapEditOperations.PaintTile(map, 0, 0, 0, plain);
        MapEditOperations.PaintTile(map, 0, 1, 0, blocked);
        MapEditOperations.PaintTile(map, 0, 2, 0, Sheet(1));

        var respect = new FloodFillOptions { RespectAttributes = true };
        Assert.Equal(1, MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(4), respect));
        Assert.Equal(4, TileAt(map, 0, 0, 0).SrcX);
        Assert.Equal(1, TileAt(map, 0, 1, 0).SrcX);
        Assert.Contains(TileAt(map, 0, 1, 0).Attributes, a => a is BlockAttribute);
        Assert.Equal(1, TileAt(map, 0, 2, 0).SrcX);

        map.Layers[0].Tiles.Clear();
        MapEditOperations.PaintTile(map, 0, 0, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 1, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 2, 0, Sheet(1));
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes, Visible = true });
        MapEditOperations.PaintTile(map, 1, 1, 0, new Tile { Type = TileType.Block, Attributes = { new BlockAttribute() } });

        Assert.Equal(1, MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(3), respect));
        Assert.Equal(3, TileAt(map, 0, 0, 0).SrcX);
        Assert.Equal(1, TileAt(map, 0, 1, 0).SrcX);
        Assert.Equal(1, TileAt(map, 0, 2, 0).SrcX);
        Assert.Equal(TileType.Block, TileAt(map, 1, 1, 0).Type);

        map.Layers[0].Tiles.Clear();
        MapEditOperations.PaintTile(map, 0, 0, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 1, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 2, 0, Sheet(1));
        Assert.Equal(3, MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(8)));
        Assert.Equal(8, TileAt(map, 0, 0, 0).SrcX);
        Assert.Equal(8, TileAt(map, 0, 2, 0).SrcX);
        Assert.Equal(TileType.Block, TileAt(map, 1, 1, 0).Type);
    }

    [Fact]
    public void FloodFill_LargeEmptyRegion_TerminatesOnIterativeFill()
    {
        var map = new Map { Name = "Large", Width = 32, Height = 32 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var changed = MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(2));
        Assert.Equal(32 * 32, changed);
        Assert.Equal(32 * 32, map.Layers[0].Tiles.Count);
    }

    [Fact]
    public void FloodFill_SnapshotRestoresSheetAndTileAssetMaps()
    {
        var map = CreateMap();
        MapEditOperations.PaintTile(map, 0, 0, 0, Sheet(1));
        MapEditOperations.PaintTile(map, 0, 1, 0, Sheet(1));
        var serializer = new MapSerializer();
        var before = serializer.Serialize(map);
        Assert.Equal(2, MapEditOperations.FloodFill(map, 0, 0, 0, Sheet(4)));
        var restored = serializer.Deserialize(before);
        Assert.Equal(1, restored.Layers[0].Tiles.Single(t => t.X == 0).SrcX);
        Assert.Equal(1, restored.Layers[0].Tiles.Single(t => t.X == 1).SrcX);

        var red = TileAssetId.FromStraightRgba(SolidRgba(255, 0, 0, 255));
        var green = TileAssetId.FromStraightRgba(SolidRgba(0, 255, 0, 255));
        var assets = MapFormat.CreateTileAssetMap("v6", 2, 1);
        assets.Layers.Add(new Layer { LayerType = LayerType.Ground });
        MapEditOperations.PaintTile(assets, 0, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });
        var beforeV6 = serializer.Serialize(assets);
        Assert.Equal(TileGraphicIdentity.TileAsset, assets.GraphicIdentity);
        Assert.Equal(6, MapSerializer.TileAssetMapFileFormatVersion);
        MapEditOperations.FloodFill(assets, 0, 0, 0, new Tile { AssetId = green, Type = TileType.Ground });
        Assert.Equal(green, assets.Layers[0].Tiles.Single().AssetId);
        var restoredV6 = serializer.Deserialize(beforeV6);
        Assert.Equal(TileGraphicIdentity.TileAsset, restoredV6.GraphicIdentity);
        Assert.Equal(red, restoredV6.Layers[0].Tiles.Single().AssetId);
        Assert.Equal(TileAssetMetrics.TargetTileSizePixels, restoredV6.TileSizePixels);
    }

    [Fact]
    public void SetBlockTile_CreatesBlock()
    {
        var map = CreateMap();
        MapEditOperations.SetBlockTile(map, 0, 2, 2);
        var tile = map.Layers[0].Tiles.Single();
        Assert.Equal(TileType.Block, tile.Type);
    }

    [Fact]
    public void SetWarpDestination_StoresTarget()
    {
        var map = CreateMap();
        var targetId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        MapEditOperations.SetWarpDestination(map, 0, 1, 1, targetId, 3, 4);
        var tile = map.Layers[0].Tiles.Single();
        Assert.Equal(TileType.Warp, tile.Type);
        Assert.Equal(targetId, tile.WarpTargetMapId);
        Assert.Equal(3, tile.WarpTargetX);
        Assert.Equal(4, tile.WarpTargetY);
    }

    [Fact]
    public void LayerOperations_UpdateMetadata()
    {
        var map = CreateMap();
        MapEditOperations.AddLayer(map, LayerType.Mask);
        Assert.Equal(2, map.Layers.Count);
        MapEditOperations.RenameLayer(map, 1, "Masque");
        Assert.Equal("Masque", map.Layers[1].DisplayName);
        MapEditOperations.ChangeLayerType(map, 1, LayerType.Fringe);
        Assert.Equal(LayerType.Fringe, map.Layers[1].LayerType);
        MapEditOperations.SetLayerVisibility(map, 1, false);
        Assert.False(map.Layers[1].Visible);
        MapEditOperations.SetLayerLocked(map, 1, true);
        Assert.True(map.Layers[1].Locked);
        MapEditOperations.RemoveLayer(map, 1);
        Assert.Single(map.Layers);
    }

    [Fact]
    public async Task SaveAndReload_PreservesEditedModel()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();
        var map = session.CurrentMap!;
        MapEditOperations.SetBlockTile(map, map.Layers.FindIndex(l => l.LayerType == LayerType.Attributes), 2, 2);
        session.MarkDirty();

        Assert.IsType<SaveMapResult.Success>(await session.SaveCurrentAsync(SaveMapIntent.SaveDraft));

        var reloaded = await repo.LoadByIdAsync(session.CurrentMapId!.Value);
        Assert.NotNull(reloaded);
        var attr = reloaded.Map.Layers.First(l => l.LayerType == LayerType.Attributes);
        Assert.Contains(attr.Tiles, t => t.Type == TileType.Block && t.X == 2 && t.Y == 2);
    }

    [Fact]
    public void EraseStamp_ClearsActiveLayer_LeavesMaskAndLockedHiddenUntouched()
    {
        var map = CreateMap();
        map.Layers.Add(new Layer { LayerType = LayerType.Mask });
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes });
        MapEditOperations.PaintTile(map, 0, 1, 1, Sheet(4));
        MapEditOperations.PaintTile(map, 0, 2, 1, Sheet(5));
        MapEditOperations.PaintTile(map, 0, 3, 1, Sheet(6));
        MapEditOperations.PaintTile(map, 1, 1, 1, Sheet(7));
        MapEditOperations.PaintTile(map, 2, 1, 1, new Tile { Type = TileType.Block });

        var undo = false;
        Assert.Equal(2, MapEditOperations.EraseStamp(map, 0, 1, 1, 2, 1, () => undo = true));
        Assert.True(undo);
        Assert.Null(FindTile(map, 0, 1, 1));
        Assert.Null(FindTile(map, 0, 2, 1));
        Assert.Equal(6, TileAt(map, 0, 3, 1).SrcX);
        Assert.Equal(7, TileAt(map, 1, 1, 1).SrcX);
        Assert.Equal(TileType.Block, TileAt(map, 2, 1, 1).Type);

        map.Layers[1].Visible = false;
        undo = false;
        Assert.Equal(0, MapEditOperations.EraseStamp(map, 1, 1, 1, 1, 1, () => undo = true));
        Assert.False(undo);
        Assert.Equal(7, TileAt(map, 1, 1, 1).SrcX);

        map.Layers[1].Visible = true;
        map.Layers[1].Locked = true;
        Assert.Equal(0, MapEditOperations.EraseStamp(map, 1, 1, 1, 1, 1));
        Assert.Equal(7, TileAt(map, 1, 1, 1).SrcX);

        undo = false;
        Assert.Equal(0, MapEditOperations.EraseStamp(map, 0, 0, 0, 1, 1, () => undo = true));
        Assert.False(undo);
    }

    [Fact]
    public void TryApplyProperties_RenamesResizesOverlap_DropsTilesOutside_KeepsFileIdentity()
    {
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);

        var map = CreateMap();
        map.AllowPlayerOverlap = false;
        map.Bgm = new MapAudioTrack { Asset = "Assets/Audio/music-loop.wav", Volume = 70, FadeMs = 400 };
        map.Layers.Add(new Layer { LayerType = LayerType.Mask });
        MapEditOperations.PaintTile(map, 0, 1, 1, Sheet(2));
        MapEditOperations.PaintTile(map, 0, 4, 4, Sheet(9));
        MapEditOperations.PaintTile(map, 1, 1, 1, Sheet(3));
        MapEditOperations.PaintTile(map, 1, 4, 1, Sheet(8));
        var identity = map.GraphicIdentity;
        var tileSize = map.TileSizePixels;

        var unchanged = new MapPropertiesEdit
        {
            Name = map.Name,
            Width = map.Width,
            Height = map.Height,
            AllowPlayerOverlap = false,
        };
        var called = false;
        Assert.False(MapEditOperations.TryApplyProperties(map, unchanged, out var none, () => called = true));
        Assert.Null(none);
        Assert.False(called);

        Assert.False(MapEditOperations.TryApplyProperties(
            map,
            new MapPropertiesEdit { Name = "x", Width = 0, Height = 4, AllowPlayerOverlap = true },
            out var error));
        Assert.Contains("512", error, StringComparison.Ordinal);
        Assert.Equal(5, map.Width);

        called = false;
        Assert.True(MapEditOperations.TryApplyProperties(
            map,
            new MapPropertiesEdit
            {
                Name = "  Clairière  ",
                Width = 3,
                Height = 4,
                AllowPlayerOverlap = true,
            },
            out var ok,
            () => called = true));
        Assert.Null(ok);
        Assert.True(called);
        Assert.Equal("Clairière", map.Name);
        Assert.Equal(3, map.Width);
        Assert.Equal(4, map.Height);
        Assert.True(map.AllowPlayerOverlap);
        Assert.Equal(identity, map.GraphicIdentity);
        Assert.Equal(tileSize, map.TileSizePixels);
        Assert.Equal(2, TileAt(map, 0, 1, 1).SrcX);
        Assert.Null(FindTile(map, 0, 4, 4));
        Assert.Equal(3, TileAt(map, 1, 1, 1).SrcX);
        Assert.Null(FindTile(map, 1, 4, 1));
        Assert.True(map.Validate(out var validateError), validateError);

        var roundTrip = new MapSerializer().Deserialize(new MapSerializer().Serialize(map));
        Assert.Equal("Clairière", roundTrip.Name);
        Assert.Equal(3, roundTrip.Width);
        Assert.Equal(4, roundTrip.Height);
        Assert.True(roundTrip.AllowPlayerOverlap);
        Assert.Equal(TileGraphicIdentity.SheetSource, roundTrip.GraphicIdentity);
        Assert.Equal(0, roundTrip.TileSizePixels);
        Assert.Equal("Assets/Audio/music-loop.wav", roundTrip.Bgm.Asset);
        Assert.Equal(70, roundTrip.Bgm.Volume);
        Assert.Equal(400, roundTrip.Bgm.FadeMs);
        Assert.True(roundTrip.Se.IsNone);
        Assert.Equal(MapSerializer.MapFileFormatVersion, new MapSerializer().Serialize(map)[4]);

        var summary = MapEditOperations.FormatPropertiesSummary(map, 1, 2);
        Assert.Contains("Nom : Clairière", summary, StringComparison.Ordinal);
        Assert.Contains("3 × 4", summary, StringComparison.Ordinal);
        Assert.Contains("Chevauchement : oui", summary, StringComparison.Ordinal);
        Assert.Contains("Musique : music-loop.wav (70 %, fondu 400 ms)", summary, StringComparison.Ordinal);
        Assert.Contains("Ambiance : aucune", summary, StringComparison.Ordinal);
        Assert.Contains($"Feuille (v5, {WorldMetrics.DefaultTileSizePixels} px)", summary, StringComparison.Ordinal);
        Assert.Contains("Départ playtest : (1, 2)", summary, StringComparison.Ordinal);
        Assert.Equal("Départ playtest : non défini", MapEditOperations.FormatSpawnMemo(null, null));
    }

    [Fact]
    public void TryApplyProperties_TileAssetMap_Keeps48pxIdentity()
    {
        var map = MapFormat.CreateTileAssetMap("Asset", 4, 4);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Se = new MapAudioTrack { Asset = "MusicLoop", Volume = 40, FadeMs = 0 };
        Assert.True(MapEditOperations.TryApplyProperties(
            map,
            new MapPropertiesEdit { Name = "Asset 2", Width = 2, Height = 2, AllowPlayerOverlap = false },
            out _));
        Assert.Equal(TileGraphicIdentity.TileAsset, map.GraphicIdentity);
        Assert.Equal(TileAssetMetrics.TargetTileSizePixels, map.TileSizePixels);
        Assert.Equal("MusicLoop", map.Se.Asset);
        Assert.Equal(40, map.Se.Volume);
        Assert.True(map.Bgm.IsNone);
        Assert.Contains("v6, 48 px", MapEditOperations.FormatGraphicIdentity(map), StringComparison.Ordinal);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    private static Map CreateMap()
    {
        var map = new Map { Name = "Test", Width = 5, Height = 5 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        return map;
    }

    private static Tile Sheet(int srcX) => new()
    {
        Type = TileType.Ground,
        SrcX = srcX,
        TilesetId = 1,
    };

    private static Tile? FindTile(Map map, int layer, int x, int y)
        => map.Layers[layer].Tiles.SingleOrDefault(t => t.X == x && t.Y == y);

    private static Tile TileAt(Map map, int layer, int x, int y)
        => FindTile(map, layer, x, y) ?? throw new InvalidOperationException($"Tuile absente ({x},{y}).");

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
