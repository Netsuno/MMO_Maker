using Frog.Core.Audio;

namespace Frog.Client.Services;

/// <summary>Résout <c>Assets/Audio/*.wav</c> (sortie, repo, playtest).</summary>
internal static class AudioAssetLocator
{
    public static string? Find(AudioCue cue) => Find(AudioAssetNames.FileName(cue));

    public static string? Find(string fileName)
    {
        foreach (var root in Roots())
        {
            var path = Path.Combine(root, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> Roots()
    {
        var parts = AudioAssetNames.RelativeDirectory.Split('/');
        yield return Path.Combine(AppContext.BaseDirectory, Path.Combine(parts));
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, Path.Combine(parts));
            yield return Path.Combine(dir.FullName, "Frog.Client", Path.Combine(parts));
            dir = dir.Parent;
        }
    }
}
