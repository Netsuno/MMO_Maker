namespace Frog.Core.Combat;

/// <summary>
/// Style d'attaque sur le paquet 17. L'octet est optionnel : absent = mêlée.
/// <see cref="Frog.Core.Constants.FrogWireProtocol.Version"/> reste 11.
/// </summary>
public enum AttackStyle : byte
{
    Melee = 0,
    Ranged = 1,
}
