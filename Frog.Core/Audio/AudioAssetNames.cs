namespace Frog.Core.Audio;

/// <summary>Noms des placeholders générés (CC0, dans le dépôt).</summary>
public static class AudioAssetNames
{
    public const string RelativeDirectory = "Assets/Audio";

    public const string UiClickFile = "ui-click.wav";

    public const string MusicLoopFile = "music-loop.wav";

    public static string FileName(AudioCue cue) => cue switch
    {
        AudioCue.UiClick => UiClickFile,
        AudioCue.MusicLoop => MusicLoopFile,
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Cue audio inconnu."),
    };
}
