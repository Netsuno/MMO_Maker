using System;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapPlaytestSpawnTests
{
    [Fact]
    public void BuildWorkstateKey_PrefersMapId()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal("id:aaaaaaaabbbbccccddddeeeeeeeeeeee", MapPlaytestSpawn.BuildWorkstateKey(id, "Town", 20, 15));
    }

    [Fact]
    public void BuildWorkstateKey_FallsBackToNameAndSize()
    {
        Assert.Equal("local:Town|20x15", MapPlaytestSpawn.BuildWorkstateKey(null, " Town ", 20, 15));
        Assert.Equal("local:Town|20x15", MapPlaytestSpawn.BuildWorkstateKey(Guid.Empty, "Town", 20, 15));
    }

    [Fact]
    public void TryClamp_ClampsInsideMap()
    {
        var map = CreateMap(4, 3);
        Assert.True(MapPlaytestSpawn.TryClamp(map, -2, 99, out var x, out var y));
        Assert.Equal(0, x);
        Assert.Equal(2, y);
    }

    [Fact]
    public void TryClamp_RejectsEmptyMap()
    {
        var map = new Map { Width = 0, Height = 5, Name = "empty" };
        Assert.False(MapPlaytestSpawn.TryClamp(map, 0, 0, out _, out _));
    }

    [Fact]
    public void ResolvePreferred_UsesStoredWhenPresent()
    {
        var map = CreateMap(8, 8);
        var resolved = MapPlaytestSpawn.ResolvePreferred(map, storedX: 5, storedY: 6, fallbackX: 1, fallbackY: 1);
        Assert.Equal((5, 6), resolved);
    }

    [Fact]
    public void ResolvePreferred_FallsBackToHoverWhenNoStore()
    {
        var map = CreateMap(8, 8);
        var resolved = MapPlaytestSpawn.ResolvePreferred(map, storedX: null, storedY: null, fallbackX: 3, fallbackY: 4);
        Assert.Equal((3, 4), resolved);
    }

    [Fact]
    public void ResolvePreferred_ClampsStoredOutOfBounds()
    {
        var map = CreateMap(3, 3);
        var resolved = MapPlaytestSpawn.ResolvePreferred(map, storedX: 40, storedY: -1, fallbackX: 1, fallbackY: 1);
        Assert.Equal((2, 0), resolved);
    }

    [Fact]
    public void ClampedOpenTile_StillValidatesForPlaytest()
    {
        var map = CreateMap(4, 4);
        var resolved = MapPlaytestSpawn.ResolvePreferred(map, 0, 0, 2, 2);
        Assert.True(PlaytestSpawnValidator.TryValidate(map, resolved.X, resolved.Y, out var error));
        Assert.Null(error);
    }

    private static Map CreateMap(int w, int h)
    {
        var map = new Map { Name = "spawn-tile", Width = w, Height = h };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                ground.Tiles.Add(new Tile { X = x, Y = y, TilesetId = 1, Type = TileType.Ground });
            }
        }

        map.Layers.Add(ground);
        return map;
    }
}
