using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>Genre de retour visuel. Les étincelles sont quelques pixels, pas un moteur de particules.</summary>
public enum CombatFxKind : byte
{
    None = 0,
    Hit = 1,
    Kill = 2,
    Miss = 3,
    Crit = 4,
}

/// <summary>Cue client : texte français, flash sprite, taille. Testable sans WinForms.</summary>
public readonly record struct CombatFxCue(CombatFxKind Kind, string Text, bool Flash, float EmSize)
{
    public bool Visible => Kind != CombatFxKind.None && Text.Length > 0;
}

/// <summary>
/// Correspondance hit / miss / crit à partir des champs déjà sur le fil
/// (<see cref="DamageEvent.Hit"/>, <see cref="DamageEvent.Killed"/>, bit <see cref="Frog.Core.Constants.CombatMvpLimits.DamageFlagCrit"/>).
/// </summary>
public static class CombatFx
{
    public const string MissText = "Raté";

    public const float HitEmSize = 9f;

    public const float CritEmSize = 13f;

    /// <summary>Jaune saturé — nombre de dégâts lisible sur une tuile, pas un or d'interface.</summary>
    public const int HitArgb = unchecked((int)0xFFFFFF00);

    /// <summary>Blanc plein pour le coup qui achève.</summary>
    public const int KillArgb = unchecked((int)0xFFFFFFFF);

    /// <summary>Gris pour « Raté ».</summary>
    public const int MissArgb = unchecked((int)0xFFB0B0B0);

    /// <summary>Rouge saturé — critique plus fort que le jaune, sans lueur.</summary>
    public const int CritArgb = unchecked((int)0xFFFF2020);

    public const int OutlineArgb = unchecked((int)0xFF000000);

    /// <summary>Clignotement blanc du sprite (une frame classique), pas un flash d'interface.</summary>
    public const int SpriteFlashMs = 120;

    public const int SpriteSizePx = 32;

    public static CombatFxCue MissCue { get; } = new(CombatFxKind.Miss, MissText, Flash: false, HitEmSize);

    public static CombatFxCue Map(DamageEvent ev)
    {
        if (!ev.Hit)
        {
            return MissCue;
        }

        var text = ev.Damage > 0 ? "-" + ev.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
        if (ev.Crit)
        {
            return new CombatFxCue(CombatFxKind.Crit, text, Flash: true, CritEmSize);
        }

        if (ev.Killed)
        {
            return new CombatFxCue(CombatFxKind.Kill, text, Flash: true, HitEmSize);
        }

        return new CombatFxCue(CombatFxKind.Hit, text, Flash: true, HitEmSize);
    }

    /// <summary>
    /// Coup qui ne porte pas (portée / orientation). Recharge, mort et cible invalide ne sont pas un « Raté ».
    /// </summary>
    public static bool IsSwingMiss(bool hit, string? message)
    {
        if (hit)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        return message.Contains("portee", StringComparison.OrdinalIgnoreCase)
            || message.Contains("portée", StringComparison.OrdinalIgnoreCase)
            || message.Contains("en face", StringComparison.OrdinalIgnoreCase);
    }

    public static int ArgbFor(CombatFxKind kind) => kind switch
    {
        CombatFxKind.Crit => CritArgb,
        CombatFxKind.Kill => KillArgb,
        CombatFxKind.Miss => MissArgb,
        _ => HitArgb,
    };

    /// <summary>Flash sprite seulement sur un coup qui porte, et seulement au tout début.</summary>
    public static bool ShowSpriteFlash(CombatFxKind kind, double ageMs)
        => kind is CombatFxKind.Hit or CombatFxKind.Kill or CombatFxKind.Crit
           && ageMs >= 0
           && ageMs < SpriteFlashMs;

    /// <summary>Rectangle du sprite pieds-ancré, en pixels entiers.</summary>
    public static (int X, int Y, int Size) SpriteFlashRect(float feetX, float feetY)
    {
        var size = SpriteSizePx;
        var x = (int)MathF.Round(feetX - (size / 2f));
        var y = (int)MathF.Round(feetY - size + 1f);
        return (x, y, size);
    }

    /// <summary>Durée des étincelles au contact. Plus court que le nombre flottant.</summary>
    public const int SparkLifetimeMs = 240;

    /// <summary>Le trait à distance devient la croix d'impact après ce délai.</summary>
    public const int SparkTravelMs = 100;

    /// <summary>Blanc plein du centre d'étincelle — pas un or d'interface.</summary>
    public const int SparkWhiteArgb = unchecked((int)0xFFFFFFFF);

