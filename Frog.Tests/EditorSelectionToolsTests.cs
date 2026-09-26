using System;
using System.Linq;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class EditorSelectionToolsTests
{
    [Fact]
    public void Rotate90Clockwise_RemapsLShapeAndSwapsBounds()
    {
        var tiles = new[]
        {
            TileAt(0, 0, 1, 10, 0),
            TileAt(1, 0, 1, 20, 0),
            TileAt(0, 1, 1, 10, 32),
        };

        var result = TileSelectionTransform.Apply(tiles, width: 2, height: 2, TileSelectionTransformKind.Rotate90Clockwise);

        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Contains(result.Tiles, t => t.X == 1 && t.Y == 0 && t.SrcX == 10 && t.SrcY == 0);
        Assert.Contains(result.Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 20 && t.SrcY == 0);
        Assert.Contains(result.Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 10 && t.SrcY == 32);
    }

    [Fact]
    public void MirrorHorizontalAndVertical_PreserveGraphicSource()
    {
        var tiles = new[]
        {
            TileAt(0, 0, 2, 8, 16),
            TileAt(2, 1, 2, 24, 48),
        };

        var h = TileSelectionTransform.Apply(tiles, 3, 2, TileSelectionTransformKind.MirrorHorizontal);
        Assert.Equal((3, 2), (h.Width, h.Height));
        Assert.Contains(h.Tiles, t => t.X == 2 && t.Y == 0 && t.SrcX == 8 && t.SrcY == 16);
        Assert.Contains(h.Tiles, t => t.X == 0 && t.Y == 1 && t.SrcX == 24 && t.SrcY == 48);

        var v = TileSelectionTransform.Apply(tiles, 3, 2, TileSelectionTransformKind.MirrorVertical);
        Assert.Contains(v.Tiles, t => t.X == 0 && t.Y == 1 && t.SrcX == 8 && t.SrcY == 16);
        Assert.Contains(v.Tiles, t => t.X == 2 && t.Y == 0 && t.SrcX == 24 && t.SrcY == 48);
    }

    [Fact]
    public void NonSquareRotate_SwapsWidthAndHeight()
    {
        var (w, h) = TileSelectionTransform.TransformSize(3, 1, TileSelectionTransformKind.Rotate90Clockwise);
        Assert.Equal((1, 3), (w, h));
        TileSelectionTransform.MapPoint(2, 0, 3, 1, TileSelectionTransformKind.Rotate90Clockwise, out var x, out var y);
        Assert.Equal((0, 2), (x, y));
    }

    [Fact]
    public void ZoneRectangle_CopyThenPaste_KeepsLayoutAndAttributes()
    {
        var warpTarget = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var source = CreateLayeredMap(8, 6);
        MapEditOperations.PaintTile(source, 0, 1, 1, TileAt(1, 1, 3, 10, 0));
        MapEditOperations.PaintTile(source, 0, 2, 1, TileAt(2, 1, 3, 20, 0));
        MapEditOperations.PaintTile(source, 0, 3, 1, TileAt(3, 1, 3, 30, 0));
        MapEditOperations.PaintTile(source, 0, 1, 2, TileAt(1, 2, 3, 40, 0));
        MapEditOperations.PaintTile(source, 0, 2, 2, new Tile
        {
            X = 2,
            Y = 2,
            Type = TileType.Warp,
            TilesetId = 3,
            SrcX = 8,
            SrcY = 16,
            WarpTargetMapId = warpTarget,
            WarpTargetX = 2,
            WarpTargetY = 9,
            Attributes = { new WarpAttribute { TargetMapId = warpTarget, TargetX = 2, TargetY = 9 } },
        });
        MapEditOperations.SetBlockTile(source, 2, 3, 2);
        MapEditOperations.PaintTile(source, 1, 3, 1, new Tile
        {
            X = 3,
            Y = 1,
            Type = TileType.Resource,
            TilesetId = 2,
            SrcX = 24,
            SrcY = 8,
            Attributes = { new ResourceAttribute { ResourceId = 17 } },
        });

        var clip = new TileClipboardBuffer();
        clip.CopyAllLayers(source, 1, 1, 3, 2);
        Assert.True(clip.HasContent);
        Assert.False(clip.IsSingleLayer);
        Assert.Equal(3, clip.CapturedLayerCount);
        Assert.Equal((3, 2), (clip.Width, clip.Height));

        var dest = CreateLayeredMap(8, 6);
        MapEditOperations.PaintTile(dest, 0, 6, 4, TileAt(6, 4, 1, 99, 0));
        MapEditOperations.PaintTile(dest, 1, 4, 3, TileAt(4, 3, 1, 77, 0));
        MapEditOperations.PaintTile(dest, 0, 0, 0, TileAt(0, 0, 1, 7, 0));

        var pasted = clip.PasteAllLayers(dest, 4, 3, dest.Width, dest.Height);
        Assert.True(pasted.Changed);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 4 && t.Y == 3 && t.SrcX == 10 && t.TilesetId == 3);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 5 && t.Y == 3 && t.SrcX == 20);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 6 && t.Y == 3 && t.SrcX == 30);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 4 && t.Y == 4 && t.SrcX == 40);
        Assert.DoesNotContain(dest.Layers[0].Tiles, t => t.X == 6 && t.Y == 4);
        var warp = Assert.Single(dest.Layers[0].Tiles, t => t.X == 5 && t.Y == 4);
        Assert.Equal(TileType.Warp, warp.Type);
        Assert.Equal(warpTarget, warp.WarpTargetMapId);
        Assert.Equal(2, warp.WarpTargetX);
        Assert.Equal(9, warp.WarpTargetY);
        Assert.Equal(warpTarget, Assert.IsType<WarpAttribute>(Assert.Single(warp.Attributes)).TargetMapId);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 7);

        var resource = Assert.Single(dest.Layers[1].Tiles, t => t.X == 6 && t.Y == 3);
        Assert.Equal(TileType.Resource, resource.Type);
        Assert.Equal(17, Assert.IsType<ResourceAttribute>(Assert.Single(resource.Attributes)).ResourceId);
        Assert.DoesNotContain(dest.Layers[1].Tiles, t => t.X == 4 && t.Y == 3);

        var block = Assert.Single(dest.Layers[2].Tiles, t => t.X == 6 && t.Y == 4);
        Assert.Equal(TileType.Block, block.Type);
        Assert.IsType<BlockAttribute>(Assert.Single(block.Attributes));

        Assert.Contains(source.Layers[0].Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 10);
        Assert.Equal(TileType.Warp, Assert.Single(source.Layers[0].Tiles, t => t.X == 2 && t.Y == 2).Type);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
    }

    [Fact]
    public void Clipboard_CopyRotatePaste_PlacesTransformedTiles()
    {
        var map = CreateMap(8, 8);
        MapEditOperations.PaintTile(map, 0, 1, 1, TileAt(1, 1, 4, 32, 0));
        MapEditOperations.PaintTile(map, 0, 2, 1, TileAt(2, 1, 4, 64, 0));

        var clip = new TileClipboardBuffer();
        clip.CopyFromLayer(map, 0, 1, 1, 2, 1);
        Assert.True(clip.HasContent);
        Assert.Equal(2, clip.Width);
        Assert.Equal(1, clip.Height);
        Assert.True(clip.TryTransform(TileSelectionTransformKind.Rotate90Clockwise));
        Assert.Equal(1, clip.Width);
        Assert.Equal(2, clip.Height);

        var dest = CreateMap(8, 8);
        var pasted = clip.PasteToLayer(dest, 0, 4, 4, dest.Width, dest.Height);
        Assert.Equal(2, pasted.Painted);
        Assert.Equal(0, pasted.Cleared);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 4 && t.Y == 4 && t.SrcX == 32);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 4 && t.Y == 5 && t.SrcX == 64);
    }

    [Fact]
    public void TransformLayerRect_RotatesInPlaceAndClearsOldCells()
    {
        var map = CreateMap(6, 6);
        MapEditOperations.PaintTile(map, 0, 2, 2, TileAt(2, 2, 1, 10, 0));
        MapEditOperations.PaintTile(map, 0, 3, 2, TileAt(3, 2, 1, 20, 0));
        MapEditOperations.PaintTile(map, 0, 4, 2, TileAt(4, 2, 1, 30, 0));

        Assert.True(MapEditOperations.TryTransformLayerRect(
            map, 0, 2, 2, 3, 1, TileSelectionTransformKind.Rotate90Clockwise, out var nw, out var nh));
        Assert.Equal((1, 3), (nw, nh));
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X == 3 && t.Y == 2);
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X == 4 && t.Y == 2);
        Assert.Contains(map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 10);
        Assert.Contains(map.Layers[0].Tiles, t => t.X == 2 && t.Y == 3 && t.SrcX == 20);
        Assert.Contains(map.Layers[0].Tiles, t => t.X == 2 && t.Y == 4 && t.SrcX == 30);
    }

    [Fact]
    public void TransformLayerRect_LockedOrEmpty_ReturnsFalse()
    {
        var map = CreateMap(4, 4);
        map.Layers[0].Locked = true;
        MapEditOperations.PaintTile(map, 0, 0, 0, TileAt(0, 0, 1, 0, 0));
        Assert.False(MapEditOperations.TryTransformLayerRect(
            map, 0, 0, 0, 1, 1, TileSelectionTransformKind.MirrorHorizontal, out _, out _));

        map.Layers[0].Locked = false;
        Assert.False(MapEditOperations.TryTransformLayerRect(
            map, 0, 1, 1, 2, 2, TileSelectionTransformKind.MirrorVertical, out _, out _));
    }

    [Fact]
    public void Pipette_PrefersActiveLayer_ThenTopVisible()
    {
        var map = CreateMap(4, 4);
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe, Visible = true });
        MapEditOperations.PaintTile(map, 0, 1, 1, TileAt(1, 1, 3, 16, 0, TileType.Ground));
        MapEditOperations.PaintTile(map, 1, 1, 1, TileAt(1, 1, 9, 48, 32, TileType.Block));

        Assert.True(MapTilePipette.TrySample(map, 1, 1, preferredLayerIndex: 1, out var fromActive));
        Assert.Equal(9, fromActive.TilesetId);
        Assert.Equal(48, fromActive.SrcX);
        Assert.Equal(32, fromActive.SrcY);
        Assert.Equal(TileType.Block, fromActive.Type);

        map.Layers[1].Tiles.Clear();
        Assert.True(MapTilePipette.TrySample(map, 1, 1, preferredLayerIndex: 1, out var fallback));
        Assert.Equal(3, fallback.TilesetId);
        Assert.Equal(16, fallback.SrcX);

        map.Layers[0].Visible = false;
        Assert.False(MapTilePipette.TrySample(map, 1, 1, preferredLayerIndex: 1, out _));
        Assert.False(MapTilePipette.TrySample(map, 2, 2, 0, out _));
        Assert.False(MapTilePipette.TrySample(map, 99, 0, 0, out _));
    }

    [Fact]
    public void ProtocolAndFmap_Unchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
    }

    [Fact]
    public void Clipboard_CopyAllLayers_PastesEachLayer_ClearsHoles_KeepsAttributes()
    {
        var warpTarget = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var source = CreateLayeredMap(6, 6);
        MapEditOperations.PaintTile(source, 0, 0, 0, TileAt(0, 0, 1, 99, 0));
        MapEditOperations.PaintTile(source, 0, 1, 1, TileAt(1, 1, 3, 10, 0));
        MapEditOperations.PaintTile(source, 0, 1, 2, new Tile
        {
            X = 1,
            Y = 2,
            Type = TileType.Warp,
            TilesetId = 3,
            SrcX = 8,
            SrcY = 16,
            WarpTargetMapId = warpTarget,
            WarpTargetX = 4,
            WarpTargetY = 5,
            Attributes =
            {
                new WarpAttribute { TargetMapId = warpTarget, TargetX = 4, TargetY = 5 },
            },
        });
        MapEditOperations.PaintTile(source, 1, 2, 1, new Tile
        {
            X = 2,
            Y = 1,
            Type = TileType.Resource,
            TilesetId = 2,
            SrcX = 24,
            SrcY = 8,
            Attributes = { new ResourceAttribute { ResourceId = 42 } },
        });
        MapEditOperations.PaintTile(source, 2, 2, 2, new Tile
        {
            X = 2,
            Y = 2,
            Type = TileType.Block,
            Attributes = { new BlockAttribute() },
        });

        var clip = new TileClipboardBuffer();
        clip.CopyAllLayers(source, 1, 1, 2, 2);
        Assert.True(clip.HasContent);
        Assert.False(clip.IsSingleLayer);
        Assert.Equal(3, clip.CapturedLayerCount);
        Assert.True(clip.CapturesLayer(0));
        Assert.True(clip.CapturesLayer(1));
        Assert.True(clip.CapturesLayer(2));
        Assert.Equal(2, clip.Width);
        Assert.Equal(2, clip.Height);

        var empty = new TileClipboardBuffer();
        empty.CopyAllLayers(source, 4, 4, 2, 2);
        Assert.False(empty.HasContent);
        Assert.Equal(0, empty.Width);

        var dest = CreateLayeredMap(8, 8);
        MapEditOperations.PaintTile(dest, 0, 4, 5, TileAt(4, 5, 1, 7, 0));
        MapEditOperations.PaintTile(dest, 0, 5, 5, TileAt(5, 5, 1, 7, 0));
        MapEditOperations.PaintTile(dest, 1, 4, 4, TileAt(4, 4, 1, 7, 0));
        MapEditOperations.PaintTile(dest, 2, 4, 4, TileAt(4, 4, 1, 7, 0));
        MapEditOperations.PaintTile(dest, 0, 0, 0, TileAt(0, 0, 1, 70, 0));

        var pasted = clip.PasteAllLayers(dest, 4, 4, dest.Width, dest.Height);
        Assert.True(pasted.Changed);
        Assert.Equal(4, pasted.Painted);
        Assert.True(pasted.Cleared >= 3);

        var ground = dest.Layers[0];
        Assert.Contains(ground.Tiles, t => t.X == 4 && t.Y == 4 && t.SrcX == 10 && t.TilesetId == 3);
        Assert.DoesNotContain(ground.Tiles, t => t.X == 5 && t.Y == 4);
        var warp = Assert.Single(ground.Tiles, t => t.X == 4 && t.Y == 5);
        Assert.Equal(TileType.Warp, warp.Type);
        Assert.Equal(warpTarget, warp.WarpTargetMapId);
        Assert.Equal(4, warp.WarpTargetX);
        Assert.Equal(5, warp.WarpTargetY);
        var warpAttr = Assert.IsType<WarpAttribute>(Assert.Single(warp.Attributes));
        Assert.Equal(warpTarget, warpAttr.TargetMapId);
        Assert.NotSame(source.Layers[0].Tiles.Single(t => t.X == 1 && t.Y == 2).Attributes[0], warpAttr);
        Assert.DoesNotContain(ground.Tiles, t => t.X == 5 && t.Y == 5);
        Assert.Contains(ground.Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 70);

        var fringe = dest.Layers[1];
        Assert.DoesNotContain(fringe.Tiles, t => t.X == 4 && t.Y == 4);
        var resource = Assert.Single(fringe.Tiles, t => t.X == 5 && t.Y == 4);
        Assert.Equal(TileType.Resource, resource.Type);
        Assert.Equal(42, Assert.IsType<ResourceAttribute>(Assert.Single(resource.Attributes)).ResourceId);

        var attributes = dest.Layers[2];
        Assert.DoesNotContain(attributes.Tiles, t => t.X == 4 && t.Y == 4);
        var block = Assert.Single(attributes.Tiles, t => t.X == 5 && t.Y == 5);
        Assert.Equal(TileType.Block, block.Type);
        Assert.IsType<BlockAttribute>(Assert.Single(block.Attributes));

        Assert.Contains(source.Layers[0].Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 10);
        Assert.Contains(source.Layers[0].Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 99);

        var serializer = new MapSerializer();
        var bytes = serializer.Serialize(dest);
        Assert.Equal((byte)'F', bytes[0]);
        Assert.Equal((byte)5, bytes[4]);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);

        var roundTrip = serializer.Deserialize(bytes);
        var roundWarp = Assert.Single(roundTrip.Layers[0].Tiles, t => t.X == 4 && t.Y == 5);
        Assert.Equal(warpTarget, roundWarp.WarpTargetMapId);
        Assert.Equal(4, roundWarp.WarpTargetX);
        Assert.Equal(TileType.Block, Assert.Single(roundTrip.Layers[2].Tiles, t => t.X == 5 && t.Y == 5).Type);
        Assert.Equal(TileType.Resource, Assert.Single(roundTrip.Layers[1].Tiles, t => t.X == 5 && t.Y == 4).Type);
    }

    [Fact]
    public void Clipboard_SingleLayer_DoesNotTouchOtherLayers_AndPasteAllIsNoop()
    {
        var source = CreateLayeredMap(4, 4);
        MapEditOperations.PaintTile(source, 0, 0, 0, TileAt(0, 0, 1, 11, 0));
        MapEditOperations.PaintTile(source, 1, 0, 0, TileAt(0, 0, 2, 22, 0));

        var clip = new TileClipboardBuffer();
        clip.CopyFromLayer(source, 0, 0, 0, 1, 1);
        Assert.True(clip.IsSingleLayer);
        Assert.Equal(1, clip.CapturedLayerCount);

        var dest = CreateLayeredMap(4, 4);
        MapEditOperations.PaintTile(dest, 1, 2, 2, TileAt(2, 2, 9, 90, 0));
        var pasted = clip.PasteToLayer(dest, 0, 2, 2, dest.Width, dest.Height);
        Assert.Equal(1, pasted.Painted);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 11);
        Assert.Contains(dest.Layers[1].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 90);

        var before = dest.Layers[0].Tiles.Count + dest.Layers[1].Tiles.Count;
        Assert.False(clip.PasteAllLayers(dest, 0, 0, dest.Width, dest.Height).Changed);
        Assert.Equal(before, dest.Layers[0].Tiles.Count + dest.Layers[1].Tiles.Count);
    }

    [Fact]
    public void Clipboard_PasteAllLayers_SkipsLockedDestination_AndClipsToMap()
    {
        var source = CreateLayeredMap(4, 4);
        MapEditOperations.PaintTile(source, 0, 0, 0, TileAt(0, 0, 1, 3, 0));
        MapEditOperations.PaintTile(source, 1, 1, 0, TileAt(1, 0, 1, 4, 0));

        var clip = new TileClipboardBuffer();
        clip.CopyAllLayers(source, 0, 0, 2, 1);

        var dest = CreateLayeredMap(4, 4);
        MapEditOperations.PaintTile(dest, 1, 0, 0, TileAt(0, 0, 8, 80, 0));
        dest.Layers[1].Locked = true;
        Assert.True(clip.CanPasteAllLayers(dest));
        var pasted = clip.PasteAllLayers(dest, 0, 0, dest.Width, dest.Height);
        Assert.Equal(1, pasted.Painted);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 3);
        Assert.DoesNotContain(dest.Layers[0].Tiles, t => t.X == 1 && t.Y == 0);
        Assert.Contains(dest.Layers[1].Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 80);
        Assert.DoesNotContain(dest.Layers[1].Tiles, t => t.SrcX == 4);

        var edge = CreateLayeredMap(3, 3);
        var partial = clip.PasteAllLayers(edge, 2, 0, edge.Width, edge.Height);
        Assert.Equal(1, partial.Painted);
        Assert.Contains(edge.Layers[0].Tiles, t => t.X == 2 && t.Y == 0 && t.SrcX == 3);
        Assert.DoesNotContain(edge.Layers[0].Tiles, t => t.X == 3);
        Assert.Empty(edge.Layers[1].Tiles);
    }

    [Fact]
    public void Clipboard_Rotate_KeepsLayersAligned()
    {
        var source = CreateLayeredMap(4, 4);
        MapEditOperations.PaintTile(source, 0, 0, 0, TileAt(0, 0, 1, 1, 0));
        MapEditOperations.SetBlockTile(source, 2, 1, 0);

        var clip = new TileClipboardBuffer();
        clip.CopyAllLayers(source, 0, 0, 2, 1);
        Assert.True(clip.TryTransform(TileSelectionTransformKind.Rotate90Clockwise));
        Assert.Equal(1, clip.Width);
        Assert.Equal(2, clip.Height);
        Assert.False(clip.IsSingleLayer);

        var dest = CreateLayeredMap(6, 6);
        Assert.True(clip.PasteAllLayers(dest, 3, 3, dest.Width, dest.Height).Changed);
        Assert.Contains(dest.Layers[0].Tiles, t => t.X == 3 && t.Y == 3 && t.SrcX == 1);
        Assert.DoesNotContain(dest.Layers[0].Tiles, t => t.X == 3 && t.Y == 4);
        var block = Assert.Single(dest.Layers[2].Tiles);
        Assert.Equal((3, 4), (block.X, block.Y));
        Assert.Equal(TileType.Block, block.Type);
        Assert.IsType<BlockAttribute>(Assert.Single(block.Attributes));
        Assert.Empty(dest.Layers[1].Tiles);
    }

    [Fact]
    public void TransformMapRect_RotatesEditableLayers_SkipsLocked()
    {
        var map = CreateLayeredMap(4, 4);
        MapEditOperations.PaintTile(map, 0, 0, 0, TileAt(0, 0, 1, 1, 0));
        MapEditOperations.PaintTile(map, 0, 1, 0, TileAt(1, 0, 1, 2, 0));
        MapEditOperations.SetBlockTile(map, 2, 1, 0);
        MapEditOperations.PaintTile(map, 1, 1, 0, TileAt(1, 0, 4, 77, 0));
        map.Layers[1].Locked = true;

        Assert.True(MapEditOperations.TryTransformMapRect(
            map, 0, 0, 2, 1, TileSelectionTransformKind.Rotate90Clockwise, onlyLayerIndex: null, out var width, out var height));
        Assert.Equal((1, 2), (width, height));
        Assert.Contains(map.Layers[0].Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 1);
        Assert.Contains(map.Layers[0].Tiles, t => t.X == 0 && t.Y == 1 && t.SrcX == 2);
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X == 1 && t.Y == 0);
        var block = Assert.Single(map.Layers[2].Tiles);
        Assert.Equal((0, 1), (block.X, block.Y));
        Assert.Equal(TileType.Block, block.Type);
        Assert.Contains(map.Layers[1].Tiles, t => t.X == 1 && t.Y == 0 && t.SrcX == 77);

        Assert.False(MapEditOperations.TryTransformMapRect(
            map, 3, 3, 1, 1, TileSelectionTransformKind.MirrorHorizontal, onlyLayerIndex: null, out _, out _));
    }

    private static Map CreateMap(int width, int height)
    {
        var map = new Map { Name = "Sel", Width = width, Height = height };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground, Visible = true });
        return map;
    }

    private static Map CreateLayeredMap(int width, int height)
    {
        var map = CreateMap(width, height);
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe, DisplayName = "Frange", Visible = true });
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes, DisplayName = "Attributs", Visible = true });
        return map;
    }

    private static Tile TileAt(int x, int y, int tilesetId, int srcX, int srcY, TileType type = TileType.Ground)
        => new()
        {
            X = x,
            Y = y,
            TilesetId = tilesetId,
            SrcX = srcX,
            SrcY = srcY,
            Type = type,
        };
}
