namespace Frog.Core.Constants;

/// <summary>
/// Bornes du MVP effets de statut (poison / étourdissement).
/// Règle de pile : <see cref="StackRule"/> — un seul effet par (carte, cible, kind).
/// Réappliquer le même kind remet la durée et la puissance ; ça n'empile pas les dégâts.
/// <see cref="FrogWireProtocol.Version"/> reste 11. Pas de table PostgreSQL.
/// </summary>
public static class StatusEffectLimits
{
    public const string StackRule = "refresh";

    public const string TickIntervalConfigKey = "Combat:StatusTickMs";

    public const int TickIntervalMs = 1000;

    public const int PoisonTicks = 4;

    public const int PoisonPotency = 3;

    public const int StunTicks = 2;

    /// <summary>op + kind + effectId + sourceId + targetId + ticks ushort + potency ushort.</summary>
    public const int TrailerBytes = 1 + 1 + 16 + 16 + 16 + 2 + 2;
}