    /// <summary>Jaune saturé des bras d'étincelle, identique au nombre de dégâts.</summary>
    public const int SparkYellowArgb = HitArgb;

    /// <summary>Ancre au-dessus des pieds, en pixels entiers. Le nombre monte (Y diminue).</summary>
    public static (int X, int Y) Place(
        float feetX,
        float feetY,
        int risePixels,
        float emSize,
        Direction facing,
        int stackIndex)
    {
        var nudgeX = facing switch
        {
            Direction.Left => -12,
            Direction.Right => 12,
            _ => 0,
        };
        var x = (int)MathF.Round(feetX) + nudgeX + (stackIndex * 10);
        var y = (int)MathF.Round(feetY - (28f + emSize) - risePixels);
        return (x, y);
    }

    /// <summary>Point d'impact devant le sprite. La distance part plus loin que la mêlée.</summary>
    public static (int X, int Y) SparkAnchor(float feetX, float feetY, Direction facing, AttackStyle style)
    {
        var cx = (int)MathF.Round(feetX);
        var cy = (int)MathF.Round(feetY) - 16;
        var reach = style == AttackStyle.Ranged ? 34 : 20;
        return facing switch
        {
            Direction.Left => (cx - reach, cy),
            Direction.Right => (cx + reach, cy),
            Direction.Up => (cx, cy - reach),
            _ => (cx, cy + reach),
        };
    }

    /// <summary>
    /// Pixels d'étincelle absolus. Mêlée : croix jaune/blanc. Distance : trait puis la même croix.
    /// </summary>
    public static int FillSparks(
        float feetX,
        float feetY,
        Direction facing,
        AttackStyle style,
        double ageMs,
        Span<SparkPixel> dest)
    {
        if (dest.Length < 9 || ageMs < 0 || ageMs >= SparkLifetimeMs)
        {
            return 0;
        }

        var (ax, ay) = SparkAnchor(feetX, feetY, facing, style);
        var opened = ageMs >= SparkTravelMs;
        if (style == AttackStyle.Ranged && !opened)
        {
            return FillBolt(ax, ay, facing, dest);
        }

        return FillBurst(ax, ay, opened, dest);
    }

    private static int FillBolt(int ax, int ay, Direction facing, Span<SparkPixel> dest)
    {
        var (dx, dy) = facing switch
        {
            Direction.Left => (1, 0),
            Direction.Right => (-1, 0),
            Direction.Up => (0, 1),
            _ => (0, -1),
        };
        dest[0] = new SparkPixel(ax + (dx * 16), ay + (dy * 16), SparkYellowArgb, 1);
        dest[1] = new SparkPixel(ax + (dx * 8), ay + (dy * 8), SparkYellowArgb, 1);
        dest[2] = new SparkPixel(ax, ay, SparkWhiteArgb, 2);
        return 3;
    }

    private static int FillBurst(int ax, int ay, bool opened, Span<SparkPixel> dest)
    {
        var arm = opened ? 7 : 4;
        dest[0] = new SparkPixel(ax, ay, SparkWhiteArgb, opened ? 1 : 2);
        dest[1] = new SparkPixel(ax - arm, ay, SparkYellowArgb, 1);
        dest[2] = new SparkPixel(ax + arm, ay, SparkYellowArgb, 1);
        dest[3] = new SparkPixel(ax, ay - arm, SparkYellowArgb, 1);
        dest[4] = new SparkPixel(ax, ay + arm, SparkYellowArgb, 1);
        if (!opened)
        {
            return 5;
        }

        dest[5] = new SparkPixel(ax - 4, ay - 4, SparkWhiteArgb, 1);
        dest[6] = new SparkPixel(ax + 4, ay - 4, SparkWhiteArgb, 1);
        dest[7] = new SparkPixel(ax - 4, ay + 4, SparkWhiteArgb, 1);
        dest[8] = new SparkPixel(ax + 4, ay + 4, SparkWhiteArgb, 1);
        return 9;
    }
}

/// <summary>Étincelle 1 ou 2 px, ancrée en pixels écran.</summary>
public readonly record struct SparkPixel(int X, int Y, int Argb, int Size);

/// <summary>Salve d'étincelles liée à un coup qui porte.</summary>
public readonly record struct SparkBurst(AttackStyle Style, Direction Facing, DateTime StartedUtc)
{
    public bool Visible(DateTime utcNow)
    {
        var age = (utcNow - StartedUtc).TotalMilliseconds;
        return age >= 0 && age < CombatFx.SparkLifetimeMs;
    }

    public double AgeMs(DateTime utcNow) => (utcNow - StartedUtc).TotalMilliseconds;
}
