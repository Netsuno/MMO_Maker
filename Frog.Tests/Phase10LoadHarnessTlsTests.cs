using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Core.Security;
using Frog.LoadHarness;
using Frog.Tests.Support;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10LoadHarnessTlsTests
{
    [Fact]
    public void Parse_RequiredWithoutTargetOnAttachIp_FailsFast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LoadHarnessOptions.Parse(["--host", "127.0.0.1", "--port", "6000", "--tls-mode", "Required"]));
        Assert.Contains("tls-target-host", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_SelfHostRequiredWithoutCert_FailsFast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LoadHarnessOptions.Parse(["--self-host", "--tls-mode", "Required", "--tls-target-host", "localhost"]));
        Assert.Contains("tls-cert-path", ex.Message, StringComparison.Ordinal);
        Assert.Contains("no silent cleartext fallback", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnknownMode_Rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            LoadHarnessOptions.Parse(["--tls-mode", "Optional"]));
        Assert.Contains("Off or Required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductSources_HaveNoAcceptAll()
    {
        var root = RepoRoot();
        foreach (var rel in new[]
        {
            Path.Combine("tools", "Frog.LoadHarness", "LoadTcpClient.cs"),
            Path.Combine("tools", "Frog.LoadHarness", "LoadHarnessOptions.cs"),
            Path.Combine("tools", "Frog.LoadHarness", "LoadHarnessRunner.cs"),
            Path.Combine("tools", "Frog.LoadHarness", "InMemoryLoadHost.cs"),
        })
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            Assert.DoesNotContain("AcceptAllCertificate", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DangerousAcceptAnyServerCertificateValidator", text, StringComparison.Ordinal);
            Assert.DoesNotContain("=> true", text, StringComparison.Ordinal);
        }

        var client = File.ReadAllText(Path.Combine(root, "tools", "Frog.LoadHarness", "LoadTcpClient.cs"));
        Assert.Contains("TlsClientAuthenticator", client, StringComparison.Ordinal);
        Assert.Contains("WrapAfterConnectAsync", client, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Required_ConfinedTestCa_Hello()
    {
        using var certs = EphemeralTlsCertificates.Create("localhost");
        var report = await LoadHarnessRunner.RunAsync(new LoadHarnessOptions
        {
            SelfHost = true,
            Scenario = "connect",
            Sessions = 2,
            HoldMilliseconds = 200,
            TlsMode = TlsTransportMode.Required,
            TlsTargetHost = "localhost",
            TlsCaPath = certs.CaPemPath,
            TlsCertificatePath = certs.LeafCertPemPath,
            TlsPrivateKeyPath = certs.LeafKeyPemPath,
            ConnectTimeoutMs = 10_000,
        });

        Assert.Equal("Required", report.Host.TlsMode);
        Assert.Equal("localhost", report.Host.TlsTargetHost);
        Assert.Equal(2, report.Client.TcpConnectOk);
        Assert.Equal(2, report.Client.HelloOk);
        Assert.Equal(0, report.Client.TcpConnectFail);
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Required_UnknownCa_IsRejected()
    {
        using var certs = EphemeralTlsCertificates.Create("localhost");
        var report = await LoadHarnessRunner.RunAsync(new LoadHarnessOptions
        {
            SelfHost = true,
            Scenario = "connect",
            Sessions = 1,
            HoldMilliseconds = 100,
            TlsMode = TlsTransportMode.Required,
            TlsTargetHost = "localhost",
            TlsCertificatePath = certs.LeafCertPemPath,
            TlsPrivateKeyPath = certs.LeafKeyPemPath,
            ConnectTimeoutMs = 8_000,
        });

        Assert.Equal(0, report.Client.HelloOk);
        Assert.True(report.Client.TcpConnectFail >= 1);
    }

    [Fact]
    [Trait("Category", "Load")]
    public async Task DefaultOff_MixedStillWorks()
    {
        var report = await LoadHarnessRunner.RunAsync(new LoadHarnessOptions
        {
            SelfHost = true,
            Scenario = "connect",
            Sessions = 1,
            HoldMilliseconds = 150,
            ConnectTimeoutMs = 8_000,
        });

        Assert.Equal("Off", report.Host.TlsMode);
        Assert.Equal(1, report.Client.HelloOk);
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
