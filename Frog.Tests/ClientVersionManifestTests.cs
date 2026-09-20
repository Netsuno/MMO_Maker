using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Xunit;

namespace Frog.Tests;

public sealed class ClientVersionManifestTests
{
    [Fact]
    public void Parse_AndCompare_CurrentUpdateAndProtocol()
    {
        var local = ClientVersionManifest.Parse("product=10.3.0\nprotocol=11\nminClient=10.3.0\n");
        Assert.Equal("10.3.0", local.ProductVersion);
        Assert.Equal((ushort)11, local.ProtocolVersion);
        Assert.Equal("10.3.0", local.MinClientVersion);
        Assert.Equal(FrogWireProtocol.Version, local.ProtocolVersion);

        var current = ClientVersionManifest.Compare(local, local);
        Assert.Equal(VersionCheckOutcome.Current, current.Outcome);

        var remoteNewer = new ClientVersionManifest("10.4.0", 11, "10.3.0");
        var available = ClientVersionManifest.Compare(local, remoteNewer);
        Assert.Equal(VersionCheckOutcome.UpdateAvailable, available.Outcome);

        var remoteMin = new ClientVersionManifest("10.5.0", 11, "10.4.0");
        var required = ClientVersionManifest.Compare(local, remoteMin);
        Assert.Equal(VersionCheckOutcome.UpdateRequired, required.Outcome);

        var remoteProto = new ClientVersionManifest("10.3.0", 12, "10.3.0");
        var proto = ClientVersionManifest.Compare(local, remoteProto);
        Assert.Equal(VersionCheckOutcome.IncompatibleProtocol, proto.Outcome);
    }

    [Fact]
    public void ScaffoldingVersionFile_MatchesCurrentClientAndProtocol11()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "maintenance-launcher", "VERSION");
        var loaded = ClientVersionManifest.LoadFile(path);
        Assert.Equal(ClientVersionManifest.CurrentScaffolding.ProductVersion, loaded.ProductVersion);
        Assert.Equal((ushort)11, loaded.ProtocolVersion);
        Assert.Equal(FrogWireProtocol.Version, loaded.ProtocolVersion);
        Assert.Equal(0, ClientVersionManifest.CompareProduct("10.3.0", "10.3.0"));
        Assert.True(ClientVersionManifest.CompareProduct("10.2.9", "10.3.0") < 0);
    }

    [Fact]
    public void CheckClientVersionScript_IsStubCompare_NotAnInstaller()
    {
        var root = RepoRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "check-client-version.sh"));
        Assert.Contains("--remote", script, StringComparison.Ordinal);
        Assert.Contains("incompatible-protocol", script, StringComparison.Ordinal);
        Assert.Contains("update-required", script, StringComparison.Ordinal);
        Assert.Contains("update-available", script, StringComparison.Ordinal);
        Assert.DoesNotContain("msiexec", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("curl ", script, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "docs", "progress", "maintenance-launcher", "remote-VERSION.example")));
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
