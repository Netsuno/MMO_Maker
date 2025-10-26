// #TODO (FR) : Définir l’instantané joueur pour la synchro Client/Serveur (équipement, stats, position).
#nullable enable
namespace Frog.Core.Models;

using Frog.Core.Enums;

/// <summary>Snapshot minimal d’un joueur pour transit réseau/sérialisation.</summary>
public sealed class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Facing { get; set; }
    // #TODO (FR) : Inventaire, équipement, classe, HP/MP/Level, guilde, flags, etc.
}
