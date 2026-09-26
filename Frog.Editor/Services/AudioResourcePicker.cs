using System.IO;
using System.Windows.Forms;
using Frog.Application.Assets;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Dialogs;

namespace Frog.Editor.Services;

/// <summary>
/// Ouvre le navigateur BGM/SE et écrit le chemin relatif dans le champ texte existant.
/// </summary>
internal static class AudioResourcePicker
{
    public static void BrowseInto(
        IWin32Window? owner,
        AudioResourceKind kind,
        string title,
        TextBox target,
        Action<string> reportError)
    {
        if (!TryPick(owner, kind, title, out var stored, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                reportError(error);
            }

            return;
        }

        target.Text = stored;
    }

    private static bool TryPick(
        IWin32Window? owner,
        AudioResourceKind kind,
        string title,
        out string stored,
        out string? error)
    {
        var forced = EditorTestHooks.OverrideMapAudioPickPath;
        if (!string.IsNullOrWhiteSpace(forced))
        {
            if (!MapAudioTrack.TryFromPickedFile(forced, FindRepositoryRoot(), out stored, out error))
            {
                error ??= "Fichier audio refusé.";
                stored = string.Empty;
                return false;
            }

            return true;
        }

        var roots = CurrentSearchRoots();
        var entries = AudioResourceCatalog.List(kind, roots);
        if (EditorTestHooks.OverrideAudioResourceIndex is int index)
        {
            if ((uint)index >= (uint)entries.Count)
            {
                stored = string.Empty;
                error = "Aucun fichier audio.";
                return false;
            }

            stored = entries[index].StoredAsset;
            error = null;
            return true;
        }

        using var dialog = new AudioResourceBrowserDialog(title, kind, entries);
        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.AcceptedAsset))
        {
            stored = string.Empty;
            error = null;
            return false;
        }

        stored = dialog.AcceptedAsset;
        error = null;
        return true;
    }

    private static IReadOnlyList<string> CurrentSearchRoots()
    {
        if (EditorTestHooks.OverrideAudioResourceRoots is { } forced)
        {
            return Normalize(forced)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        string? asset = EditorTestHooks.OverrideProjectAssetRoot;
        if (string.IsNullOrWhiteSpace(asset))
        {
            try
            {
                asset = ProjectAssetRoot.Resolve();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                asset = null;
            }
        }

        return AudioResourceCatalog.DiscoverSearchRoots(asset, FindRepositoryRoot());
    }

    private static IEnumerable<string> Normalize(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
            {
                continue;
            }

            yield return full;
        }
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }
}
