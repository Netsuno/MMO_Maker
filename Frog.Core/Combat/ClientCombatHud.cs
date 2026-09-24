namespace Frog.Core.Combat;

/// <summary>État client combat — testable sans WinForms (floats + flash).</summary>
public sealed class ClientCombatHud
{
    private readonly List<FloatingCombatNumber> _floats = new();

    public DamageEvent? LastEvent { get; private set; }

    public string LastFloatText { get; private set; } = string.Empty;

    public bool FlashPending { get; private set; }

    public bool FlashCrit { get; private set; }

    public IReadOnlyList<FloatingCombatNumber> Floats => _floats;

    public bool HasFloats => _floats.Count > 0;

    public void Apply(DamageEvent ev, DateTime utcNow)
    {
        LastEvent = ev;
        ApplyCue(CombatFx.Map(ev), utcNow);
    }

    public void ApplyMiss(DateTime utcNow) => ApplyCue(CombatFx.MissCue, utcNow);

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
