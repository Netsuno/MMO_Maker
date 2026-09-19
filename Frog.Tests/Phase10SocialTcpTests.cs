using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Gameplay;
using Frog.Server.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10SocialTcpTests
{
    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_PartyInviteAcceptChatMuteReconnectAndBlock()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            var leaderUser = UniqueUser("ld");
            var memberUser = UniqueUser("mb");
            var blockedUser = UniqueUser("bk");
            var gmUser = UniqueUser("gm");

            await using var leader = new TcpProbe();
            await using var member = new TcpProbe();
            await using var blocked = new TcpProbe();
            await using var gm = new TcpProbe();

            var leaderId = await RegisterLoginSelectAsync(leader, port, leaderUser, password, "LdHero");
            var memberId = await RegisterLoginSelectAsync(member, port, memberUser, password, "MbHero");
            var blockedId = await RegisterLoginSelectAsync(blocked, port, blockedUser, password, "BkHero");
            await RegisterLoginSelectAsync(gm, port, gmUser, password, "GmHero");

            var accounts = host.Services.GetRequiredService<IAccountRepository>();
            var operators = host.Services.GetRequiredService<IOperatorDirectory>();
            var gmAccount = await accounts.FindByUsernameAsync(gmUser);
            Assert.Equal(OperatorGrantStatus.Granted, (await operators.GrantAsync(gmAccount!.Id, "sql", "p10-1")).Status);
            Assert.False(await operators.IsOperatorAsync((await accounts.FindByUsernameAsync(leaderUser))!.Id));

            var inviteReq = Guid.NewGuid();
            await leader.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Invite, inviteReq, memberId));
            var inviteResult = DecodeSocialResult(await leader.ReadUntilAsync(PacketId.SocialResult));
            Assert.True(inviteResult.Success);
            var inviteEvent = DecodeSocialEvent(await member.ReadUntilAsync(PacketId.SocialEvent));
            Assert.Equal(SocialEventType.InviteReceived, inviteEvent.Type);

            var acceptReq = Guid.NewGuid();
            await member.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Accept, acceptReq, inviteResult.SubjectId));
            Assert.True(DecodeSocialResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);

            await leader.SendFrameAsync(BuildChat(ChatChannel.Party, "party-hi"));
            var partyChat = await member.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(partyChat, out var pCh, out _, out var pMsg));
            Assert.Equal(ChatChannel.Party, pCh);
            Assert.Equal("party-hi", pMsg);

            await blocked.SendFrameAsync(BuildChat(ChatChannel.Party, "not-in-party"));
            var denied = await blocked.ReadUntilAsync(PacketId.Error);
            Assert.Equal("Vous n'etes pas membre de ce canal.", DecodeError(denied));

            await gm.SendFrameAsync(BuildModerate(ModerationAction.Mute, leaderUser, "spam"));
            Assert.True(DecodeStatus(await gm.ReadUntilAsync(PacketId.ModerateResult)).ok);
            await leader.DrainPendingAsync(TimeSpan.FromMilliseconds(80));
            await leader.SendFrameAsync(BuildChat(ChatChannel.Party, "muted-party"));
            Assert.Equal(ModerationMessages.Muted, DecodeError(await leader.ReadUntilAsync(PacketId.Error)));

            await gm.SendFrameAsync(BuildModerate(ModerationAction.Unmute, leaderUser, "ok"));
            Assert.True(DecodeStatus(await gm.ReadUntilAsync(PacketId.ModerateResult)).ok);

            await member.SendFrameAsync(BuildSocial(SocialKind.Block, (byte)BlockAction.Block, Guid.NewGuid(), blockedId));
            Assert.True(DecodeSocialResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);
            await blocked.SendFrameAsync(BuildChat(ChatChannel.Whisper, "nope", memberUser));
            Assert.Equal("Vous etes bloque.", DecodeError(await blocked.ReadUntilAsync(PacketId.Error)));

            await blocked.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Invite, Guid.NewGuid(), memberId));
            Assert.False(DecodeSocialResult(await blocked.ReadUntilAsync(PacketId.SocialResult)).Success);

            var createGuild = await leader.SendThenResult(
                BuildSocialUtf8(SocialKind.Guild, (byte)GuildAction.Create, Guid.NewGuid(), "Knights"));
            Assert.True(createGuild.Success);
            Assert.False(await operators.IsOperatorAsync((await accounts.FindByUsernameAsync(leaderUser))!.Id));

            await leader.SendFrameAsync(BuildSocial(SocialKind.Guild, (byte)GuildAction.Invite, Guid.NewGuid(), memberId));
            Assert.True(DecodeSocialResult(await leader.ReadUntilAsync(PacketId.SocialResult)).Success);
            var guildEvent = DecodeSocialEvent(await member.ReadUntilAsync(PacketId.SocialEvent));
            await member.SendFrameAsync(BuildSocial(SocialKind.Guild, (byte)GuildAction.Accept, Guid.NewGuid(), guildEvent.SubjectId));
            Assert.True(DecodeSocialResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);

            await leader.SendFrameAsync(BuildChat(ChatChannel.Guild, "guild-hi"));
            var guildChat = await member.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(guildChat, out var gCh, out _, out var gMsg));
            Assert.Equal(ChatChannel.Guild, gCh);
            Assert.Equal("guild-hi", gMsg);

            await leader.DisconnectAsync();
            await using var leader2 = new TcpProbe();
            await leader2.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await leader2.ReadFrameAsync())[0]);
            await leader2.SendFrameAsync(BuildLogin(leaderUser, password));
            Assert.True(DecodeStatus(await leader2.ReadUntilAsync(PacketId.LoginResult)).ok);
            await leader2.SendFrameAsync(BuildCharacterSelect(leaderId.ToString()));
            Assert.True(DecodeStatus(await leader2.ReadUntilAsync(PacketId.CharacterSelectResult)).ok);
            var snap = await ReadPartySnapshotAsync(leader2);
            Assert.Contains(snap.Members, m => m.CharacterId == memberId);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_ConcurrentPartyInvites_FirstAcceptWins()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var a = new TcpProbe();
            await using var b = new TcpProbe();
            await using var c = new TcpProbe();
            var aId = await RegisterLoginSelectAsync(a, port, UniqueUser("a"), password, "AHero");
            var bId = await RegisterLoginSelectAsync(b, port, UniqueUser("b"), password, "BHero");
            var cId = await RegisterLoginSelectAsync(c, port, UniqueUser("c"), password, "CHero");
            _ = aId;
            _ = bId;

            await a.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Invite, Guid.NewGuid(), cId));
            var aInv = DecodeSocialResult(await a.ReadUntilAsync(PacketId.SocialResult));
            await b.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Invite, Guid.NewGuid(), cId));
            var bInv = DecodeSocialResult(await b.ReadUntilAsync(PacketId.SocialResult));
            Assert.True(aInv.Success);
            Assert.True(bInv.Success);

            await c.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Accept, Guid.NewGuid(), aInv.SubjectId));
            Assert.True(DecodeSocialResult(await c.ReadUntilAsync(PacketId.SocialResult)).Success);
            await c.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Accept, Guid.NewGuid(), bInv.SubjectId));
            Assert.False(DecodeSocialResult(await c.ReadUntilAsync(PacketId.SocialResult)).Success);

            await c.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Accept, Guid.NewGuid(), aInv.SubjectId));
            var replay = DecodeSocialResult(await c.ReadUntilAsync(PacketId.SocialResult));
            Assert.False(replay.Success);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_PartyDissolvedOnProcessRestart()
    {
        var port = GetFreePort();
        Guid leaderId;
        Guid memberId;
        string leaderUser;
        string memberUser;
        const string password = "password123";
        using (var host = CreateInMemoryHost(port))
        {
            await host.StartAsync();
            try
            {
                leaderUser = UniqueUser("ld");
                memberUser = UniqueUser("mb");
                await using var leader = new TcpProbe();
                await using var member = new TcpProbe();
                leaderId = await RegisterLoginSelectAsync(leader, port, leaderUser, password, "LdHero");
                memberId = await RegisterLoginSelectAsync(member, port, memberUser, password, "MbHero");
                await leader.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Invite, Guid.NewGuid(), memberId));
                var inv = DecodeSocialResult(await leader.ReadUntilAsync(PacketId.SocialResult));
                await member.SendFrameAsync(BuildSocial(SocialKind.Party, (byte)PartyAction.Accept, Guid.NewGuid(), inv.SubjectId));
                Assert.True(DecodeSocialResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);
            }
            finally
            {
                await host.StopAsync();
            }
        }

        using var host2 = CreateInMemoryHost(port);
        await host2.StartAsync();
        try
        {
            await using var leader = new TcpProbe();
            leaderId = await RegisterLoginSelectAsync(leader, port, UniqueUser("ld2"), password, "LdHero2", drain: false);
            var snap = await ReadPartySnapshotAsync(leader);
            Assert.Empty(snap.Members);
            Assert.Equal(Guid.Empty, snap.SubjectId);
            _ = leaderId;
            _ = memberId;
            _ = leaderUser;
            _ = memberUser;
        }
        finally
        {
            await host2.StopAsync();
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
        string characterName,
        bool drain = true)
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
        if (drain)
        {
            await client.DrainPendingAsync();
        }

        return Guid.Parse(characterId);
    }

    private static async Task<SocialSnapshotWire> ReadPartySnapshotAsync(TcpProbe client)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            var frame = await client.ReadFrameAsync(deadline - DateTime.UtcNow);
            if (frame[0] != (byte)PacketId.SocialSnapshot)
            {
                continue;
            }

            Assert.True(SocialWire.TryParseSnapshot(frame.AsSpan(1), out var snap));
            if (snap.Kind == SocialKind.Party)
            {
                return snap;
            }
        }

        throw new TimeoutException("party snapshot not received");
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

    private static byte[] BuildCharacterSelect(string id)
    {
        var b = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildChat(ChatChannel channel, string message, string whisperTarget = "")
    {
        var m = Encoding.UTF8.GetBytes(message);
        if (channel == ChatChannel.Whisper)
        {
            var target = Encoding.UTF8.GetBytes(whisperTarget);
            var payload = new byte[1 + 1 + 1 + target.Length + sizeof(ushort) + m.Length];
            payload[0] = (byte)PacketId.ChatSend;
            payload[1] = (byte)channel;
            payload[2] = (byte)target.Length;
            target.CopyTo(payload, 3);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3 + target.Length), (ushort)m.Length);
            m.CopyTo(payload, 3 + target.Length + sizeof(ushort));
            return payload;
        }

        var plain = new byte[1 + 1 + sizeof(ushort) + m.Length];
        plain[0] = (byte)PacketId.ChatSend;
        plain[1] = (byte)channel;
        BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(2), (ushort)m.Length);
        m.CopyTo(plain, 2 + sizeof(ushort));
        return plain;
    }

    private static byte[] BuildModerate(ModerationAction action, string target, string reason)
    {
        var body = ModerateWire.BuildRequest(action, target, reason);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.ModerateRequest;
        body.CopyTo(payload, 1);
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

    private static byte[] BuildSocialUtf8(SocialKind kind, byte action, Guid requestId, string text)
    {
        var body = SocialWire.BuildRequest(kind, action, requestId, SocialWire.BuildUtf8Payload(text, 64));
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.SocialRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    private static SocialResultWire DecodeSocialResult(byte[] frame)
    {
        Assert.Equal((byte)PacketId.SocialResult, frame[0]);
        Assert.True(SocialWire.TryParseResult(frame.AsSpan(1), out var result));
        return result;
    }

    private static SocialEventWire DecodeSocialEvent(byte[] frame)
    {
        Assert.Equal((byte)PacketId.SocialEvent, frame[0]);
        Assert.True(SocialWire.TryParseEvent(frame.AsSpan(1), out var ev));
        return ev;
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

    private static bool TryDecodeChat(byte[] payload, out ChatChannel channel, out string from, out string message)
    {
        channel = default;
        from = message = string.Empty;
        if (payload.Length < 3 || payload[0] != (byte)PacketId.ChatMessage)
        {
            return false;
        }

        channel = (ChatChannel)payload[1];
        var o = 2;
        var fromLen = payload[o++];
        from = Encoding.UTF8.GetString(payload, o, fromLen);
        o += fromLen;
        var toLen = payload[o++];
        o += toLen;
        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(o));
        o += 2;
        message = Encoding.UTF8.GetString(payload, o, msgLen);
        return true;
    }

    private static string DecodeError(byte[] payload)
    {
        var len = payload[1];
        return Encoding.UTF8.GetString(payload, 2, len);
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
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var frame = new byte[4 + payload.Length];
                BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
                payload.CopyTo(frame, 4);
                await _stream!.WriteAsync(frame).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<SocialResultWire> SendThenResult(byte[] payload)
        {
            await SendFrameAsync(payload);
            return DecodeSocialResult(await ReadUntilAsync(PacketId.SocialResult));
        }

        public async Task<byte[]> ReadFrameAsync(TimeSpan? timeout = null)
        {
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf, cts.Token);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await ReadExactAsync(payload, cts.Token);
            return payload;
        }

        public async Task DrainPendingAsync(TimeSpan? budget = null)
        {
            var deadline = DateTime.UtcNow + (budget ?? TimeSpan.FromMilliseconds(250));
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                try
                {
                    _ = await ReadFrameAsync(remaining);
                }
                catch
                {
                    break;
                }
            }
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var frame = await ReadFrameAsync(remaining);
                if (frame[0] == (byte)id)
                {
                    return frame;
                }
            }

            throw new TimeoutException("expected packet not received: " + id);
        }

        public Task DisconnectAsync()
        {
            _tcp?.Close();
            return Task.CompletedTask;
        }

        private async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
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
