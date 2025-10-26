using Frog.Core.Enums;
using Frog.Core.Interfaces;
using Frog.Core.Models;
using System.Reflection.Emit;
using System.Text;

namespace Frog.Core.IO
{
    public class MapSerializer : ISerializer<Map>
    {
        public string Version => "MAP\x01";

        public Map Read(Stream stream)
        {
            using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var magic = br.ReadBytes(4);
            // TODO: comparer à Version
            var width = br.ReadUInt16();
            var height = br.ReadUInt16();
            var tilesetId = br.ReadUInt16();
            var musicId = br.ReadUInt16();
            br.ReadBytes(8); // reserved

            var map = new Map
            {
                Width = width,
                Height = height,
                TilesetId = tilesetId,
                MusicId = musicId,
                Tiles = new Tile[height, width]
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var t = new Tile
                    {
                        Ground = new Layer { TileNum = br.ReadUInt16(), Flags = br.ReadByte() },
                        Mask = new Layer { TileNum = br.ReadUInt16(), Flags = br.ReadByte() },
                        Fringe = new Layer { TileNum = br.ReadUInt16(), Flags = br.ReadByte() },
                        Animation = new Layer { TileNum = br.ReadUInt16(), Flags = br.ReadByte() },
                        AttributeType = (TileType)br.ReadByte(),
                        Data1 = br.ReadInt16(),
                        Data2 = br.ReadInt16(),
                        Data3 = br.ReadInt16()
                    };
                    map.Tiles[y, x] = t;
                }
            }

            // Optional checksum:
            // var checksum = br.ReadUInt32();

            map.Validate();
            return map;
        }

        public void Write(Stream stream, Map value)
        {
            using var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            // Magic/version (4 bytes)
            bw.Write(new byte[] { (byte)'M', (byte)'A', (byte)'P', 0x01 });
            bw.Write(value.Width);
            bw.Write(value.Height);
            bw.Write(value.TilesetId);
            bw.Write(value.MusicId);
            bw.Write(new byte[8]); // reserved

            for (int y = 0; y < value.Height; y++)
            {
                for (int x = 0; x < value.Width; x++)
                {
                    var t = value.Tiles[y, x];
                    bw.Write(t.Ground.TileNum); bw.Write(t.Ground.Flags);
                    bw.Write(t.Mask.TileNum); bw.Write(t.Mask.Flags);
                    bw.Write(t.Fringe.TileNum); bw.Write(t.Fringe.Flags);
                    bw.Write(t.Animation.TileNum); bw.Write(t.Animation.Flags);
                    bw.Write((byte)t.AttributeType);
                    bw.Write(t.Data1);
                    bw.Write(t.Data2);
                    bw.Write(t.Data3);
                }
            }

            // Optional checksum:
            // bw.Write(CalcChecksum(...));
        }
    }
}
