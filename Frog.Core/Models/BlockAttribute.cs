using Frog.Core.Enums;

using Microsoft.VisualBasic;

namespace Frog.Core.Models
{
    /// <summary>
    /// Attribut de case : blocage (impassable).
    /// Reste minimaliste pour le MVP ; on pourra étendre (sens unique, portes, etc.).
    /// </summary>
    public sealed class BlockAttribute : ITileAttribute
    {
        public TileType Type => TileType.Block;
    }
}
