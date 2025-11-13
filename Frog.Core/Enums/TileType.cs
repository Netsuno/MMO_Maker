// #TODO (FR) : Aligner les valeurs avec les constantes VB6 historiques (walkable, block, warp, damage…).
namespace Frog.Core.Enums;

/// <summary>Typage logique d’une tuile (doit rester sérialisable sur 1 octet si possible).</summary>
public enum TileType : byte
{
    Unknown = 0,
    Ground = 1,
    Block = 2,
    Warp = 3,
    Attribute = 4,
    Door = 5,
    NpcSpawn = 6,
    Resource = 7,

    // #TODO (FR) : Étendre (Water, Slide, Ladder, Door, Damage…).
}
