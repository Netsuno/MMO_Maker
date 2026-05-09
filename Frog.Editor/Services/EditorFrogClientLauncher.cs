using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Frog.Editor.Config;

namespace Frog.Editor.Services;

/// <summary>Lance <c>Frog.Client.exe</c> à côté de l’éditeur (détection build Debug/Release ou chemin mémorisé).</summary>
public static class EditorFrogClientLauncher
{
    private const string ClientExeFileName = "Frog.Client.exe";

    /// <summary>Tente de résoudre l’exécutable client (mémorisé puis chemins relatifs au dépôt).</summary>
    public static bool TryResolveExecutable(out string exePath)
    {
        exePath = string.Empty;
        if (EditorLocalWorkstate.TryReadClientExePath(out var saved))
        {
            exePath = saved;
            return true;
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var candidate = Path.GetFullPath(
                Path.Combine(baseDir, "..", "..", "..", "..", "Frog.Client", "bin", cfg, "net8.0-windows", ClientExeFileName));
            if (File.Exists(candidate))
            {
                exePath = candidate;
                return true;
            }
        }

        return false;
    }

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
