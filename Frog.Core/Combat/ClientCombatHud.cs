using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>État client combat — testable sans WinForms (floats + flash).</summary>
public sealed class ClientCombatHud
{
    private readonly List<FloatingCombatNumber> _floats = new();
    private readonly List<ActiveStatusIcon> _statuses = new();

    public DamageEvent? LastEvent { get; private set; }

    public StatusEffectEvent? LastStatus { get; private set; }

    public string LastFloatText { get; private set; } = string.Empty;

    public bool FlashPending { get; private set; }

    public bool FlashCrit { get; private set; }

    public IReadOnlyList<FloatingCombatNumber> Floats => _floats;

    public IReadOnlyList<ActiveStatusIcon> Statuses => _statuses;

    public bool HasFloats => _floats.Count > 0;

    public bool HasStatuses => _statuses.Count > 0;

    public string StatusTooltip
        => string.Join(" · ", _statuses.Select(icon => icon.Tooltip));

    public SparkBurst? Sparks { get; private set; }

    public bool SparksVisible(DateTime utcNow) => Sparks is { } burst && burst.Visible(utcNow);

    public void Apply(DamageEvent ev, DateTime utcNow, Direction facing = Direction.Down)
    {
        LastEvent = ev;
        if (ev.Hit)
        {
            Sparks = new SparkBurst(
                ev.Ranged ? AttackStyle.Ranged : AttackStyle.Melee,
                facing,
                utcNow);
        }

        ApplyCue(CombatFx.Map(ev), utcNow);
    }

    public void ApplyMiss(DateTime utcNow) => ApplyCue(CombatFx.MissCue, utcNow);

    /// <summary>
    /// Apply / tic / clear. Le coup d'arme (Apply) ne rajoute pas un second nombre :
    /// le <see cref="DamageEvent"/> s'en charge. Un tic de poison ajoute le nombre vert.
    /// </summary>
    public void ApplyStatus(StatusEffectEvent ev, DateTime utcNow, DamageEvent? damage)
    {
        LastStatus = ev;
        if (ev.Op == StatusEffectOp.Clear)
        {
            if (ev.Kind == StatusEffectKind.None)
            {
                _statuses.RemoveAll(icon => icon.TargetId == ev.TargetId);
            }
            else
            {
                _statuses.RemoveAll(icon => icon.TargetId == ev.TargetId && icon.Kind == ev.Kind);
            }

            if (damage is { Killed: false, Damage: > 0 } && ev.Kind == StatusEffectKind.Poison)
            {
                ApplyCue(CombatFx.PoisonTick(damage.Value.Damage), utcNow);
            }

            return;
        }

        if (ev.Kind is not (StatusEffectKind.Poison or StatusEffectKind.Stun))
        {
            return;
        }

        Upsert(ev);
        if (ev.Op == StatusEffectOp.Tick
            && damage is { Killed: false, Damage: > 0 }
            && ev.Kind == StatusEffectKind.Poison)
        {
            ApplyCue(CombatFx.PoisonTick(damage.Value.Damage), utcNow);
        }
    }

    public void Tick(DateTime utcNow) => Prune(utcNow);

    public void ClearFlash()
    {
        FlashPending = false;
        FlashCrit = false;
    }

    public static string FormatFloat(DamageEvent ev) => CombatFx.Map(ev).Text;

    private void ApplyCue(CombatFxCue cue, DateTime utcNow)
    {
        if (!cue.Visible)
        {
            return;
        }

        if (_floats.Count > 0)
        {
            var last = _floats[^1];
            if (last.Kind == cue.Kind
                && last.Text == cue.Text
                && (utcNow - last.CreatedUtc).TotalMilliseconds < 40)
            {
                LastFloatText = cue.Text;
                return;
            }
        }

        LastFloatText = cue.Text;
        FlashPending = cue.Flash;
        FlashCrit = cue.Kind == CombatFxKind.Crit && cue.Flash;
        while (_floats.Count >= 4)
        {
            _floats.RemoveAt(0);
        }

        _floats.Add(new FloatingCombatNumber(cue.Text, utcNow, cue.Kind, cue.EmSize));
        Prune(utcNow);
    }

    private void Upsert(StatusEffectEvent ev)
    {
        var icon = new ActiveStatusIcon(ev.TargetId, ev.Kind, ev.RemainingTicks, ev.Potency);
        for (var i = 0; i < _statuses.Count; i++)
        {
            if (_statuses[i].TargetId == ev.TargetId && _statuses[i].Kind == ev.Kind)
            {
                _statuses[i] = icon;
                return;
            }
        }

        while (_statuses.Count >= 4)
        {
            _statuses.RemoveAt(0);
        }

        _statuses.Add(icon);
    }

    private void Prune(DateTime utcNow)
    {
        _floats.RemoveAll(f => f.IsExpired(utcNow));
    }
}

public readonly record struct FloatingCombatNumber(string Text, DateTime CreatedUtc, CombatFxKind Kind, float EmSize)
{
    public bool IsExpired(DateTime utcNow)
        => (utcNow - CreatedUtc).TotalMilliseconds >= Frog.Core.Constants.CombatMvpLimits.FloatingNumberLifetimeMs;

    public int RisePixels(DateTime utcNow)
    {
        var age = Math.Clamp((utcNow - CreatedUtc).TotalMilliseconds, 0, Frog.Core.Constants.CombatMvpLimits.FloatingNumberLifetimeMs);
        return (int)(age / 20);
    }
}
