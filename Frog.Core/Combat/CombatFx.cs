using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>Genre de retour visuel mêlée. Pas de particules.</summary>
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
}
