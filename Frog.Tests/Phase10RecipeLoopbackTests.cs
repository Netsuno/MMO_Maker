using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// P10-4: automatable 12-step recipe on one loopback host. Steps that require
/// two physical machines are named, not faked.
/// </summary>
public sealed class Phase10RecipeLoopbackTests
{
    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Loopback_TwoClients_CoverAutomatableRecipeSteps()
    {
        var port = GetFreePort();
        var kv = new Dictionary<string, string?>
        {
            ["Server:Port"] = port.ToString(),
            ["Server:BindAddress"] = "127.0.0.1",
            ["MariaDb:Enabled"] = "false",
            ["PostgreSql:AllowInMemoryFallback"] = "true",
        };
        using var host = FrogServerHostFactory
            .CreateHostBuilder(configureServices: services =>
            {
                services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(8));
            })
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(kv))
            .Build();
        await host.StartAsync();
        try
        {
            await using var a = new RecipeTcp();
            await using var b = new RecipeTcp();
            await a.ConnectAsync(port);
            await b.ConnectAsync(port);
            Assert.True(LoadPacketsHello(await a.ReadUntilAsync(PacketId.Hello)));
            Assert.True(LoadPacketsHello(await b.ReadUntilAsync(PacketId.Hello)));

            // Steps 3–4 (loopback): login + create/select. Step 1 zip install and
            // step 2 second physical PC are NOT covered here.
            var stamp = Guid.NewGuid().ToString("N")[..8];
            await RegisterLoginSelectAsync(a, "ra" + stamp, "RaHero");
            await RegisterLoginSelectAsync(b, "rb" + stamp, "RbHero");

            // Step 5 presence / step 6 move (same process, not WAN).
            await a.SendFrameAsync([(byte)PacketId.MoveRequest, 1, 0]);
            var pos = await a.ReadUntilAsync(PacketId.PositionUpdate);
            Assert.Equal((byte)PacketId.PositionUpdate, pos[0]);

            // Step 7 social/chat isolation is proven in Phase10SocialTcpTests;
            // here: global chat decoded by the second session.
            await a.SendFrameAsync(BuildChat("recipe-hi"));
            var chat = await b.ReadUntilAsync(PacketId.ChatMessage);
            Assert.Equal((byte)PacketId.ChatMessage, chat[0]);

            // Step 8 trade invite (full commit = Phase10TradeTcpTests).
            // Step 9 editor publish from delivered zip = 2 PCs / P10-3.
            // Step 10 restore = Phase10BackupRestoreRowsTests.
            // Step 11 sanctions = Phase10ClosedBeta / social mute tests.
            // Step 12 30–60 min two machines = not this test.

            await a.SendFrameAsync([(byte)PacketId.HeartbeatRequest]);
            Assert.Equal((byte)PacketId.HeartbeatAck, (await a.ReadUntilAsync(PacketId.HeartbeatAck))[0]);
        }
        finally
        {
            await host.StopAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void RecipeMatrix_NamesTwoPhysicalMachines()
    {
        var plan = System.IO.File.ReadAllText(System.IO.Path.Combine(
            RepoRoot(), "docs", "progress", "phase-10-beta-release", "guides", "BETA_TEST_PLAN.md"));
        Assert.Contains("Exige 2 machines physiques", plan, StringComparison.Ordinal);
        Assert.Contains("Deux machines physiques sont obligatoires", plan, StringComparison.Ordinal);
        Assert.Contains("étapes 2,", plan, StringComparison.Ordinal);
    }

    private static async Task RegisterLoginSelectAsync(RecipeTcp tcp, string user, string hero)
    {
        const string password = "password123";
        await tcp.SendFrameAsync(BuildLogin(PacketId.RegisterRequest, user, password));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.RegisterResult))[1]);
        await tcp.SendFrameAsync(BuildLogin(PacketId.LoginRequest, user, password));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.LoginResult))[1]);
        await tcp.SendFrameAsync(BuildCreate(hero));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        Assert.NotEqual(0, create[1]);
        var id = Encoding.UTF8.GetString(create.AsSpan(3, create[2]));
        await tcp.SendFrameAsync(BuildSelect(id));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
        await tcp.DrainAsync(TimeSpan.FromMilliseconds(200));
    }

    private static bool LoadPacketsHello(byte[] frame)
        => frame.Length > 0 && frame[0] == (byte)PacketId.Hello && WireHello.TryParse(frame, out _, out _);

    private static byte[] BuildLogin(PacketId id, string user, string pass)
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

    private static byte[] BuildCreate(string name)
    {
        var n = Encoding.UTF8.GetBytes(name);
        var payload = new byte[1 + 1 + n.Length + 16];
        payload[0] = (byte)PacketId.CharacterCreateRequest;
        payload[1] = (byte)n.Length;
        n.CopyTo(payload, 2);
        Frog.Server.Gameplay.Phase7ContentSeed.DefaultClassId.TryWriteBytes(payload.AsSpan(2 + n.Length));
        return payload;
    }

    private static byte[] BuildSelect(string id)
    {
        var b = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildChat(string message)
    {
        var m = Encoding.UTF8.GetBytes(message);
        var payload = new byte[1 + 1 + sizeof(ushort) + m.Length];
        payload[0] = (byte)PacketId.ChatSend;
        payload[1] = (byte)ChatChannel.Global;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), (ushort)m.Length);
        m.CopyTo(payload, 2 + sizeof(ushort));
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

    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found");
    }

    private sealed class RecipeTcp : IAsyncDisposable
    {
        private TcpClient? _tcp;
        private NetworkStream? _stream;

        public async Task ConnectAsync(int port)
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync(IPAddress.Loopback, port);
            _stream = _tcp.GetStream();
        }

        public async Task SendFrameAsync(byte[] payload)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await _stream!.WriteAsync(frame);
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan timeout)
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await ReadExactAsync(payload, cts.Token);
            return payload;
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(12);
            while (DateTime.UtcNow < deadline)
            {
                var frame = await ReadFrameAsync(deadline - DateTime.UtcNow);
                if (frame.Length > 0 && frame[0] == (byte)id)
                {
                    return frame;
                }
            }

            throw new TimeoutException("missing " + id);
        }

        public async Task DrainAsync(TimeSpan budget)
        {
            var deadline = DateTime.UtcNow + budget;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    _ = await ReadFrameAsync(deadline - DateTime.UtcNow);
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task ReadExactAsync(byte[] buffer, System.Threading.CancellationToken ct)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await _stream!.ReadAsync(buffer.AsMemory(read), ct);
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
