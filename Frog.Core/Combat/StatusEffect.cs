namespace Frog.Core.Combat;

/// <summary>
/// Effet de statut MVP. Poison tique des PV ; Étourdi bloque l'attaque du porteur.
/// Règle de pile : refresh (<see cref="Frog.Core.Constants.StatusEffectLimits.StackRule"/>).
/// </summary>
public enum StatusEffectKind : byte
{
    None = 0,
    Poison = 1,
    Stun = 2,
}

/// <summary>Opération portée par le trailer additif du paquet 18.</summary>
public enum StatusEffectOp : byte
{
    None = 0,
    Apply = 1,
    Tick = 2,
    Clear = 3,
}

/// <summary>Effet actif. La carte est un détail serveur, pas un champ du fil.</summary>
public readonly record struct StatusEffect(
    Guid EffectId,
    StatusEffectKind Kind,
    Guid SourceId,
    Guid TargetId,
    int RemainingTicks,
    int Potency);

/// <summary>Trailer additif après <see cref="DamageEvent"/> sur le paquet 18.</summary>
public readonly record struct StatusEffectEvent(
    Guid EffectId,
    StatusEffectKind Kind,
    Guid SourceId,
    Guid TargetId,
    int RemainingTicks,
    int Potency,
    StatusEffectOp Op)
{
    public StatusEffect ToEffect()
        => new(EffectId, Kind, SourceId, TargetId, RemainingTicks, Potency);
}

/// <summary>Icône client (testable sans WinForms).</summary>
public readonly record struct ActiveStatusIcon(
    Guid TargetId,
    StatusEffectKind Kind,
    int RemainingTicks,
    int Potency)
{
    public string Tooltip => StatusEffectText.Tooltip(Kind, RemainingTicks, Potency);
}

/// <summary>Libellés français des effets. Le chrome DA ne change pas.</summary>
public static class StatusEffectText
{
    public static string Tooltip(StatusEffectKind kind, int remainingTicks, int potency)
        => kind switch
        {
            StatusEffectKind.Poison => $"Poison — {remainingTicks} tic(s), {potency} PV",
            StatusEffectKind.Stun => $"Étourdi — {remainingTicks} tic(s)",
            _ => string.Empty,
        };

    public static string Log(StatusEffectEvent ev, DamageEvent? damage)
    {
        var name = string.IsNullOrWhiteSpace(damage?.TargetName) ? "cible" : damage!.Value.TargetName;
        return ev.Op switch
        {
            StatusEffectOp.Apply when ev.Kind == StatusEffectKind.Poison
                => $"Poison → {name} ({ev.RemainingTicks} tics)",
            StatusEffectOp.Apply when ev.Kind == StatusEffectKind.Stun
                => $"Étourdi → {name} ({ev.RemainingTicks} tics)",
            StatusEffectOp.Tick when ev.Kind == StatusEffectKind.Poison && damage is { Damage: > 0 } d
                => $"Poison {d.Damage} → {name} ({d.RemainingHp}/{d.MaxHp})",
            StatusEffectOp.Tick when ev.Kind == StatusEffectKind.Stun
                => $"Étourdi — {ev.RemainingTicks} tic(s)",
            StatusEffectOp.Clear when ev.Kind == StatusEffectKind.Poison && damage is { Killed: false, Damage: > 0 } dmg
                => $"Poison {dmg.Damage} → {name} — dissipé",
            StatusEffectOp.Clear when ev.Kind == StatusEffectKind.Poison
                => "Poison dissipé.",
            StatusEffectOp.Clear when ev.Kind == StatusEffectKind.Stun
                => "Étourdissement dissipé.",
            StatusEffectOp.Clear => "Effets dissipés.",
            _ => string.Empty,
        };
    }

    /// <summary>Messages de pulsation serveur : le journal mêlée ne les double pas.</summary>
    public static bool IsPulseMessage(string? message)
        => message is "Poison."
            or "Étourdissement."
            or "Poison dissipé."
            or "Étourdissement dissipé."
            or "Effets dissipés.";
}
