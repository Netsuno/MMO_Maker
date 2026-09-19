using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10PackagingTests
{
    [Fact]
    public void PublishScripts_AreSelfContained_WithArchiveSha256()
    {
        var root = RepoRoot();
        var sh = File.ReadAllText(Path.Combine(root, "scripts", "publish-frog.sh"));
        var ps1 = File.ReadAllText(Path.Combine(root, "scripts", "publish-frog.ps1"));
        Assert.Contains("--self-contained true", sh, StringComparison.Ordinal);
        Assert.DoesNotContain("--self-contained false", sh, StringComparison.Ordinal);
        Assert.Contains("archiveSha256", sh, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", sh, StringComparison.Ordinal);
        Assert.Contains("libhostfxr.so", sh, StringComparison.Ordinal);
        Assert.Contains("hostfxr.dll", sh, StringComparison.Ordinal);
        Assert.Contains("LICENSES.md", sh, StringComparison.Ordinal);

        Assert.Contains("--self-contained true", ps1, StringComparison.Ordinal);
        Assert.DoesNotContain("--self-contained false", ps1, StringComparison.Ordinal);
        Assert.Contains("hostfxr.dll", ps1, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", ps1, StringComparison.Ordinal);

        var layoutProof = File.ReadAllText(Path.Combine(root, "scripts", "packaged-winforms-layout-proof.sh"));
        Assert.Contains("client-win-x64", layoutProof, StringComparison.Ordinal);
        Assert.Contains("editor-win-x64", layoutProof, StringComparison.Ordinal);
        Assert.Contains("outside the git tree", layoutProof, StringComparison.Ordinal);
        Assert.Contains("hostfxr.dll", layoutProof, StringComparison.Ordinal);
        Assert.Contains("sha256sum", layoutProof, StringComparison.Ordinal);
        Assert.Contains("Linux cannot launch WinForms", layoutProof, StringComparison.Ordinal);

        var winSmoke = File.ReadAllText(Path.Combine(root, "scripts", "packaged-winforms-smoke.ps1"));
        Assert.Contains("--smoke-launch", winSmoke, StringComparison.Ordinal);
        Assert.Contains("Get-PathWithoutDotnet", winSmoke, StringComparison.Ordinal);
        Assert.Contains("Frog.Client.exe", winSmoke, StringComparison.Ordinal);
        Assert.Contains("Frog.Editor.exe", winSmoke, StringComparison.Ordinal);

        var clientProgram = File.ReadAllText(Path.Combine(root, "Frog.Client", "Program.cs"));
        Assert.Contains("--smoke-launch", clientProgram, StringComparison.Ordinal);

        var editorApp = File.ReadAllText(Path.Combine(root, "Frog.Editor", "App.xaml.cs"));
        Assert.Contains("--smoke-launch", editorApp, StringComparison.Ordinal);

        var clientLauncher = File.ReadAllText(Path.Combine(
            root, "Frog.Editor", "Services", "EditorFrogClientLauncher.cs"));
        Assert.Contains("client-win-x64", clientLauncher, StringComparison.Ordinal);
        var serverLauncher = File.ReadAllText(Path.Combine(
            root, "Frog.Editor", "Services", "EditorPlaytestProcessLauncher.cs"));
        Assert.Contains("server-win-x64", serverLauncher, StringComparison.Ordinal);

        var ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        Assert.Contains("packaged-winforms-smoke.ps1", ci, StringComparison.Ordinal);
        Assert.Contains("packaged-winforms-layout-proof.sh", ci, StringComparison.Ordinal);
        Assert.Contains("packaged-playtest-e2e.ps1", ci, StringComparison.Ordinal);
        Assert.Contains("packaged-playtest-e2e.sh", ci, StringComparison.Ordinal);

        var playtestSh = File.ReadAllText(Path.Combine(root, "scripts", "packaged-playtest-e2e.sh"));
        Assert.Contains("outside the git tree", playtestSh, StringComparison.Ordinal);
        Assert.Contains("not proven on Linux agents", playtestSh, StringComparison.Ordinal);
        Assert.Contains("WinForms", playtestSh, StringComparison.Ordinal);

        var playtestPs = File.ReadAllText(Path.Combine(root, "scripts", "packaged-playtest-e2e.ps1"));
        Assert.Contains("sibling", playtestPs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Frog.Server.exe", playtestPs, StringComparison.Ordinal);
        Assert.Contains("outside the git tree", playtestPs, StringComparison.Ordinal);

        var guide = File.ReadAllText(Path.Combine(
            root, "docs", "progress", "phase-09-distribution-admin-hardening", "PACKAGING_GUIDE.md"));
        Assert.Contains("--self-contained true", guide, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", guide, StringComparison.Ordinal);
        Assert.Contains("not proven on Linux agents", guide, StringComparison.Ordinal);
        Assert.Contains("packaged-winforms-smoke.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version = 11", guide, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
