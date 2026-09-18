using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Enums;
using Frog.Core.Security;
using Frog.Server;
using Frog.Server.Config;
using Frog.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10TlsTests
{
    [Fact]
    public void ServerOptions_RequiredNonLoopbackWithoutCert_FailsFast()
    {
        var options = new ServerOptions
        {
            BindAddress = "0.0.0.0",
            Port = 6000,
            AllowNonLoopbackBind = true,
            Tls = new TlsOptions { Mode = TlsTransportMode.Required },
        };
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("no silent cleartext fallback", ex.Message, StringComparison.Ordinal);
        Assert.False(options.IsTlsStartable);
    }

    [Fact]
    public void ServerOptions_RequiredLoopbackWithoutCert_FailsFastUnlessAllowCleartextLoopback()
    {
        var required = new ServerOptions
        {
            BindAddress = "127.0.0.1",
            Port = 6000,
            Tls = new TlsOptions { Mode = TlsTransportMode.Required },
        };
        Assert.Throws<InvalidOperationException>(() => required.Validate());

        var allowed = new ServerOptions
        {
            BindAddress = "127.0.0.1",
            Port = 6000,
            Tls = new TlsOptions { Mode = TlsTransportMode.Required, AllowCleartextLoopback = true },
        };
        allowed.Validate();
        Assert.True(allowed.IsTlsStartable);
    }

    [Fact]
    public void ServerOptions_AllowCleartextLoopbackDoesNotAuthorizePublicBindWithoutCert()
    {
        var options = new ServerOptions
        {
            BindAddress = "0.0.0.0",
            Port = 6000,
            AllowNonLoopbackBind = true,
            Tls = new TlsOptions
            {
                Mode = TlsTransportMode.Required,
                AllowCleartextLoopback = true,
            },
        };
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("no silent cleartext fallback", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_RequiredNonLoopbackWithoutCert_FailsFastAtStart()
    {
        var port = GetFreePort();
        using var host = FrogServerHostFactory
            .CreateHostBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "0.0.0.0",
                    ["Server:AllowNonLoopbackBind"] = "true",
                    ["Server:Tls:Mode"] = "Required",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                });
            })
            .Build();

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());
        var text = Flatten(ex);
        Assert.Contains("Tls", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cleartext", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task RequiredTls_ValidCertAndName_HelloBeforeFraming()
    {
        using var certs = EphemeralTlsCertificates.Create("localhost");
        var port = GetFreePort();
        using var host = CreateTlsHost(port, certs.LeafCertPemPath, certs.LeafKeyPemPath);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            await using var ssl = await TlsClientAuthenticator.WrapAfterConnectAsync(
                tcp.GetStream(),
                ClientTlsOptions.Required("localhost", certs.TrustRoots),
                "127.0.0.1");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var hello = await ReadFrameAsync(ssl, cts.Token);
            Assert.Equal((byte)PacketId.Hello, hello[0]);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task RequiredTls_PfxWithoutPassword_Hello()
    {
        using var certs = EphemeralTlsCertificates.Create("localhost");
        var port = GetFreePort();
        using var host = CreateTlsHostPfx(port, certs.LeafPfxPath);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            await using var ssl = await TlsClientAuthenticator.WrapAfterConnectAsync(
                tcp.GetStream(),
                ClientTlsOptions.Required("localhost", certs.TrustRoots),
                "127.0.0.1");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var hello = await ReadFrameAsync(ssl, cts.Token);
            Assert.Equal((byte)PacketId.Hello, hello[0]);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task RequiredTls_ExpiredCertificate_IsRejected()
    {
        using var certs = EphemeralTlsCertificates.Create("localhost", expiredLeaf: true);
        var port = GetFreePort();
        using var host = CreateTlsHost(port, certs.LeafCertPemPath, certs.LeafKeyPemPath);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            await Assert.ThrowsAsync<AuthenticationException>(() =>
                TlsClientAuthenticator.WrapAfterConnectAsync(
                    tcp.GetStream(),
                    ClientTlsOptions.Required("localhost", certs.TrustRoots),
                    "127.0.0.1"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task RequiredTls_WrongHostname_IsRejected()
    {
        using var certs = EphemeralTlsCertificates.Create("other.example.test");
        var port = GetFreePort();
        using var host = CreateTlsHost(port, certs.LeafCertPemPath, certs.LeafKeyPemPath);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            await Assert.ThrowsAsync<AuthenticationException>(() =>
                TlsClientAuthenticator.WrapAfterConnectAsync(
                    tcp.GetStream(),
                    ClientTlsOptions.Required("localhost", certs.TrustRoots),
                    "127.0.0.1"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task RequiredTls_UnknownCa_IsRejected()
    {
        using var certs = EphemeralTlsCertificates.Create("localhost");
        var port = GetFreePort();
        using var host = CreateTlsHost(port, certs.LeafCertPemPath, certs.LeafKeyPemPath);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            await Assert.ThrowsAsync<AuthenticationException>(() =>
                TlsClientAuthenticator.WrapAfterConnectAsync(
                    tcp.GetStream(),
                    ClientTlsOptions.Required("localhost"),
                    "127.0.0.1"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task RequiredTls_CleartextClient_GetsNoHelloFallback()
    {
        using var certs = EphemeralTlsCertificates.Create("localhost");
        var port = GetFreePort();
        using var host = CreateTlsHost(port, certs.LeafCertPemPath, certs.LeafKeyPemPath);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            var stream = tcp.GetStream();
            await stream.WriteAsync(new byte[] { 0x16, 0x03, 0x01, 0x00, 0x01, 0x00 });
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var buffer = new byte[16];
            try
            {
                var n = await stream.ReadAsync(buffer, cts.Token);
                Assert.True(n == 0 || buffer[0] != (byte)PacketId.Hello);
                if (n >= 5)
                {
                    var framedLen = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                    Assert.NotEqual(1, framedLen);
                }
            }
            catch (OperationCanceledException)
            {
                // Handshake blocked / no Hello frame — not a silent cleartext fallback.
            }
            catch (IOException)
            {
                // Server closed after a failed TLS handshake.
            }
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task ClientRequired_ServerOff_DoesNotFallBackToCleartext()
    {
        var port = GetFreePort();
        using var host = CreateCleartextHost(port);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            await Assert.ThrowsAsync<AuthenticationException>(() =>
                TlsClientAuthenticator.WrapAfterConnectAsync(
                    tcp.GetStream(),
                    ClientTlsOptions.Required("localhost"),
                    "127.0.0.1"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task ModeOff_StillServesCleartextHello()
    {
        var port = GetFreePort();
        using var host = CreateCleartextHost(port);
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var hello = await ReadFrameAsync(tcp.GetStream(), cts.Token);
            Assert.Equal((byte)PacketId.Hello, hello[0]);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task AllowCleartextLoopback_ExplicitOptIn_ServesHelloWithoutCert()
    {
        var port = GetFreePort();
        using var host = FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["Server:Tls:Mode"] = "Required",
                    ["Server:Tls:AllowCleartextLoopback"] = "true",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                });
            })
            .Build();
        await host.StartAsync();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var hello = await ReadFrameAsync(tcp.GetStream(), cts.Token);
            Assert.Equal((byte)PacketId.Hello, hello[0]);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public void Validator_NeverAcceptsNullOrMissingHost_NoAcceptAll()
    {
        Assert.False(TlsCertificateValidator.IsAcceptable(null, null, SslPolicyErrors.None, "localhost", null));
        using var certs = EphemeralTlsCertificates.Create("localhost");
        Assert.False(
            TlsCertificateValidator.IsAcceptable(
                certs.Leaf,
                null,
                SslPolicyErrors.None,
                targetHost: "",
                certs.TrustRoots));
        Assert.True(
            TlsCertificateValidator.IsAcceptable(
                certs.Leaf,
                null,
                SslPolicyErrors.RemoteCertificateChainErrors,
                "localhost",
                certs.TrustRoots));
        Assert.False(
            TlsCertificateValidator.IsAcceptable(
                certs.Leaf,
                null,
                SslPolicyErrors.RemoteCertificateChainErrors,
                "localhost",
                customTrustRoots: null));
    }

    [Fact]
    public void ProductSources_HaveNoAcceptAllAndWrapBeforeFraming()
    {
        var root = RepoRoot();
        foreach (var file in new[]
        {
            Path.Combine(root, "Frog.Core", "Security", "TlsCertificateValidator.cs"),
            Path.Combine(root, "Frog.Core", "Security", "TlsClientAuthenticator.cs"),
            Path.Combine(root, "Frog.Server", "Network", "TlsServerTransport.cs"),
        })
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("AcceptAllCertificate", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DangerousAcceptAnyServerCertificateValidator", text, StringComparison.Ordinal);
            Assert.DoesNotContain("=> true", text, StringComparison.Ordinal);
        }

        var validator = File.ReadAllText(Path.Combine(root, "Frog.Core", "Security", "TlsCertificateValidator.cs"));
        Assert.DoesNotContain("return true;", validator.Replace("return verifyChain.Build(cert2);", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        var transport = File.ReadAllText(Path.Combine(root, "Frog.Server", "Network", "TlsServerTransport.cs"));
        Assert.DoesNotContain("EphemeralKeySet", transport, StringComparison.Ordinal);
        Assert.Contains("UserKeySet", transport, StringComparison.Ordinal);
        Assert.Contains("X509ContentType.Pfx", transport, StringComparison.Ordinal);

        var client = File.ReadAllText(Path.Combine(root, "Frog.Client", "Network", "FrogGameClient.cs"));
        Assert.Contains("TlsClientAuthenticator", client, StringComparison.Ordinal);
        Assert.Contains("WrapAfterConnectAsync", client, StringComparison.Ordinal);

        var server = File.ReadAllText(Path.Combine(root, "Frog.Server", "Services", "GameServerService.cs"));
        Assert.Contains("WrapAfterAcceptAsync", server, StringComparison.Ordinal);
        var helloIndex = server.IndexOf("SendHelloAsync", StringComparison.Ordinal);
        var wrapIndex = server.IndexOf("WrapAfterAcceptAsync", StringComparison.Ordinal);
        Assert.True(wrapIndex >= 0 && helloIndex > wrapIndex);
    }

    [Fact]
    public void TestFixture_WritesPkcs12AndLoadsUserKeySet_NoEphemeralKeySet()
    {
        var root = RepoRoot();
        var fixture = File.ReadAllText(Path.Combine(root, "Frog.Tests", "Support", "EphemeralTlsCertificates.cs"));
        Assert.Contains("UserKeySet", fixture, StringComparison.Ordinal);
        Assert.Contains("ExportPkcs8PrivateKeyPem", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("EphemeralKeySet", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptAllCertificate", fixture, StringComparison.Ordinal);

        using var certs = EphemeralTlsCertificates.Create("localhost");
        Assert.True(File.Exists(certs.LeafPfxPath));
        Assert.True(File.Exists(certs.LeafKeyPemPath));
        Assert.True(certs.Leaf.HasPrivateKey);
        using var rsa = certs.Leaf.GetRSAPrivateKey();
        Assert.NotNull(rsa);
    }

    [Fact]
    public void CommittedAppsettings_DefaultTlsModeIsOff_NoProductionCerts()
    {
        var root = RepoRoot();
        var json = File.ReadAllText(Path.Combine(root, "Frog.Server", "appsettings.json"));
        Assert.Contains("\"mode\": \"Off\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN CERTIFICATE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", json, StringComparison.Ordinal);

        foreach (var cert in Directory.EnumerateFiles(root, "*.pfx", SearchOption.AllDirectories))
        {
            Assert.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", cert, StringComparison.Ordinal);
        }

        foreach (var pem in Directory.EnumerateFiles(root, "*.pem", SearchOption.AllDirectories))
        {
            Assert.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", pem, StringComparison.Ordinal);
        }
    }

    private static IHost CreateTlsHost(int port, string certPath, string keyPath)
        => FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["Server:Tls:Mode"] = "Required",
                    ["Server:Tls:CertificatePath"] = certPath,
                    ["Server:Tls:PrivateKeyPath"] = keyPath,
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                });
            })
            .Build();

    private static IHost CreateTlsHostPfx(int port, string pfxPath)
        => FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["Server:Tls:Mode"] = "Required",
                    ["Server:Tls:PfxPath"] = pfxPath,
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                });
            })
            .Build();

    private static IHost CreateCleartextHost(int port)
        => FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["Server:Tls:Mode"] = "Off",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                });
            })
            .Build();

    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        Assert.True(length > 0 && length <= 1024 * 1024);
        var payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken)
                .ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }
    }

    private static string Flatten(Exception ex)
    {
        var sb = new StringBuilder();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            sb.AppendLine(current.Message);
        }

        return sb.ToString();
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

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
