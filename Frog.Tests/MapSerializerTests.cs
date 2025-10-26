using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Enums;
using Xunit;

namespace Frog.Tests;

public class MapSerializerTests
{
    [Fact]
    public void RoundtripSerializeDeserializePreservesContent()
    {
        var map = new Map { Width = 10, Height = 10, Name = "TestMap" };

        var ground = new Layer { LayerType = LayerType.Ground };
        ground.Tiles.Add(new Tile
        {
            X = 1,
            Y = 2,
            TilesetId = 3,
            SrcX = 32,
            SrcY = 64,
            Type = TileType.Ground
        });
        map.Layers.Add(ground);

        var serializer = new MapSerializer();
        var data = serializer.Serialize(map);
        var back = serializer.Deserialize(data);

        Assert.Equal(10, back.Width);
        Assert.Equal(10, back.Height);
        Assert.Equal("TestMap", back.Name);
        Assert.Single(back.Layers);
        Assert.Equal(LayerType.Ground, back.Layers[0].LayerType);

        Assert.Single(back.Layers[0].Tiles);
        var t = back.Layers[0].Tiles[0];
        Assert.Equal(1, t.X);
        Assert.Equal(2, t.Y);
        Assert.Equal(3, t.TilesetId);
        Assert.Equal(32, t.SrcX);
        Assert.Equal(64, t.SrcY);
        Assert.Equal(TileType.Ground, t.Type);
    }
}

