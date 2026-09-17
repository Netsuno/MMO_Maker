using System;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestSpawnValidatorTests
{
    [Fact]
    public void Valid_Spawn_OnOpenTile_Succeeds()
    {
        var map = CreateMap(4, 4, blockX: 2, blockY: 2);
        Assert.True(PlaytestSpawnValidator.TryValidate(map, 0, 0, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Blocked_Spawn_Fails()
    {
        var map = CreateMap(4, 4, blockX: 1, blockY: 1);
        Assert.False(PlaytestSpawnValidator.TryValidate(map, 1, 1, out var error));
        Assert.Contains("bloquée", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutOfBounds_Spawn_Fails()
    {
        var map = CreateMap(3, 3, blockX: -1, blockY: -1);
        Assert.False(PlaytestSpawnValidator.TryValidate(map, 3, 0, out _));
        Assert.False(PlaytestSpawnValidator.TryValidate(map, 0, 3, out _));
        Assert.False(PlaytestSpawnValidator.TryValidate(map, -1, 0, out _));
    }

    [Fact]
    public void OneByOne_Map_AllowsOnlyOrigin()
    {
        var map = CreateMap(1, 1, blockX: -1, blockY: -1);
        Assert.True(PlaytestSpawnValidator.TryValidate(map, 0, 0, out _));
        Assert.False(PlaytestSpawnValidator.TryValidate(map, 1, 0, out _));
        Assert.False(PlaytestSpawnValidator.TryValidate(map, 0, 1, out _));
    }

    [Fact]
    public void Edge_Map_CornerSpawns_Valid()
    {
        var map = CreateMap(5, 5, blockX: 2, blockY: 2);
        Assert.True(PlaytestSpawnValidator.TryValidate(map, 0, 0, out _));
        Assert.True(PlaytestSpawnValidator.TryValidate(map, 4, 0, out _));
        Assert.True(PlaytestSpawnValidator.TryValidate(map, 0, 4, out _));
        Assert.True(PlaytestSpawnValidator.TryValidate(map, 4, 4, out _));
    }

    private static Map CreateMap(int w, int h, int blockX, int blockY)
    {
        var map = new Map { Name = "spawn", Width = w, Height = h };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var type = x == blockX && y == blockY ? TileType.Block : TileType.Ground;
                ground.Tiles.Add(new Tile { X = x, Y = y, TilesetId = 1, Type = type });
            }
        }

        map.Layers.Add(ground);
        return map;
    }
}
