namespace Frog.Core.Audio;

/// <summary>
/// Sortie optionnelle (WinForms <c>System.Media</c> côté client, enregistreur en tests).
/// Le mixer décide mute / volume ; le backend ne doit pas relire les réglages.
/// </summary>
public interface IAudioPlayback
{
    void Play(AudioCue cue, float gain);

    void Stop(AudioCue cue);
}
