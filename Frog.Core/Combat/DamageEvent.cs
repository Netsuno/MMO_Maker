using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>Dégâts appliqués — trailer additif du paquet 18 existant.</summary>
public readonly record struct DamageEvent(
    Guid AttackerId,
    Guid TargetId,
    CombatTargetKind TargetKind,
    string TargetName,
    int Damage,
    int RemainingHp,
    int MaxHp,
    bool Hit,
    bool Killed)
{
    public byte Flags
    {
        get
        {
            byte flags = 0;
            if (Hit)
            {
                flags |= CombatMvpLimits.DamageFlagHit;
            }

            if (Killed)
            {
                flags |= CombatMvpLimits.DamageFlagKilled;
            }

            return flags;
        }
    }

    public static DamageEvent FromFlags(
        Guid attackerId,
        Guid targetId,
        CombatTargetKind kind,
        string targetName,
        int damage,
        int remainingHp,
        int maxHp,
        byte flags)
        => new(
            attackerId,
            targetId,
            kind,
            targetName,
            damage,
            remainingHp,
            maxHp,
            (flags & CombatMvpLimits.DamageFlagHit) != 0,
            (flags & CombatMvpLimits.DamageFlagKilled) != 0);
}
