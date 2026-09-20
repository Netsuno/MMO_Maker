using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>Cible résolue (joueur, monstre, PNJ ou mannequin d'entraînement).</summary>
public readonly record struct CombatTarget(
    CombatTargetKind Kind,
    Guid TargetId,
    string Name,
    int MapId,
    int PixelX,
    int PixelY,
    int Hp,
    int MaxHp)
{
    public static CombatTarget Dummy(int mapId, int pixelX, int pixelY, int hp, int maxHp = CombatMvpLimits.DummyMaxHp)
        => new(
            CombatTargetKind.Dummy,
            CombatMvpLimits.DummyId,
            CombatMvpLimits.DummyName,
            mapId,
            pixelX,
            pixelY,
            hp,
            maxHp);
}
