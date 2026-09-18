using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

public sealed class Phase9PackagingTests
{
    [Fact]
    public void OperatorPublishScripts_AndGuides_Exist()
    {
        var root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "scripts", "publish-frog.sh")));
        Assert.True(File.Exists(Path.Combine(root, "scripts", "publish-frog.ps1")));
        Assert.True(File.Exists(Path.Combine(root, "scripts", "run-packaged-server.sh")));
        Assert.True(File.Exists(Path.Combine(root, "scripts", "run-packaged-server.ps1")));
        Assert.True(File.Exists(Path.Combine(root, "scripts", "packaged-server-smoke.sh")));

        var guide = File.ReadAllText(Path.Combine(
            root, "docs", "progress", "phase-09-distribution-admin-hardening", "PACKAGING_GUIDE.md"));
        Assert.DoesNotContain("Status:** stub", guide, StringComparison.Ordinal);
        Assert.Contains("server-linux-x64", guide, StringComparison.Ordinal);
        Assert.Contains("server-win-x64", guide, StringComparison.Ordinal);
        Assert.Contains("client-win-x64", guide, StringComparison.Ordinal);
        Assert.Contains("editor-win-x64", guide, StringComparison.Ordinal);
        Assert.Contains("appsettings.Local.json", guide, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version = 10", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/runs/999", guide, StringComparison.Ordinal);

        var runbook = File.ReadAllText(Path.Combine(
            root, "docs", "progress", "phase-09-distribution-admin-hardening", "OPERATIONS_RUNBOOK.md"));
        Assert.DoesNotContain("Status:** stub", runbook, StringComparison.Ordinal);
        Assert.Contains("run-packaged-server.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("FROG_SHUTDOWN_FILE", runbook, StringComparison.Ordinal);
        Assert.Contains("Database.Migrate()", runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerProject_DoesNotPublishLocalOverlay()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Server", "Frog.Server.csproj"));
        Assert.Contains("appsettings.Local.json", csproj, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory>Never", csproj, StringComparison.Ordinal);
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
