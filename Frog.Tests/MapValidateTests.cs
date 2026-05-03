using System;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapValidateTests
{
    [Fact]
    public void Valid_minimal_map_passes()
    {
        var map = new Map { Width = 4, Height = 4, Name = "t" };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers[0].Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Ground, TilesetId = 1, SrcX = 0, SrcY = 0 });
        Assert.True(map.Validate(out var err));
        Assert.Null(err);
    }

    [Fact]
    public void Zero_dimensions_fails()
    {
        var map = new Map { Width = 0, Height = 5, Name = "x" };
        map.Layers.Add(new Layer());
        Assert.False(map.Validate(out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void No_layers_fails()
    {
        var map = new Map { Width = 2, Height = 2, Name = "x" };
        Assert.False(map.Validate(out var err));
        Assert.Contains("couche", err ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tile_out_of_bounds_fails()
    {
        var map = new Map { Width = 2, Height = 2, Name = "x" };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers[0].Tiles.Add(new Tile { X = 2, Y = 0, Type = TileType.Ground, TilesetId = 1 });
        Assert.False(map.Validate(out var err));
        Assert.Contains("hors carte", err ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_tile_same_cell_fails()
    {
        var map = new Map { Width = 3, Height = 3, Name = "x" };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers[0].Tiles.Add(new Tile { X = 1, Y = 1, Type = TileType.Ground, TilesetId = 1 });
        map.Layers[0].Tiles.Add(new Tile { X = 1, Y = 1, Type = TileType.Block, TilesetId = 1 });
        Assert.False(map.Validate(out var err));
        Assert.Contains("superpos", err ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warp_negative_destination_fails()
    {
        var map = new Map { Width = 3, Height = 3, Name = "x" };
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes });
        map.Layers[0].Tiles.Add(new Tile
        {
            X = 0,
            Y = 0,
            Type = TileType.Warp,
            TilesetId = 0,
            WarpTargetMapId = 1,
            WarpTargetX = -1,
            WarpTargetY = 0,
        });
        Assert.False(map.Validate(out var err));
        Assert.Contains("Warp", err ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warp_valid_passes()
    {
        var map = new Map { Width = 3, Height = 3, Name = "x" };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers[0].Tiles.Add(new Tile
        {
            X = 0,
            Y = 0,
            Type = TileType.Warp,
            TilesetId = 1,
            WarpTargetMapId = 0,
            WarpTargetX = 2,
            WarpTargetY = 2,
        });
        Assert.True(map.Validate(out var err));
        Assert.Null(err);
    }
}
