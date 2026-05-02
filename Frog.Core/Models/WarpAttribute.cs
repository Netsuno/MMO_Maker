using Frog.Core.Enums;

namespace Frog.Core.Models
{

    /// Attribut de case : téléportation (warp) vers une autre map / position.

    public sealed class WarpAttribute : ITileAttribute
    {
        public TileType Type => TileType.Warp;


        /// ID de la map cible.
 
        public int TargetMapId { get; set; }


        /// Position X sur la map cible (en coordonnées de tile).

        public int TargetX { get; set; }


        /// Position Y sur la map cible (en coordonnées de tile).

        public int TargetY { get; set; }
    }
}
