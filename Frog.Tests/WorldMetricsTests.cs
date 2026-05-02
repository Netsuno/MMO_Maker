using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Server.Network;
using Frog.Server.Services;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Frog.Tests;

public sealed class WorldMetricsTests
{
    [Fact]
    public void MeleeCombat_AdjacentTileCenters_AreInRange()
    {
        var (ax, ay) = WorldMetrics.TileCenterToPixels(0, 0);
        var (bx, by) = WorldMetrics.TileCenterToPixels(1, 0);
        Assert.True(MeleeCombat.IsWithinMeleeRange(ax, ay, bx, by));
    }

    [Fact]
    public void MeleeCombat_TwoTilesApart_IsOutOfRange()
    {
        var (ax, ay) = WorldMetrics.TileCenterToPixels(0, 0);
        var (bx, by) = WorldMetrics.TileCenterToPixels(2, 0);
        Assert.False(MeleeCombat.IsWithinMeleeRange(ax, ay, bx, by));
    }

    [Fact]
    public void MapService_LoadsWorld_FromSerializedFmapFile()
    {
        var map = new Map { Width = 8, Height = 8, Name = "Unit" };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    SrcX = 0,
                    SrcY = 0,
                    Type = TileType.Ground
                });
            }
        }

        map.Layers.Add(ground);
        var bytes = new MapSerializer().Serialize(map);
        var tmp = Path.Combine(Path.GetTempPath(), $"frog-map-{Guid.NewGuid():N}.fmap");
        File.WriteAllBytes(tmp, bytes);
        try
        {
            var svc = MapTestHelpers.CreateMapService(tmp);
            Assert.Equal(8, svc.GetDefaultMapBounds().Width);
            Assert.Equal(8, svc.GetDefaultMapBounds().Height);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void PacketDispatcher_ParsesMeleeTargetPayload()
    {
        var target = "victim";
        var t = Encoding.UTF8.GetBytes(target);
        var payload = new byte[1 + t.Length];
        payload[0] = (byte)t.Length;
        t.CopyTo(payload, 1);

        var ok = PacketDispatcher.TryParseMeleeTargetPayload(payload, out var name);
        Assert.True(ok);
        Assert.Equal(target, name);
    }
}
