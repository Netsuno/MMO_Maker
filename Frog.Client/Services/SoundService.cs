using Frog.Client.Config;

namespace Frog.Client.Services;

/// <summary>Volume 0–100 persisté. Gain appliqué à toute lecture audio future.</summary>
public sealed class SoundService
{
    public int VolumePercent { get; private set; } = 80;

    /// <summary>Gain linéaire 0–1 dérivé du volume persisté (réglage réel, pas un slider factice).</summary>
    public float Gain => VolumePercent / 100f;

    public bool IsMuted => VolumePercent <= 0;

    public void Apply(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SetVolume(settings.VolumePercent);
        settings.VolumePercent = VolumePercent;
    }

    public void SetVolume(int percent)
    {
        VolumePercent = Math.Clamp(percent, 0, 100);
    }
}
