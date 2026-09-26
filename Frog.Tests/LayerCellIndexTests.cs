using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class LayerCellIndexTests
{
    [Fact]
    public void PaintTile_ReplacesOneCellOnALargeLayer()
    {
        var map = new Map { Name = "Grande", Width = 80, Height = 60 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var layer = map.Layers[0];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                layer.Tiles.Add(new Tile { X = x, Y = y, SrcX = 1, TilesetId = 1, Type = TileType.Ground });
            }
        }

        MapEditOperations.PaintTile(map, 0, 10, 12, new Tile { SrcX = 9, TilesetId = 1, Type = TileType.Ground });

        Assert.Equal(map.Width * map.Height, layer.Tiles.Count);
        Assert.Equal(9, layer.TileAt(10, 12)!.SrcX);
        Assert.Equal(1, layer.TileAt(0, 0)!.SrcX);
        Assert.Equal(1, layer.TileAt(79, 59)!.SrcX);
        Assert.Single(layer.Tiles, tile => tile.X == 10 && tile.Y == 12);
    }

    [Fact]
    public void RemoveTileAt_KeepsTheOtherCells()
    {
        var layer = new Layer();
        layer.ReplaceTileAt(0, 0, new Tile { SrcX = 1 });
        layer.ReplaceTileAt(1, 0, new Tile { SrcX = 2 });
        layer.ReplaceTileAt(2, 0, new Tile { SrcX = 3 });

        Assert.True(layer.RemoveTileAt(0, 0));
        Assert.False(layer.RemoveTileAt(0, 0));

        Assert.Null(layer.TileAt(0, 0));
        Assert.Equal(2, layer.TileAt(1, 0)!.SrcX);
        Assert.Equal(3, layer.TileAt(2, 0)!.SrcX);
        Assert.Equal(2, layer.Tiles.Count);
    }

    [Fact]
    public void TileAt_NoticesADirectListAdd()
    {
        var layer = new Layer();
        layer.Tiles.Add(new Tile { X = 0, Y = 0, SrcX = 1 });
        Assert.Equal(1, layer.TileAt(0, 0)!.SrcX);

        layer.Tiles.Add(new Tile { X = 2, Y = 3, SrcX = 4 });

        Assert.Equal(4, layer.TileAt(2, 3)!.SrcX);
        Assert.Equal(1, layer.TileAt(0, 0)!.SrcX);
    }

    [Fact]
    public void TileAt_NoticesClearAndAddOfTheSameCount()
    {
        var layer = new Layer();
        layer.Tiles.Add(new Tile { X = 0, Y = 0, SrcX = 1 });
        Assert.NotNull(layer.TileAt(0, 0));

        layer.Tiles.Clear();
        layer.Tiles.Add(new Tile { X = 2, Y = 2, SrcX = 8 });

        Assert.Null(layer.TileAt(0, 0));
        Assert.Equal(8, layer.TileAt(2, 2)!.SrcX);
    }

    [Fact]
    public void TileAt_RebuildsWhenTheStoredCoordinateNoLongerMatches()
    {
        var layer = new Layer();
        var tile = new Tile { X = 1, Y = 1, SrcX = 3 };
        layer.Tiles.Add(tile);
        Assert.Equal(3, layer.TileAt(1, 1)!.SrcX);

        tile.X = 4;
        tile.Y = 5;

        Assert.Null(layer.TileAt(1, 1));
        Assert.Equal(3, layer.TileAt(4, 5)!.SrcX);
    }
}
