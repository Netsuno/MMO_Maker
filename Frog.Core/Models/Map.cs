using System.Runtime.InteropServices;
using Frog.Core.Interfaces;
using Frog.Core.Enums;

namespace Frog.Core.Models
{
    public class Map : IValidatable
    {
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort TilesetId { get; set; }
        public ushort MusicId { get; set; }
        public Tile[,] Tiles { get; set; } = new Tile[0,0];

        public void Validate()
        {
            if (Width == 0 || Height == 0) throw new InvalidDataException("Map size must be > 0.");
            if (Tiles.GetLength(0) != Height || Tiles.GetLength(1) != Width)
                throw new InvalidDataException("Tiles array size mismatch.");
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tile
    {
        public Layer Ground;
        public Layer Mask;
        public Layer Fringe;
        public Layer Animation;
        public TileType AttributeType;
        public short Data1;
        public short Data2;
        public short Data3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Layer
    {
        public ushort TileNum;
        public byte Flags; // bitfield; TODO: définir
    }
}
