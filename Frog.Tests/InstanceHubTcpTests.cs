using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Enums;
using Frog.Core.Instances;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class InstanceHubTcpTests
{
    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_EnterLeave_PartyGateAndSharedInstance()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var leader = new TcpProbe();
            await using var member = new TcpProbe();
            var leaderId = await RegisterLoginSelectAsync(leader, port, UniqueUser("il"), password, "IlHero");
            var memberId = await RegisterLoginSelectAsync(member, port, UniqueUser("im"), password, "ImHero");

            await leader.SendFrameAsync(BuildInstance(
                InstanceHubKind.Dungeon, (byte)InstanceHubAction.Query, Guid.NewGuid()));
            Assert.True(DecodeResult(await leader.ReadUntilAsync(PacketId.InstanceHubResult)).Success);
            var catalog = DecodeSnapshot(await leader.ReadUntilAsync(PacketId.InstanceHubSnapshot));
            Assert.Contains(catalog.Entries, e => e.EntryId == DungeonCatalog.MarshRuins.Id);

            await leader.SendFrameAsync(BuildInstance(
                InstanceHubKind.Dungeon,
                (byte)InstanceHubAction.Enter,
                Guid.NewGuid(),
                InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.MarshRuins.Id)));
            var noParty = DecodeResult(await leader.ReadUntilAsync(PacketId.InstanceHubResult));
            Assert.False(noParty.Success);
            Assert.Equal("Groupe requis.", noParty.Message);

            await leader.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Invite, Guid.NewGuid(), memberId));
            var invite = DecodeSocialResult(await leader.ReadUntilAsync(PacketId.SocialResult));
            Assert.True(invite.Success);

            await leader.SendFrameAsync(BuildInstance(
                InstanceHubKind.Dungeon,
                (byte)InstanceHubAction.Enter,
                Guid.NewGuid(),
                InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.MarshRuins.Id)));
            var created = DecodeResult(await leader.ReadUntilAsync(PacketId.InstanceHubResult));
            Assert.True(created.Success);
            Assert.Equal("Instance creee.", created.Message);
            Assert.NotEqual(Guid.Empty, created.SubjectId);
            var createdSnap = DecodeSnapshot(await leader.ReadUntilAsync(PacketId.InstanceHubSnapshot));
            Assert.Equal(created.SubjectId, createdSnap.SubjectId);
            Assert.Contains(createdSnap.Entries, e => e.RelatedId == created.SubjectId && e.Quantity == 1);

            await member.SendFrameAsync(BuildInstance(
                InstanceHubKind.Dungeon,
                (byte)InstanceHubAction.Enter,
                Guid.NewGuid(),
                InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.MarshRuins.Id)));
            var memberNoParty = DecodeResult(await member.ReadUntilAsync(PacketId.InstanceHubResult));
            Assert.False(memberNoParty.Success);
            Assert.Equal("Groupe requis.", memberNoParty.Message);

            await member.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Accept, Guid.NewGuid(), invite.SubjectId));
            Assert.True(DecodeSocialResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);

            await member.SendFrameAsync(BuildInstance(
                InstanceHubKind.Dungeon,
                (byte)InstanceHubAction.Enter,
                Guid.NewGuid(),
                InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.MarshRuins.Id)));
            var joined = DecodeResult(await member.ReadUntilAsync(PacketId.InstanceHubResult));
            Assert.True(joined.Success);
            Assert.Equal("Instance rejointe.", joined.Message);
            Assert.Equal(created.SubjectId, joined.SubjectId);
            var joinedSnap = DecodeSnapshot(await member.ReadUntilAsync(PacketId.InstanceHubSnapshot));
            Assert.Contains(joinedSnap.Entries, e => e.RelatedId == created.SubjectId && e.Quantity == 2);

            await member.SendFrameAsync(BuildInstance(
                InstanceHubKind.Dungeon, (byte)InstanceHubAction.Leave, Guid.NewGuid()));
            var memberLeft = DecodeResult(await member.ReadUntilAsync(PacketId.InstanceHubResult));
            Assert.True(memberLeft.Success);
            Assert.Equal("Retour overworld.", memberLeft.Message);
            _ = DecodeSnapshot(await member.ReadUntilAsync(PacketId.InstanceHubSnapshot));

            await leader.SendFrameAsync(BuildInstance(
                InstanceHubKind.Dungeon, (byte)InstanceHubAction.Leave, Guid.NewGuid()));
            var leaderLeft = DecodeResult(await leader.ReadUntilAsync(PacketId.InstanceHubResult));
            Assert.True(leaderLeft.Success);
            var leftSnap = DecodeSnapshot(await leader.ReadUntilAsync(PacketId.InstanceHubSnapshot));
            Assert.Equal(Guid.Empty, leftSnap.SubjectId);
            Assert.All(leftSnap.Entries, e => Assert.Equal(Guid.Empty, e.RelatedId));

            await leader.SendFrameAsync(BuildInstance(
                InstanceHubKind.Raid,
                (byte)InstanceHubAction.Enter,
                Guid.NewGuid(),
                InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.KingCrypt.Id)));
            var raid = DecodeResult(await leader.ReadUntilAsync(PacketId.InstanceHubResult));
            Assert.True(raid.Success);
            Assert.Equal("Instance creee.", raid.Message);
            _ = DecodeSnapshot(await leader.ReadUntilAsync(PacketId.InstanceHubSnapshot));
            _ = leaderId;
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

    private static byte[] BuildInstance(InstanceHubKind kind, byte action, Guid requestId, byte[]? extra = null)
    {
        var body = InstanceHubWire.BuildRequest(kind, action, requestId, extra ?? Array.Empty<byte>());
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.InstanceHubRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    private static byte[] BuildSocial(SocialKind kind, byte action, Guid requestId, Guid target)
    {
        var body = SocialWire.BuildRequest(kind, action, requestId, SocialWire.BuildGuidPayload(target));
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.SocialRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    private static InstanceHubResultWire DecodeResult(byte[] frame)
    {
        Assert.Equal((byte)PacketId.InstanceHubResult, frame[0]);
        Assert.True(InstanceHubWire.TryParseResult(frame.AsSpan(1), out var result));
        return result;
    }

    private static InstanceHubSnapshotWire DecodeSnapshot(byte[] frame)
    {
        Assert.Equal((byte)PacketId.InstanceHubSnapshot, frame[0]);
        Assert.True(InstanceHubWire.TryParseSnapshot(frame.AsSpan(1), out var snap));
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
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(8));
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

        private async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
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
