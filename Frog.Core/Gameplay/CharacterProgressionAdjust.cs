using Frog.Core.Character;

namespace Frog.Core.Gameplay;

/// <summary>
/// Niveau, EXP et paramètres d'un personnage. Les commandes d'événement
/// n'appliquent pas la courbe de combat : chaque champ bouge seul.
/// L'EXP reste dans la barre du niveau courant.
/// </summary>
public readonly record struct CharacterVitals
{
    public int Level { get; init; }

    public long Experience { get; init; }

    public int Hp { get; init; }

    public int MaxHp { get; init; }

    public int Mp { get; init; }

    public int MaxMp { get; init; }

    public int Str { get; init; }

    public int Agi { get; init; }

    public int Vit { get; init; }

    public int Int { get; init; }

    public int Dex { get; init; }

    public int Luck { get; init; }

    public static CharacterVitals Default { get; } = new()
    {
        Level = ProgressionCurve.MinLevel,
        Hp = 1,
        MaxHp = 1,
        Str = CharacterStatsWire.MinStat,
        Agi = CharacterStatsWire.MinStat,
        Vit = CharacterStatsWire.MinStat,
        Int = CharacterStatsWire.MinStat,
        Dex = CharacterStatsWire.MinStat,
        Luck = CharacterStatsWire.MinStat,
    };
}

/// <summary>Bornes et application des deltas niveau / EXP / paramètres.</summary>
public static class CharacterProgressionAdjust
{
    public const int MaxLevelDelta = ProgressionCurve.MaxLevel - ProgressionCurve.MinLevel;

    public const int MaxExpDelta = 1_000_000;

    public const int MaxParamDelta = 9999;

    public const int MaxVital = 9999;

    public const string StatStr = "STR";
    public const string StatAgi = "AGI";
    public const string StatDex = "DEX";
    public const string StatInt = "INT";
    public const string StatVit = "VIT";
    public const string StatLuck = "LUCK";
    public const string StatHp = "HP";
    public const string StatMaxHp = "MAXHP";
    public const string StatMp = "MP";
    public const string StatMaxMp = "MAXMP";

    public static readonly IReadOnlyList<string> ParamKeys =
    [
        StatStr, StatAgi, StatDex, StatInt, StatVit, StatLuck, StatHp, StatMaxHp, StatMp, StatMaxMp,
    ];

    private static readonly HashSet<string> ParamKeySet = new(ParamKeys, StringComparer.Ordinal);

    public static bool IsParam(string? stat) =>
        !string.IsNullOrEmpty(stat) && ParamKeySet.Contains(stat);

    public static bool IsPrimaryStat(string? stat) => stat is StatStr or StatAgi or StatDex or StatInt or StatVit or StatLuck;

    public static CharacterVitals AdjustLevel(CharacterVitals current, int delta)
    {
        var level = AddClamp(current.Level, delta, ProgressionCurve.MinLevel, ProgressionCurve.MaxLevel);
        return current with
        {
            Level = level,
            Experience = ClampExperience(level, current.Experience),
        };
    }

    public static CharacterVitals AdjustExperience(CharacterVitals current, int delta)
    {
        var sum = current.Experience + delta;
        if (current.Experience > 0 && delta > 0 && sum < current.Experience)
        {
            sum = long.MaxValue;
        }
        else if (current.Experience < 0 && delta < 0 && sum > current.Experience)
        {
            sum = 0;
        }

        return current with { Experience = ClampExperience(current.Level, sum) };
    }

