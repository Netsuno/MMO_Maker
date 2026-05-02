using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Enums;
using Xunit;

namespace Frog.Tests
{
    public class MapSerializerTests
    {
        [Fact]
        public void RoundtripSerializeDeserializePreservesContent()
        {
            var map = new Map { Width = 10, Height = 10, Name = "TestMap" };

            var ground = new Layer { LayerType = LayerType.Ground };
            var tile = new Tile
            {
                X = 1,
                Y = 2,
                TilesetId = 3,
                SrcX = 32,
                SrcY = 64,
                Type = TileType.Ground
            };
            ground.Tiles.Add(tile);
            map.Layers.Add(ground);

            var serializer = new MapSerializer();
            var data = serializer.Serialize(map);
            var back = serializer.Deserialize(data);

            Assert.Equal(10, back.Width);
            Assert.Equal(10, back.Height);
            Assert.Equal("TestMap", back.Name);

            // IMPORTANT: Assert.Single retourne l’élément → on le capture pour éviter IDE0058
            var onlyLayer = Assert.Single(back.Layers);
            Assert.Equal(LayerType.Ground, onlyLayer.LayerType);

            var onlyTile = Assert.Single(onlyLayer.Tiles);
            Assert.Equal(1, onlyTile.X);
            Assert.Equal(2, onlyTile.Y);
            Assert.Equal(3, onlyTile.TilesetId);
            Assert.Equal(32, onlyTile.SrcX);
            Assert.Equal(64, onlyTile.SrcY);
            Assert.Equal(TileType.Ground, onlyTile.Type);
        }
    }
}
