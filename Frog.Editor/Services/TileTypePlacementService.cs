using System.Linq;

using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Editor.Services
{
    /// <summary>
    /// Service permettant de modifier le TileType d'une tuile
    /// (Block, Warp, Resource, etc.) à une position donnée.
    /// </summary>
    public sealed class TileTypePlacementService
    {
        /// <summary>
        /// Applique un type de tuile sur la case (tileX, tileY)
        /// du layer spécifié.
        ///
        /// Règle :
        /// - Si la tuile existe déjà :
        ///     → si même type → toggle: Ground
        ///     → sinon → applique le nouveau type
        /// </summary>
        public void Apply(Map map, int layerIndex, int tileX, int tileY, TileType type)
        {
            if (map == null || map.Layers == null)
                return;

            if (layerIndex < 0 || layerIndex >= map.Layers.Count)
                return;

            var layer = map.Layers[layerIndex];
            if (layer.Tiles == null)
                return;

            var tile = layer.Tiles.FirstOrDefault(t => t.X == tileX && t.Y == tileY);
            if (tile == null)
                return;

            // Toggle
            tile.Type = (tile.Type == type)
                ? TileType.Ground
                : type;
        }
    }
}