    public static CharacterVitals AdjustParam(CharacterVitals current, string stat, int delta) =>
        stat switch
        {
            StatStr => current with { Str = AddClamp(current.Str, delta, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat) },
            StatAgi => current with { Agi = AddClamp(current.Agi, delta, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat) },
            StatDex => current with { Dex = AddClamp(current.Dex, delta, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat) },
            StatInt => current with { Int = AddClamp(current.Int, delta, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat) },
            StatVit => current with { Vit = AddClamp(current.Vit, delta, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat) },
            StatLuck => current with { Luck = AddClamp(current.Luck, delta, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat) },
            StatHp => current with { Hp = AddClamp(current.Hp, delta, 0, Math.Max(0, current.MaxHp)) },
            StatMaxHp => AdjustMaxHp(current, delta),
            StatMp => current with { Mp = AddClamp(current.Mp, delta, 0, Math.Max(0, current.MaxMp)) },
            StatMaxMp => AdjustMaxMp(current, delta),
            _ => current,
        };

    public static bool VitalsDiffer(CharacterVitals before, CharacterVitals after) =>
        before.Level != after.Level
        || before.Experience != after.Experience
        || before.Hp != after.Hp
        || before.MaxHp != after.MaxHp
        || before.Mp != after.Mp
        || before.MaxMp != after.MaxMp;

    public static bool StatsDiffer(CharacterVitals before, CharacterVitals after) =>
        before.Str != after.Str
        || before.Agi != after.Agi
        || before.Dex != after.Dex
        || before.Int != after.Int
        || before.Vit != after.Vit
        || before.Luck != after.Luck;

    public static string FormatPrimaryStats(int str, int agi, int dex, int intel, int vit, int luck) =>
        $"STR {str} · AGI {agi} · DEX {dex} · INT {intel} · VIT {vit} · LUCK {luck}";

    public static string StatsPayloadJson(int str, int agi, int dex, int intel, int vit, int luck)
    {
        str = AddClamp(str, 0, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat);
        agi = AddClamp(agi, 0, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat);
        dex = AddClamp(dex, 0, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat);
        intel = AddClamp(intel, 0, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat);
        vit = AddClamp(vit, 0, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat);
        luck = AddClamp(luck, 0, CharacterStatsWire.MinStat, CharacterStatsWire.MaxStat);
        return "{\"stats\":{\"STR\":" + str
            + ",\"AGI\":" + agi
            + ",\"DEX\":" + dex
            + ",\"INT\":" + intel
            + ",\"VIT\":" + vit
            + ",\"LUCK\":" + luck + "}}";
    }

    public static string ParamLabel(string? stat) => stat switch
    {
        StatStr => "STR",
        StatAgi => "AGI",
        StatDex => "DEX",
        StatInt => "INT",
        StatVit => "VIT",
        StatLuck => "LUCK",
        StatHp => "HP",
        StatMaxHp => "HP max",
        StatMp => "MP",
        StatMaxMp => "MP max",
        _ => string.IsNullOrWhiteSpace(stat) ? "Paramètre" : stat.Trim(),
    };

    private static CharacterVitals AdjustMaxHp(CharacterVitals current, int delta)
    {
        var maxHp = AddClamp(current.MaxHp, delta, 1, MaxVital);
        var hp = Math.Min(Math.Max(0, current.Hp), maxHp);
        return current with { MaxHp = maxHp, Hp = hp };
    }

    private static CharacterVitals AdjustMaxMp(CharacterVitals current, int delta)
    {
        var maxMp = AddClamp(current.MaxMp, delta, 0, MaxVital);
        var mp = Math.Min(Math.Max(0, current.Mp), maxMp);
        return current with { MaxMp = maxMp, Mp = mp };
    }

    private static long ClampExperience(int level, long experience)
    {
        if (level >= ProgressionCurve.MaxLevel)
        {
            return 0;
        }

        var cap = ProgressionCurve.ExperienceToNextLevel(level);
        if (cap <= 0)
        {
            return 0;
        }

        if (experience < 0)
        {
            return 0;
        }

        return experience > cap ? cap : experience;
    }

    private static int AddClamp(int value, int delta, int min, int max)
    {
        var sum = (long)value + delta;
        if (sum < min)
        {
            return min;
        }

        if (sum > max)
        {
            return max;
        }

        return (int)sum;
    }
}
