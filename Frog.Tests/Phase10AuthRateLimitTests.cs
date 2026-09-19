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
using Frog.Server.Security;
using Frog.Server.Services;
using Frog.Server.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10AuthRateLimitTests
{
    [Fact]
    public void AuthRateLimitKey_StripsPort_AndUnmapsV4Mapped()
    {
        Assert.Equal("127.0.0.1", AuthRateLimitKey.NormalizeIp("127.0.0.1:54321"));
        Assert.Equal("127.0.0.1", AuthRateLimitKey.NormalizeIp("127.0.0.1:9"));
        Assert.Equal("::1", AuthRateLimitKey.NormalizeIp("[::1]:6000"));
        var mapped = new IPEndPoint(IPAddress.Parse("::ffff:192.0.2.10"), 4444).ToString();
        Assert.Equal("192.0.2.10", AuthRateLimitKey.NormalizeIp(mapped));
        Assert.Equal("192.0.2.10", AuthRateLimitKey.NormalizeIp("::ffff:192.0.2.10"));
        Assert.NotEqual("127.0.0.1:54321", AuthRateLimitKey.Ip("127.0.0.1:54321"));
        Assert.Equal(AuthRateLimitKey.Ip("127.0.0.1:1"), AuthRateLimitKey.Ip("127.0.0.1:65535"));
        Assert.Equal(
            AuthRateLimitKey.IpUser("10.0.0.1:1", "Alice"),
            AuthRateLimitKey.IpUser("10.0.0.1:9999", "alice"));
    }

    [Fact]
    public void AuthRateLimiter_IpUser_BlocksAfter8_IndependentOfSourcePortKey()
    {
        var limiter = new AuthRateLimiter();
        const string user = "brute";
        for (var i = 0; i < 8; i++)
        {
            Assert.True(limiter.TryAllow("127.0.0.1:" + (1000 + i), user));
            limiter.RegisterFailure("127.0.0.1:" + (1000 + i), user);
        }

        Assert.False(limiter.TryAllow("127.0.0.1:65000", user));
        limiter.RegisterSuccess("127.0.0.1:65000", user);
        Assert.True(limiter.TryAllow("127.0.0.1:1", user));
    }

    [Fact]
    public void AuthRateLimiter_Ip_BlocksAfter30_AcrossUsernames()
    {
        var limiter = new AuthRateLimiter();
        for (var i = 0; i < 30; i++)
        {
            var user = "u" + i;
            Assert.True(limiter.TryAllow("203.0.113.9:40000", user));
            limiter.RegisterFailure("203.0.113.9:40000", user);
        }

        Assert.False(limiter.TryAllow("203.0.113.9:40001", "fresh-user"));
        Assert.False(limiter.TryAllow("203.0.113.9:9", null));
    }

    [Fact]
    public void AuthRateLimiter_SuccessClearsIpUser_NotSharedIpWindow()
    {
        var limiter = new AuthRateLimiter(ipUserMaxFailures: 2, ipMaxFailures: 4);
        limiter.RegisterFailure("198.51.100.2:1", "a");
        limiter.RegisterFailure("198.51.100.2:2", "a");
        Assert.False(limiter.TryAllow("198.51.100.2:3", "a"));
        limiter.RegisterSuccess("198.51.100.2:3", "a");
        Assert.True(limiter.TryAllow("198.51.100.2:4", "a"));
        limiter.RegisterFailure("198.51.100.2:5", "b");
        limiter.RegisterFailure("198.51.100.2:6", "c");
        limiter.RegisterFailure("198.51.100.2:7", "d");
        limiter.RegisterFailure("198.51.100.2:8", "e");
        Assert.False(limiter.TryAllow("198.51.100.2:9", "a"));
    }

    [Fact]
    public async Task AuthService_RegisterIsRateLimited_SameWindowAsLogin()
    {
        var auth = new AuthService(new InMemoryAccountRepository(), new AuthRateLimiter(ipUserMaxFailures: 2));
        var first = await auth.RegisterAccountAsync("new-user", "password123", "10.1.2.3:1");
        Assert.Equal(Frog.Application.Identity.AccountCreateStatus.Created, first.Status);
        Assert.Equal(
            Frog.Application.Identity.AccountCreateStatus.DuplicateUsername,
            (await auth.RegisterAccountAsync("new-user", "password123", "10.1.2.3:2")).Status);
        Assert.Equal(
            Frog.Application.Identity.AccountCreateStatus.DuplicateUsername,
            (await auth.RegisterAccountAsync("new-user", "password123", "10.1.2.3:8")).Status);
        var limited = await auth.RegisterAccountAsync("new-user", "password123", "10.1.2.3:3");
        Assert.Equal(Frog.Application.Identity.AccountCreateStatus.RateLimited, limited.Status);
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task TcpLogin_DistinctSourcePorts_ShareIpUserWindow()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string user = "same-user";
            const string password = "password123";
            var accounts = host.Services.GetRequiredService<IAccountRepository>();
            Assert.Equal(AccountCreateStatus.Created, (await accounts.TryCreateAsync(user, password)).Status);

            for (var i = 0; i < 8; i++)
            {
                await using var client = new Probe();
                await client.ConnectAsync("127.0.0.1", port);
                Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
                await client.SendFrameAsync(BuildLogin(user, "wrong-pass-xx"));
                var result = await client.ReadUntilAsync(PacketId.LoginResult);
                Assert.Equal(0, result[1]);
            }

            await using var otherPort = new Probe();
            await otherPort.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await otherPort.ReadFrameAsync())[0]);
            await otherPort.SendFrameAsync(BuildLogin(user, password));
            var blocked = await otherPort.ReadUntilAsync(PacketId.LoginResult);
            Assert.Equal(0, blocked[1]);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IHost CreateInMemoryHost(int port)
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
                });
            })
            .Build();

    private static byte[] BuildLogin(string user, string pass, PacketId id = PacketId.LoginRequest)
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

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var p = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return p;
    }

    private sealed class Probe : IAsyncDisposable
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

        public async Task<byte[]> ReadFrameAsync()
        {
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await ReadExactAsync(payload);
            return payload;
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id)
        {
            for (var i = 0; i < 32; i++)
            {
                var frame = await ReadFrameAsync();
                if (frame.Length > 0 && frame[0] == (byte)id)
                {
                    return frame;
                }
            }

            throw new TimeoutException("expected " + id);
        }

        private async Task ReadExactAsync(byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
                if (n == 0)
                {
                    throw new System.IO.EndOfStreamException();
                }

                read += n;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stream?.Dispose();
            _tcp?.Dispose();
            await Task.CompletedTask;
        }
    }
}
