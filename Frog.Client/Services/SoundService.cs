using Frog.Client.Config;
using Frog.Core.Audio;

namespace Frog.Client.Services;

/// <summary>
/// Volume 0–100, mute, musique opt-in. Gain appliqué à la lecture (SFX clic + boucle stub).
/// Backend Windows = <see cref="WindowsWavePlayback"/> ; tests = <see cref="RecordingAudioPlayback"/>.
/// </summary>
public sealed class SoundService : IDisposable
{
    private readonly AudioMixer _mixer;
    private readonly IAudioPlayback _playback;
    private bool _disposed;

    public SoundService()
        : this(new WindowsWavePlayback())
    {
    }

    public SoundService(IAudioPlayback playback)
    {
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _mixer = new AudioMixer(_playback);
    }

    public int VolumePercent => _mixer.VolumePercent;

    /// <summary>Gain linéaire 0–1 dérivé du volume persisté (réglage réel, pas un slider factice).</summary>
    public float Gain => _mixer.Gain;

    public bool IsMuted => _mixer.IsMuted;

    public bool MuteRequested => _mixer.MuteRequested;

    public bool MusicEnabled => _mixer.MusicEnabled;

    internal AudioMixer MixerForTest => _mixer;

    public void Apply(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _mixer.Apply(settings.VolumePercent, settings.AudioMuted, settings.MusicEnabled);
        settings.VolumePercent = _mixer.VolumePercent;
        settings.AudioMuted = _mixer.MuteRequested;
        settings.MusicEnabled = _mixer.MusicEnabled;
        SyncMusic();
    }

    public void SetVolume(int percent)
    {
        _mixer.SetVolume(percent);
        SyncMusic();
    }

    public void SetMuted(bool muted)
    {
        _mixer.SetMuted(muted);
        SyncMusic();
    }

    public void SetMusicEnabled(bool enabled)
    {
        _mixer.SetMusicEnabled(enabled);
        SyncMusic();
    }

    /// <summary>Clic UI unique du MVP. No-op si muet / volume 0 / lecture impossible.</summary>
    public bool PlayUiClick() => _mixer.Play(AudioCue.UiClick);

    public bool SyncMusic() => _mixer.Play(AudioCue.MusicLoop);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _mixer.Stop(AudioCue.MusicLoop);
        }
        catch
        {
            // ignore
        }

        (_playback as IDisposable)?.Dispose();
    }
}
