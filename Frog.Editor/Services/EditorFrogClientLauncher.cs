using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Frog.Application.Playtest;
using Frog.Editor.Config;

namespace Frog.Editor.Services;

/// <summary>Lance <c>Frog.Client.exe</c> à côté de l’éditeur (détection build Debug/Release ou chemin mémorisé).</summary>
public static class EditorFrogClientLauncher
{
    private const string ClientExeFileName = "Frog.Client.exe";

    /// <summary>Tente de résoudre l’exécutable client (mémorisé, paquet livré, puis chemins dépôt).</summary>
    public static bool TryResolveExecutable(out string exePath)
        => TryResolveExecutable(AppContext.BaseDirectory, out exePath);

    /// <summary>
    /// P10-3 / P10-6 : même dossier que l’éditeur, layout frère <c>../client-win-x64</c>,
    /// puis <c>bin/Debug|Release</c> du dépôt. Le mémo local n’est lu que pour
    /// <see cref="AppContext.BaseDirectory"/>. Le playtest hotload réutilise ces binaires
    /// (pas de <c>dotnet publish</c>).
    /// </summary>
    public static bool TryResolveExecutable(string searchBaseDirectory, out string exePath)
    {
        exePath = string.Empty;
        if (IsSameDirectory(searchBaseDirectory, AppContext.BaseDirectory)
            && EditorLocalWorkstate.TryReadClientExePath(out var saved))
        {
            exePath = saved;
            return true;
        }

        foreach (var candidate in EnumerateClientCandidates(searchBaseDirectory))
        {
            if (File.Exists(candidate))
            {
                exePath = candidate;
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<string> EnumerateClientCandidates(string searchBaseDirectory)
        => PlaytestPublishLayouts.EnumerateClientCandidates(searchBaseDirectory);

    private static bool IsSameDirectory(string a, string b)
        => string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Ouvre une boîte de dialogue si besoin, mémorise le chemin, puis démarre le processus.</summary>
    public static void Launch(IWin32Window owner)
    {
        if (!TryResolveExecutable(out var path))
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Client Frog|" + ClientExeFileName + "|Exécutable|*.exe",
                Title = "Indiquer Frog.Client.exe (build local ou release)",
                FileName = ClientExeFileName,
            };
            if (ofd.ShowDialog(owner) != DialogResult.OK)
            {
                return;
            }

            path = ofd.FileName;
            if (!File.Exists(path))
            {
                MessageBox.Show(owner, "Fichier introuvable.", "Client Frog", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EditorLocalWorkstate.WriteClientExePath(path);
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
                    UseShellExecute = true,
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                "Impossible de lancer le client : " + ex.Message,
                "Client Frog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
