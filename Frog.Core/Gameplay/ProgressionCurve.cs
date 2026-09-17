namespace Frog.Core.Gameplay;

/// <summary>
/// Courbe d’expérience déterministe documentée (Phase 7.7).
/// XP pour passer du niveau L à L+1 : 100 * L^2.
/// </summary>
public static class ProgressionCurve
{
    public const int MinLevel = 1;
    public const int MaxLevel = 99;

    public static long ExperienceToNextLevel(int currentLevel)
    {
        if (currentLevel < MinLevel)
        {
            currentLevel = MinLevel;
        }

        if (currentLevel >= MaxLevel)
        {
            return 0;
        }

        return 100L * currentLevel * currentLevel;
    }

    /// <summary>Applique un gain d’XP ; retourne nouveau niveau, XP restant dans le niveau, et niveaux gagnés.</summary>
    public static (int Level, long Experience, int LevelsGained) ApplyExperience(int level, long experience, long grant)
    {
        if (grant < 0)
        {
            grant = 0;
        }

        var levelsGained = 0;
        level = Math.Clamp(level, MinLevel, MaxLevel);
        experience = Math.Max(0, experience) + grant;

        while (level < MaxLevel)
        {
            var need = ExperienceToNextLevel(level);
            if (experience < need)
            {
                break;
            }

            experience -= need;
            level++;
            levelsGained++;
        }

        if (level >= MaxLevel)
        {
            experience = 0;
        }

        return (level, experience, levelsGained);
    }

    /// <summary>Stats dérivées au level-up : +1 à chaque stat (borné 99), +10 HP/MP max.</summary>
    public static void ApplyLevelUpBonuses(
        ref int maxHp,
        ref int maxMp,
        ref int str,
        ref int agi,
        ref int vit,
        ref int intel,
        ref int dex,
        ref int luck,
        int levelsGained)
    {
        for (var i = 0; i < levelsGained; i++)
        {
            maxHp += 10;
            maxMp += 10;
            str = Math.Min(99, str + 1);
            agi = Math.Min(99, agi + 1);
            vit = Math.Min(99, vit + 1);
            intel = Math.Min(99, intel + 1);
            dex = Math.Min(99, dex + 1);
            luck = Math.Min(99, luck + 1);
        }
    }
}
