using Frog.Core.Audio;

namespace Frog.Core.Weather;

/// <summary>
/// Hook mute-friendly sur le mixer audio MVP (#28). Pas de nouveau moteur, pas de WAV météo.
/// </summary>
public static class WeatherAudio
{
    public static bool ShouldPlayAmbience(WeatherOverlayPlan plan, bool muted, float sfxGain) =>
        plan.WantsAmbience && !muted && sfxGain > 0f;

    public static bool ShouldPlayAmbience(WeatherOverlayPlan plan, AudioMixer mixer)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        return ShouldPlayAmbience(plan, mixer.IsMuted, mixer.SfxGain);
    }
}
