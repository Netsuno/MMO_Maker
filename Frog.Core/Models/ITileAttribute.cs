using Frog.Core.Enums;

namespace Frog.Core.Models
{
    /// <summary>
    /// Interface de base pour tous les attributs de tiles (Block, Warp, Resource, etc.)
    /// </summary>
    public interface ITileAttribute
    {
        TileType Type { get; }
    }
}

