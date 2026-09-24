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

/// <summary>Cue client : texte, flash hotbar, taille. Testable sans WinForms.</summary>
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

    /// <summary>Ancre au-dessus des pieds du joueur. Le nombre monte (Y diminue) avec l'âge.</summary>
    public static (float X, float Y) Place(
        float feetX,
        float feetY,
        int risePixels,
        float emSize,
        Direction facing,
        int stackIndex)
    {
        var nudgeX = facing switch
        {
            Direction.Left => -12f,
            Direction.Right => 12f,
            _ => 0f,
        };
        var x = feetX + nudgeX + (stackIndex * 10f);
        var y = feetY - (28f + emSize) - risePixels;
        return (x, y);
    }
}
