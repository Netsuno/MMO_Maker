namespace Frog.Core.Weather;

/// <summary>
/// Plan d'overlay CPU-cheap : une teinte ARGB + éventuellement quelques traits (pas un moteur VFX).
/// </summary>
public readonly record struct WeatherOverlayPlan(
    string KindId,
    string DisplayName,
    int TintArgb,
    int ParticleCount,
    bool WantsAmbience)
{
    public byte TintAlpha => (byte)((uint)TintArgb >> 24);

    public bool NeedsDraw => TintAlpha > 0 || ParticleCount > 0;
}
