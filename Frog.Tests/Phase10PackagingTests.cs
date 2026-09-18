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

        var guide = File.ReadAllText(Path.Combine(
            root, "docs", "progress", "phase-09-distribution-admin-hardening", "PACKAGING_GUIDE.md"));
        Assert.Contains("--self-contained true", guide, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", guide, StringComparison.Ordinal);
        Assert.Contains("not proven on Linux agents", guide, StringComparison.Ordinal);
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
