using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Enums;
using Xunit;

public class MapSerializerTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrip_Works()
    {
        var map = new Map { Width = 10, Height = 10, Name = "TestMap" };
        var layer = new Layer { LayerType = LayerType.Ground };
        layer.Tiles.Add(new Tile { X = 1, Y = 2, TilesetId = 3, SrcX = 32, SrcY = 64, Type = TileType.Ground });
        map.Layers.Add(layer);

        var ser = new MapSerializer();
        var bytes = ser.Serialize(map);
        var back = ser.Deserialize(bytes);

        Assert.Equal(map.Width, back.Width);
        Assert.Equal(map.Height, back.Height);
        Assert.Equal(map.Name, back.Name);
        Assert.Single(back.Layers);
        Assert.Single(back.Layers[0].Tiles);
        Assert.Equal(TileType.Ground, back.Layers[0].Tiles[0].Type);
    }
}
