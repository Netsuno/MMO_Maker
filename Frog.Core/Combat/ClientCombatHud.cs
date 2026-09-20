namespace Frog.Core.Combat;

/// <summary>État client combat — testable sans WinForms (floats + flash).</summary>
public sealed class ClientCombatHud
{
    private readonly List<FloatingCombatNumber> _floats = new();

    public DamageEvent? LastEvent { get; private set; }

    public string LastFloatText { get; private set; } = string.Empty;

    public bool FlashPending { get; private set; }

    public IReadOnlyList<FloatingCombatNumber> Floats => _floats;

    public void Apply(DamageEvent ev, DateTime utcNow)
    {
        LastEvent = ev;
        LastFloatText = ev.Hit && ev.Damage > 0 ? "-" + ev.Damage : ev.Hit ? "0" : "rate";
        FlashPending = ev.Hit;
        if (ev.Hit)
        {
            _floats.Add(new FloatingCombatNumber(LastFloatText, utcNow, ev.Killed));
        }

        Prune(utcNow);
    }

    public void Tick(DateTime utcNow) => Prune(utcNow);

    public void ClearFlash() => FlashPending = false;

    public static string FormatFloat(DamageEvent ev)
        => ev.Hit && ev.Damage > 0 ? "-" + ev.Damage : ev.Hit ? "0" : "rate";

    private void Prune(DateTime utcNow)
    {
        _floats.RemoveAll(f => f.IsExpired(utcNow));
    }
}

public readonly record struct FloatingCombatNumber(string Text, DateTime CreatedUtc, bool Killed)
{
    public bool IsExpired(DateTime utcNow)
        => (utcNow - CreatedUtc).TotalMilliseconds >= Frog.Core.Constants.CombatMvpLimits.FloatingNumberLifetimeMs;

    public int RisePixels(DateTime utcNow)
    {
        var age = Math.Clamp((utcNow - CreatedUtc).TotalMilliseconds, 0, Frog.Core.Constants.CombatMvpLimits.FloatingNumberLifetimeMs);
        return (int)(age / 20);
    }
}
