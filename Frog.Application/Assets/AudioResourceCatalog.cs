using Frog.Core.Models;

namespace Frog.Application.Assets;

/// <summary>Dossier parcouru par le navigateur BGM ou SE de l’éditeur.</summary>
public enum AudioResourceKind
{
    Bgm = 0,
    Se = 1,
}

/// <summary>
/// Fichier audio du projet. <see cref="StoredAsset"/> est le chemin relatif déjà accepté
/// par <see cref="MapAudioTrack"/> (règles #92), jamais un chemin machine absolu.
/// </summary>
public sealed record AudioResourceEntry(string AbsolutePath, string StoredAsset)
{
    public override string ToString() => StoredAsset;
}

/// <summary>
/// Liste les fichiers sous <c>Audio/BGM</c> ou <c>Audio/SE</c>, plus les dossiers plats
/// <c>Assets/Audio</c> déjà utilisés par les propriétés de carte.
/// </summary>
public static class AudioResourceCatalog
{
    private static readonly string[] Extensions = [".wav", ".ogg", ".mp3", ".mid", ".midi", ".flac"];

    public static IReadOnlyList<string> DiscoverSearchRoots(string? assetRoot, string? repositoryRoot)
    {
        var roots = new List<string>();
        Add(roots, repositoryRoot);
        Add(roots, assetRoot);
        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            return roots;
        }

        try
        {
            var full = Path.GetFullPath(assetRoot.Trim());
            var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            if (!string.Equals(name, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return roots;
            }

            var parent = Directory.GetParent(trimmed)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
            {
                return roots;
            }

            var pathRoot = Path.GetPathRoot(parent);
            if (!string.IsNullOrEmpty(pathRoot)
                && string.Equals(parent, pathRoot, StringComparison.OrdinalIgnoreCase))
            {
                return roots;
            }

            Add(roots, parent);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // Racine illisible : on garde les racines déjà acceptées.
        }

        return roots;
    }

    public static IReadOnlyList<AudioResourceEntry> List(AudioResourceKind kind, IEnumerable<string?>? searchRoots)
    {
        var roots = NormalizeRoots(searchRoots);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<AudioResourceEntry>();
        foreach (var root in roots)
        {
            foreach (var (relative, recursive) in Folders(kind))
            {
                var folder = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsUnder(root, folder) || !Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in SafeFiles(folder, recursive))
                {
                    string full;
                    try
                    {
                        full = Path.GetFullPath(file);
                    }
                    catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    if (!IsUnder(folder, full) || !IsAudioFile(full))
                    {
                        continue;
                    }

                    if (!TryStoredAsset(full, roots, out var stored, out _) || stored.Length == 0)
                    {
                        continue;
                    }

                    if (!seen.Add(stored))
                    {
                        continue;
                    }

                    entries.Add(new AudioResourceEntry(full, stored));
                }
            }
        }

        entries.Sort((a, b) => string.Compare(a.StoredAsset, b.StoredAsset, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    /// <summary>
    /// Même canon que <see cref="MapAudioTrack.TryFromPickedFile"/> : marqueur <c>Assets/Audio/</c>,
    /// sinon relatif à une racine du projet, sinon le nom de fichier.
    /// </summary>
    public static bool TryStoredAsset(
        string? pickedPath,
        IEnumerable<string?>? bases,
        out string asset,
        out string? error)
    {
        asset = string.Empty;
        var baseList = new List<string>();
        if (bases is not null)
        {
            foreach (var candidate in bases)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                try
                {
                    var full = Path.GetFullPath(candidate.Trim());
                    if (!baseList.Exists(existing => string.Equals(existing, full, StringComparison.OrdinalIgnoreCase)))
                    {
                        baseList.Add(full);
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
                {
                    // Base ignorée.
                }
            }
        }

        if (baseList.Count == 0)
        {
            baseList.Add(string.Empty);
        }

        string? bare = null;
        string? lastError = null;
        foreach (var baseDir in baseList)
        {
            var directory = baseDir.Length == 0 ? null : baseDir;
            if (!MapAudioTrack.TryFromPickedFile(pickedPath, directory, out var stored, out var storedError))
            {
                lastError = storedError;
                continue;
            }

            if (stored.Contains('/', StringComparison.Ordinal))
            {
                asset = stored;
                error = null;
                return true;
            }

            bare ??= stored;
        }

        if (bare is not null)
        {
            asset = bare;
            error = null;
            return true;
        }

        error = lastError ?? "Aucun fichier audio.";
        return false;
    }

    private static IReadOnlyList<(string Relative, bool Recursive)> Folders(AudioResourceKind kind)
    {
        var specific = kind == AudioResourceKind.Se ? "SE" : "BGM";
        return
        [
            ($"Audio/{specific}", true),
            ($"Assets/Audio/{specific}", true),
            ("Assets/Audio", false),
            ($"Frog.Client/Assets/Audio/{specific}", true),
            ("Frog.Client/Assets/Audio", false),
        ];
    }

    private static List<string> NormalizeRoots(IEnumerable<string?>? searchRoots)
    {
        var roots = new List<string>();
        if (searchRoots is null)
        {
            return roots;
        }

        foreach (var root in searchRoots)
        {
            Add(roots, root);
        }

        return roots;
    }

    private static void Add(List<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim());
            if (!roots.Exists(existing => string.Equals(existing, full, StringComparison.OrdinalIgnoreCase)))
            {
                roots.Add(full);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // Chemin illisible.
        }
    }

    private static IEnumerable<string> SafeFiles(string folder, bool recursive)
    {
        try
        {
            return Directory.EnumerateFiles(
                folder,
                "*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsAudioFile(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Length == 0 || name[0] == '.')
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        foreach (var candidate in Extensions)
        {
            if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnder(string root, string candidate)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }
}
