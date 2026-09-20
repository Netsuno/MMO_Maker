namespace Frog.Core.Enums;

/// <summary>Cible d'une attaque mêlée MVP (paquet 17 existant).</summary>
public enum CombatTargetKind : byte
{
    None = 0,
    Player = 1,
    Monster = 2,
    Dummy = 3,
    Npc = 4
}
