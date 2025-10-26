using System.IO;
using FluentAssertions;
using Frog.Core.IO;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests
{
    public class MapSerializerTests
    {
        [Fact]
        public void Roundtrip_Write_Read_Should_Preserve_Content()
        {
            var map = new Map { Width = 2, Height = 1, TilesetId = 1, MusicId = 2, Tiles = new Map.Tile[1,2] };
            map.Tiles[0,0] = new Map.Tile { Data1 = 1, Data2 = 2, Data3 = 3 };
            map.Tiles[0,1] = new Map.Tile { Data1 = 4, Data2 = 5, Data3 = 6 };

            var ser = new MapSerializer();
            using var ms = new MemoryStream();
            ser.Write(ms, map);

            ms.Position = 0;
            var read = ser.Read(ms);

            read.Width.Should().Be(map.Width);
            read.Height.Should().Be(map.Height);
            read.Tiles[0,0].Data2.Should().Be(2);
            read.Tiles[0,1].Data3.Should().Be(6);
        }
    }
}
