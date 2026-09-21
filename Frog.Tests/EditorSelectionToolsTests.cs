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
        Assert.Equal(2, clip.PasteToLayer(dest, 0, 4, 4, dest.Width, dest.Height));
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

    private static Map CreateMap(int width, int height)
    {
        var map = new Map { Name = "Sel", Width = width, Height = height };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground, Visible = true });
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
