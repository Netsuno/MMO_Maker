namespace Frog.Core.Gameplay;

/// <summary>
/// Formules de combat documentées et déterministes (Phase 7.4).
/// Pas de RNG — polish d’équilibrage hors scope.
/// </summary>
public static class CombatFormulas
{
    public const int BasicAttackCooldownMs = 800;
    public const int BasicAttackRangePixels = 56;
    public const int DefaultSpellRangePixels = 160;

    /// <summary>Dégâts mêlée : max(1, STR + floor(weaponPower/2) - floor(targetVit/4)).</summary>
    public static int MeleeDamage(int attackerStr, int weaponPower, int targetVit)
        => Math.Max(1, attackerStr + (weaponPower / 2) - (targetVit / 4));

    /// <summary>Dégâts sort : max(1, INT + spellPower - floor(targetVit/5)).</summary>
    public static int SpellDamage(int attackerInt, int spellPower, int targetVit)
        => Math.Max(1, attackerInt + spellPower - (targetVit / 5));

    /// <summary>PV monstre : 20 + level * 15.</summary>
    public static int MonsterMaxHp(int level)
        => Math.Max(1, 20 + Math.Max(1, level) * 15);

    /// <summary>XP monstre : 10 + level * 5.</summary>
    public static long MonsterExperienceReward(int level)
        => 10L + Math.Max(1, level) * 5L;

    /// <summary>Puissance sort dérivée du coût mana (contenu minimal).</summary>
    public static int SpellPowerFromManaCost(int manaCost)
        => Math.Max(1, manaCost);
}
