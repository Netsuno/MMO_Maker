using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Identity;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Enums;
using Frog.Server;
using Frog.Server.Config;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Frog.Tests;

public sealed class MaintenanceModeTests
{
    [Fact]
    public void Protocol_StaysV11_NoNewAdminOpcode()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        var names = Enum.GetNames<PacketId>();
        Assert.DoesNotContain("MaintenanceRequest", names);
        Assert.DoesNotContain("AdminCommand", names);
        Assert.Equal(78, (byte)PacketId.ModerateRequest);
    }

    [Fact]
    public void PlayerFacing_MapsMaintenanceWire_WithoutLoginShellEdits()
    {
        Assert.Equal(MaintenanceMessages.PlayerFacing, MaintenanceMessages.ToPlayerFacing(MaintenanceMessages.LoginRejected));
        Assert.True(MaintenanceMessages.IsMaintenanceSignal("Serveur en maintenance. Reessayez plus tard."));
        Assert.False(MaintenanceMessages.IsMaintenanceSignal("Identifiants invalides."));

        var facing = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Services", "PlayerFacingMessages.cs"));
        Assert.Contains("MaintenanceMessages.IsMaintenanceSignal", facing, StringComparison.Ordinal);
        Assert.Contains("MaintenanceMessages.PlayerFacing", facing, StringComparison.Ordinal);
        Assert.Contains("FromServerOrNetwork", facing, StringComparison.Ordinal);

        var loginShell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "LoginShell.cs"));
        Assert.DoesNotContain("MaintenanceMessages", loginShell, StringComparison.Ordinal);
        Assert.Contains("CardWidth = 400", loginShell, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_ConfigAndFlagFile_ThenOverride()
    {
        var options = new MaintenanceOptions { Enabled = false, AllowOperators = true };
        var monitor = new StaticMonitor(options);
        var operators = new AlwaysOperatorDirectory();
        var svc = new MaintenanceService(monitor, operators, NullLogger<MaintenanceService>.Instance);
        Assert.False(svc.IsEnabled);
        Assert.False(await svc.ShouldRejectLoginAsync(Guid.NewGuid()));

        options.Enabled = true;
        Assert.True(svc.IsEnabled);
        Assert.False(await svc.ShouldRejectLoginAsync(Guid.NewGuid()));
        Assert.True(await svc.ShouldRejectLoginAsync(null));
        Assert.Equal(MaintenanceMessages.LoginRejected, svc.WireMessage);

        var flag = Path.Combine(Path.GetTempPath(), "frog-maint-" + Guid.NewGuid().ToString("N") + ".flag");
        try
        {
            options.Enabled = false;
            options.FlagFile = flag;
            Assert.False(svc.IsEnabled);
            await File.WriteAllTextAsync(flag, "1\n");
            Assert.True(svc.IsEnabled);
            await File.WriteAllTextAsync(flag, "off\n");
            Assert.False(svc.IsEnabled);

            svc.SetEnabledOverride(true);
            Assert.True(svc.IsEnabled);
            svc.SetEnabledOverride(false);
            Assert.False(svc.IsEnabled);
            svc.SetEnabledOverride(null);
            Assert.False(svc.IsEnabled);
        }
        finally
        {
            if (File.Exists(flag))
            {
                File.Delete(flag);
            }
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_MaintenanceRejectsLoginAndRegister_OperatorBypasses()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            var player = UniqueUser("pl");
            var gm = UniqueUser("gm");
            const string password = "password123";

            await using var bootstrap = new TcpProbe();
            await bootstrap.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await bootstrap.ReadFrameAsync())[0]);
            await bootstrap.SendFrameAsync(BuildAuth(PacketId.RegisterRequest, player, password));
            Assert.True(TryDecodeStatus(await bootstrap.ReadUntilAsync(PacketId.RegisterResult), out var playerReg, out _));
            Assert.True(playerReg);
            await bootstrap.SendFrameAsync(BuildAuth(PacketId.RegisterRequest, gm, password));
            Assert.True(TryDecodeStatus(await bootstrap.ReadUntilAsync(PacketId.RegisterResult), out var gmReg, out _));
            Assert.True(gmReg);

            var accounts = host.Services.GetRequiredService<IAccountRepository>();
            var operators = host.Services.GetRequiredService<IOperatorDirectory>();
            var gmAccount = await accounts.FindByUsernameAsync(gm);
            Assert.NotNull(gmAccount);
            Assert.Equal(
                OperatorGrantStatus.Granted,
                (await operators.GrantAsync(gmAccount!.Id, "sql", "maintenance-mvp")).Status);

            host.Services.GetRequiredService<MaintenanceService>().SetEnabledOverride(true);

            await using var playerLogin = new TcpProbe();
            await playerLogin.ConnectAsync("127.0.0.1", port);
            _ = await playerLogin.ReadFrameAsync();
            await playerLogin.SendFrameAsync(BuildAuth(PacketId.LoginRequest, player, password));
            Assert.True(TryDecodeStatus(await playerLogin.ReadUntilAsync(PacketId.LoginResult), out var playerOk, out var playerMsg));
            Assert.False(playerOk);
            Assert.Equal(MaintenanceMessages.LoginRejected, playerMsg);
            Assert.Equal(MaintenanceMessages.PlayerFacing, MaintenanceMessages.ToPlayerFacing(playerMsg));

            await using var registerBlocked = new TcpProbe();
            await registerBlocked.ConnectAsync("127.0.0.1", port);
            _ = await registerBlocked.ReadFrameAsync();
            await registerBlocked.SendFrameAsync(BuildAuth(PacketId.RegisterRequest, UniqueUser("nw"), password));
            Assert.True(TryDecodeStatus(await registerBlocked.ReadUntilAsync(PacketId.RegisterResult), out var newReg, out var newRegMsg));
            Assert.False(newReg);
            Assert.Equal(MaintenanceMessages.LoginRejected, newRegMsg);

            await using var gmLogin = new TcpProbe();
            await gmLogin.ConnectAsync("127.0.0.1", port);
            _ = await gmLogin.ReadFrameAsync();
            await gmLogin.SendFrameAsync(BuildAuth(PacketId.LoginRequest, gm, password));
            Assert.True(TryDecodeStatus(await gmLogin.ReadUntilAsync(PacketId.LoginResult), out var gmOk, out _));
            Assert.True(gmOk);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_MaintenanceEnabledInConfig_RejectsBeforeAccountsExist()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port, maintenanceEnabled: true);
        await host.StartAsync();
        try
        {
            await using var client = new TcpProbe();
            await client.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
            await client.SendFrameAsync(BuildAuth(PacketId.RegisterRequest, UniqueUser("x"), "password123"));
            Assert.True(TryDecodeStatus(await client.ReadUntilAsync(PacketId.RegisterResult), out var ok, out var msg));
            Assert.False(ok);
            Assert.Equal(MaintenanceMessages.LoginRejected, msg);

            await client.SendFrameAsync(BuildAuth(PacketId.LoginRequest, "nobody", "password123"));
            Assert.True(TryDecodeStatus(await client.ReadUntilAsync(PacketId.LoginResult), out var loginOk, out var loginMsg));
            Assert.False(loginOk);
            Assert.Equal(MaintenanceMessages.LoginRejected, loginMsg);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public void StatusDoc_RecordsConfigEnvAndNoProtocolBump()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "maintenance-launcher", "STATUS.md");
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("MaintenanceService", text, StringComparison.Ordinal);
        Assert.Contains("FROG_MAINTENANCE", text, StringComparison.Ordinal);
        Assert.Contains("AllowOperators", text, StringComparison.Ordinal);
        Assert.Contains("ClientVersionManifest", text, StringComparison.Ordinal);
        Assert.Contains("reste 11", text, StringComparison.Ordinal);
        Assert.Contains("PlayerFacingMessages", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NAudio", text, StringComparison.Ordinal);
    }

    private static IHost CreateInMemoryHost(int port, bool maintenanceEnabled = false)
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
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:AllowInMemoryFallback"] = "true",
                    ["Maintenance:Enabled"] = maintenanceEnabled ? "true" : "false",
                    ["Maintenance:AllowOperators"] = "true",
                });
            })
            .Build();

    private static byte[] BuildAuth(PacketId id, string user, string pass)
    {
        var u = Encoding.UTF8.GetBytes(user);
        var p = Encoding.UTF8.GetBytes(pass);
        var payload = new byte[1 + 1 + u.Length + 1 + p.Length];
        payload[0] = (byte)id;
        payload[1] = (byte)u.Length;
        u.CopyTo(payload, 2);
        payload[2 + u.Length] = (byte)p.Length;
        p.CopyTo(payload, 3 + u.Length);
        return payload;
    }

    private static bool TryDecodeStatus(byte[] payload, out bool success, out string message)
    {
        success = false;
        message = string.Empty;
        if (payload.Length < 3)
        {
            return false;
        }

        success = payload[1] != 0;
        var len = payload[2];
        if (payload.Length != 3 + len)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(payload, 3, len);
        return true;
    }

    private static string UniqueUser(string prefix)
        => prefix + Guid.NewGuid().ToString("N")[..8];

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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

    private sealed class StaticMonitor(MaintenanceOptions value) : IOptionsMonitor<MaintenanceOptions>
    {
        public MaintenanceOptions CurrentValue { get; } = value;

        public MaintenanceOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<MaintenanceOptions, string?> listener) => EmptyDisposable.Instance;

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class AlwaysOperatorDirectory : IOperatorDirectory
    {
        public Task<bool> IsOperatorAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(accountId != Guid.Empty);

        public Task<OperatorGrantResult> GrantAsync(
            Guid accountId,
            string grantedBy,
            string? note = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OperatorGrantResult(OperatorGrantStatus.Granted));

        public Task<bool> RevokeAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class TcpProbe : IAsyncDisposable
    {
        private TcpClient? _tcp;
        private NetworkStream? _stream;

        public async Task ConnectAsync(string host, int port)
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync(host, port);
            _stream = _tcp.GetStream();
        }

        public async Task SendFrameAsync(byte[] payload)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await _stream!.WriteAsync(frame);
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(8));
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await ReadExactAsync(payload, cts.Token);
            return payload;
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
            while (DateTime.UtcNow < deadline)
            {
                var frame = await ReadFrameAsync(deadline - DateTime.UtcNow);
                if (frame.Length > 0 && frame[0] == (byte)id)
                {
                    return frame;
                }
            }

            throw new TimeoutException("expected packet not received: " + id);
        }

        private async Task ReadExactAsync(byte[] buffer, System.Threading.CancellationToken ct)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
                if (n == 0)
                {
                    throw new EndOfStreamException();
                }

                read += n;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _tcp?.Close();
            _stream?.Dispose();
            _tcp?.Dispose();
            await Task.CompletedTask;
        }
    }
}
