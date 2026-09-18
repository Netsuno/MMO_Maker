using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Server;
using Frog.Server.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10ClosedBetaTests
{
    [Fact]
    public async Task OperatorCommands_CreateDoesNotGrantGm_ResetRevokeAndSanctions()
    {
        var accounts = new InMemoryAccountRepository();
        var sessions = new InMemoryAuthSessionRepository();
        var operators = new InMemoryOperatorDirectory(accounts);
        var sanctions = new InMemoryAccountSanctionStore();
        var ops = new OperatorAccountCommands(accounts, sessions, operators, sanctions);

        var created = await ops.CreateAccountAsync("beta-player", "password123");
        Assert.Equal(AccountCreateStatus.Created, created.Status);
        Assert.False(await ops.IsOperatorAsync("beta-player"));

        var gm = await ops.CreateAccountAsync("beta-gm", "password123");
        Assert.Equal(OperatorGrantStatus.Granted, (await ops.GrantOperatorAsync("beta-gm", "sql")).Status);
        Assert.True(await ops.IsOperatorAsync("beta-gm"));
        Assert.False(await ops.IsOperatorAsync("beta-player"));

        Assert.True(await ops.ResetPasswordAsync("beta-player", "password456"));
        var player = await accounts.FindByUsernameAsync("beta-player");
        Assert.NotNull(player);
        Assert.True(Frog.Core.Security.PasswordHasher.VerifyPassword("password456", player!.PasswordHash));

        var issued = await sessions.IssueAsync(player.Id, TimeSpan.FromHours(1));
        Assert.Equal(AuthSessionIssueStatus.Issued, issued.Status);
        Assert.True(await ops.RevokeSessionsAsync("beta-player"));
        Assert.Equal(AuthSessionValidationStatus.Revoked, (await sessions.ValidateTokenAsync(issued.Token!)).Status);

        await ops.ApplySanctionAsync("beta-player", "beta-gm", SanctionKinds.Mute, "test");
        Assert.True(await sanctions.HasActiveMuteAsync(player.Id));
        Assert.True(await ops.LiftSanctionAsync("beta-player", "beta-gm", SanctionKinds.Mute, "ok"));
        Assert.False(await sanctions.HasActiveMuteAsync(player.Id));

        Assert.True(await ops.RevokeOperatorAsync("beta-gm"));
        Assert.False(await ops.IsOperatorAsync("beta-gm"));
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task TcpRegister_ProvisionedOnly_IsRefused()
    {
        await AssertRegisterClosedAsync("ProvisionedOnly", "Inscriptions fermees.");
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task TcpRegister_InviteOnly_IsRefused_UntilInviteJalon()
    {
        await AssertRegisterClosedAsync("InviteOnly", "Inscriptions fermees.");
    }

    private static async Task AssertRegisterClosedAsync(string mode, string expectedMessage)
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
                    ["Registration:Mode"] = mode,
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
            var stream = tcp.GetStream();
            _ = await ReadFrameAsync(stream);
            await SendFrameAsync(stream, BuildRegister("fresh-user", "password123"));
            var result = await ReadUntilAsync(stream, PacketId.RegisterResult);
            Assert.Equal(0, result[1]);
            var len = result[2];
            var msg = Encoding.UTF8.GetString(result, 3, len);
            Assert.Equal(expectedMessage, msg);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static byte[] BuildRegister(string user, string pass)
    {
        var u = Encoding.UTF8.GetBytes(user);
        var p = Encoding.UTF8.GetBytes(pass);
        var payload = new byte[1 + 1 + u.Length + 1 + p.Length];
        payload[0] = (byte)PacketId.RegisterRequest;
        payload[1] = (byte)u.Length;
        u.CopyTo(payload, 2);
        payload[2 + u.Length] = (byte)p.Length;
        p.CopyTo(payload, 3 + u.Length);
        return payload;
    }

    private static async Task SendFrameAsync(NetworkStream stream, byte[] payload)
    {
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame, 4);
        await stream.WriteAsync(frame);
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
    {
        var lenBuf = new byte[4];
        await ReadExactAsync(stream, lenBuf);
        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        var payload = new byte[len];
        await ReadExactAsync(stream, payload);
        return payload;
    }

    private static async Task<byte[]> ReadUntilAsync(NetworkStream stream, PacketId id)
    {
        for (var i = 0; i < 16; i++)
        {
            var frame = await ReadFrameAsync(stream);
            if (frame.Length > 0 && frame[0] == (byte)id)
            {
                return frame;
            }
        }

        throw new TimeoutException("expected " + id);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
            if (n == 0)
            {
                throw new System.IO.EndOfStreamException();
            }

            read += n;
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var p = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return p;
    }
}
