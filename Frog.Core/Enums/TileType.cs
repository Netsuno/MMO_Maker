namespace Frog.Core.Enums;

/// <summary>Typage logique d’une tuile (sérialisable sur 1 octet).</summary>
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
    Script = 8,

    // #TODO (FR) : Étendre (Water, Slide, Ladder, Door, Damage…).
}
