namespace Frog.Core.Audio;

/// <summary>
/// API mute / volume du MVP. Gain linéaire 0–1 ; lecture refusée si muet ou volume 0.
/// Musique derrière <see cref="MusicEnabled"/> (toggle Options).
/// </summary>
public sealed class AudioMixer
{
    public const int DefaultVolumePercent = 80;

    private readonly IAudioPlayback? _playback;

    public AudioMixer()
        : this(null)
    {
    }

    public AudioMixer(IAudioPlayback? playback)
    {
        _playback = playback;
        VolumePercent = DefaultVolumePercent;
    }

    public int VolumePercent { get; private set; }

    /// <summary>Demande explicite de mute (distincte du slider à 0).</summary>
    public bool MuteRequested { get; private set; }

    /// <summary>Opt-in : la boucle musique ne part que si ce flag est vrai.</summary>
    public bool MusicEnabled { get; private set; }

    public bool IsMuted => MuteRequested || VolumePercent <= 0;

    /// <summary>Gain maître linéaire 0–1 (réglage réel, pas un slider factice).</summary>
    public float Gain => IsMuted ? 0f : VolumePercent / 100f;

    public float SfxGain => Gain;

    public float MusicGain => MusicEnabled ? Gain : 0f;

    public void SetVolume(int percent) => VolumePercent = Math.Clamp(percent, 0, 100);

    public void SetMuted(bool muted) => MuteRequested = muted;

    public void SetMusicEnabled(bool enabled) => MusicEnabled = enabled;

    public void Apply(int volumePercent, bool muted, bool musicEnabled)
    {
        SetVolume(volumePercent);
        SetMuted(muted);
        SetMusicEnabled(musicEnabled);
    }

    public float GainFor(AudioCue cue) => cue == AudioCue.MusicLoop ? MusicGain : SfxGain;

    public bool ShouldPlay(AudioCue cue) => GainFor(cue) > 0f;

    /// <summary>
    /// Tente une lecture. Si le cue n’est pas audible (mute / volume 0 / musique off),
    /// stoppe la musique le cas échéant et retourne false.
    /// </summary>
    public bool Play(AudioCue cue)
    {
        var gain = GainFor(cue);
        if (gain <= 0f)
        {
            if (cue == AudioCue.MusicLoop)
            {
                _playback?.Stop(cue);
            }

            return false;
        }

        _playback?.Play(cue, gain);
        return true;
    }

    public void Stop(AudioCue cue) => _playback?.Stop(cue);
}
