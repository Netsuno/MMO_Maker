namespace Frog.Core.Audio;

/// <summary>Backend de test : enregistre Play/Stop, ne sort aucun son.</summary>
public sealed class RecordingAudioPlayback : IAudioPlayback
{
    public List<(AudioCue Cue, float Gain)> Plays { get; } = [];

    public List<AudioCue> Stops { get; } = [];

    public void Play(AudioCue cue, float gain) => Plays.Add((cue, gain));

    public void Stop(AudioCue cue) => Stops.Add(cue);
}
