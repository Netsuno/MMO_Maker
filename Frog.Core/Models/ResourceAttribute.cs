using Frog.Core.Enums;

namespace Frog.Core.Models
{
    /// Attribut de case : ressource (ex. arbre, minerai, récolte).
    public sealed class ResourceAttribute : ITileAttribute
    {
        public TileType Type => TileType.Resource;

        /// Identifiant de la ressource (ex: ID dans la table Resource).
        public int ResourceId { get; set; }
    }
}

