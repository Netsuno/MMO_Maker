using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class EconomyHubTcpTests
{
    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_QueryAuctionMailGuildBank_EmptyThenGuildSlots()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var client = new TcpProbe();
            var characterId = await RegisterLoginSelectAsync(client, port, UniqueUser("ec"), password, "EcHero");
            _ = characterId;

            await client.SendFrameAsync(BuildEconomy(EconomyHubKind.Auction, (byte)EconomyHubAction.Query, Guid.NewGuid()));
            var auctionResult = DecodeResult(await client.ReadUntilAsync(PacketId.EconomyHubResult));
            Assert.True(auctionResult.Success);
            var auctionSnap = DecodeSnapshot(await client.ReadUntilAsync(PacketId.EconomyHubSnapshot));
            Assert.Equal(EconomyHubKind.Auction, auctionSnap.Kind);
            Assert.Empty(auctionSnap.Entries);

            await client.SendFrameAsync(BuildEconomy(EconomyHubKind.Mail, (byte)EconomyHubAction.Query, Guid.NewGuid()));
            Assert.True(DecodeResult(await client.ReadUntilAsync(PacketId.EconomyHubResult)).Success);
            var mailSnap = DecodeSnapshot(await client.ReadUntilAsync(PacketId.EconomyHubSnapshot));
            Assert.Empty(mailSnap.Entries);

            await client.SendFrameAsync(BuildEconomy(EconomyHubKind.GuildBank, (byte)EconomyHubAction.Query, Guid.NewGuid()));
            var noGuild = DecodeResult(await client.ReadUntilAsync(PacketId.EconomyHubResult));
            Assert.True(noGuild.Success);
            Assert.Contains("guilde", noGuild.Message, StringComparison.OrdinalIgnoreCase);
            var emptyBank = DecodeSnapshot(await client.ReadUntilAsync(PacketId.EconomyHubSnapshot));
            Assert.Empty(emptyBank.Entries);

            var create = SocialWire.BuildRequest(
                SocialKind.Guild,
                (byte)GuildAction.Create,
                Guid.NewGuid(),
                SocialWire.BuildUtf8Payload("Bankers", SocialProtocolLimits.MaxGuildNameUtf8Bytes));
            var createFrame = new byte[1 + create.Length];
            createFrame[0] = (byte)PacketId.SocialRequest;
            create.CopyTo(createFrame.AsSpan(1));
            await client.SendFrameAsync(createFrame);
            var created = DecodeSocialResult(await client.ReadUntilAsync(PacketId.SocialResult));
            Assert.True(created.Success);

            await client.SendFrameAsync(BuildEconomy(EconomyHubKind.GuildBank, (byte)EconomyHubAction.Query, Guid.NewGuid()));
            var bankResult = DecodeResult(await client.ReadUntilAsync(PacketId.EconomyHubResult));
            Assert.True(bankResult.Success);
            var bankSnap = DecodeSnapshot(await client.ReadUntilAsync(PacketId.EconomyHubSnapshot));
            Assert.Equal(created.SubjectId, bankSnap.SubjectId);
            Assert.Equal(EconomyHubLimits.GuildBankSlotCount, bankSnap.Entries.Count);

            await client.SendFrameAsync(BuildEconomy(EconomyHubKind.Auction, 9, Guid.NewGuid()));
            var unknown = DecodeResult(await client.ReadUntilAsync(PacketId.EconomyHubResult));
            Assert.False(unknown.Success);
            Assert.Equal("Action inconnue.", unknown.Message);
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

    private static async Task<Guid> RegisterLoginSelectAsync(
        TcpProbe client,
        int port,
        string user,
        string password,
        string characterName)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(BuildLogin(user, password, PacketId.RegisterRequest));
        Assert.True(DecodeStatus(await client.ReadUntilAsync(PacketId.RegisterResult)).ok);
        await client.SendFrameAsync(BuildLogin(user, password));
        Assert.True(DecodeStatus(await client.ReadUntilAsync(PacketId.LoginResult)).ok);
        await client.SendFrameAsync(BuildCharacterCreate(characterName, Phase7ContentSeed.DefaultClassId));
        var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
        Assert.True(create.Length > 3 && create[1] != 0);
        var characterId = Encoding.UTF8.GetString(create, 3, create[2]);
        await client.SendFrameAsync(BuildCharacterSelect(characterId));
        Assert.True(DecodeStatus(await client.ReadUntilAsync(PacketId.CharacterSelectResult)).ok);
        await client.DrainPendingAsync();
        return Guid.Parse(characterId);
    }

    private static string UniqueUser(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

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

    private static byte[] BuildCharacterCreate(string name, Guid classId)
    {
        var n = Encoding.UTF8.GetBytes(name);
        var payload = new byte[1 + 1 + n.Length + 16];
        payload[0] = (byte)PacketId.CharacterCreateRequest;
        payload[1] = (byte)n.Length;
        n.CopyTo(payload, 2);
        classId.TryWriteBytes(payload.AsSpan(2 + n.Length));
        return payload;
    }

    private static byte[] BuildCharacterSelect(string characterId)
    {
        var b = Encoding.UTF8.GetBytes(characterId);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildEconomy(EconomyHubKind kind, byte action, Guid requestId)
    {
        var body = EconomyHubWire.BuildRequest(kind, action, requestId, ReadOnlySpan<byte>.Empty);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.EconomyHubRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    private static EconomyHubResultWire DecodeResult(byte[] frame)
    {
        Assert.Equal((byte)PacketId.EconomyHubResult, frame[0]);
        Assert.True(EconomyHubWire.TryParseResult(frame.AsSpan(1), out var result));
        return result;
    }

    private static EconomyHubSnapshotWire DecodeSnapshot(byte[] frame)
    {
        Assert.Equal((byte)PacketId.EconomyHubSnapshot, frame[0]);
        Assert.True(EconomyHubWire.TryParseSnapshot(frame.AsSpan(1), out var snap));
        return snap;
    }

    private static SocialResultWire DecodeSocialResult(byte[] frame)
    {
        Assert.Equal((byte)PacketId.SocialResult, frame[0]);
        Assert.True(SocialWire.TryParseResult(frame.AsSpan(1), out var result));
        return result;
    }

    private static (bool ok, string message) DecodeStatus(byte[] payload)
    {
        var ok = payload.Length > 1 && payload[1] != 0;
        if (payload.Length < 3)
        {
            return (ok, string.Empty);
        }

        var len = payload[2];
        var message = payload.Length >= 3 + len ? Encoding.UTF8.GetString(payload, 3, len) : string.Empty;
        return (ok, message);
    }

    private sealed class TcpProbe : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
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
            await _sendLock.WaitAsync();
            try
            {
                var frame = new byte[4 + payload.Length];
                BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
                payload.CopyTo(frame, 4);
                await _stream!.WriteAsync(frame);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(8));
            var header = new byte[4];
            await ReadExactAsync(header, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(header);
            Assert.True(len > 0 && len < 1_000_000);
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

        public async Task DrainPendingAsync(TimeSpan? window = null)
        {
            var until = DateTime.UtcNow + (window ?? TimeSpan.FromMilliseconds(250));
            while (DateTime.UtcNow < until)
            {
                var remain = until - DateTime.UtcNow;
                if (remain <= TimeSpan.Zero)
                {
                    break;
                }

                try
                {
                    _ = await ReadFrameAsync(remain);
                }
                catch (Exception)
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
                var n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
                if (n == 0)
                {
                    throw new System.IO.EndOfStreamException();
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
