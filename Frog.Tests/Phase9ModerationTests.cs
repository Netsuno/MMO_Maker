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
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Database;
using Frog.Server.Network;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Frog.Tests;

public sealed class Phase9ModerationTests
{
    [Fact]
    public void PacketId_HasModerationOpcodes_ButNoGrantRevoke()
    {
        var names = Enum.GetNames<PacketId>();
        Assert.Contains("ModerateRequest", names);
        Assert.Contains("ModerateResult", names);
        Assert.Equal(78, (byte)PacketId.ModerateRequest);
        Assert.Equal(79, (byte)PacketId.ModerateResult);
        Assert.Equal(PacketIds.ModerateRequest, (byte)PacketId.ModerateRequest);
        Assert.Equal(PacketIds.ModerateResult, (byte)PacketId.ModerateResult);
        Assert.DoesNotContain("GrantOperator", names);
        Assert.DoesNotContain("RevokeOperator", names);
        Assert.DoesNotContain("AdminCommand", names);
        Assert.DoesNotContain("MuteRequest", names);
        Assert.DoesNotContain("KickRequest", names);
        Assert.DoesNotContain("BanRequest", names);
    }

    [Fact]
    public void ModerateWire_RoundTripAndSlashCommands()
    {
        var body = ModerateWire.BuildRequest(ModerationAction.Mute, "alice", "spam");
        Assert.True(ModerateWire.TryParseRequest(body, out var action, out var target, out var reason));
        Assert.Equal(ModerationAction.Mute, action);
        Assert.Equal("alice", target);
        Assert.Equal("spam", reason);

        Assert.True(ModerateWire.TryParseSlashCommand("/ban Bob harassment in global", out action, out target, out reason));
        Assert.Equal(ModerationAction.Ban, action);
        Assert.Equal("Bob", target);
        Assert.Equal("harassment in global", reason);
        Assert.True(ModerateWire.TryParseSlashCommand("/kick x", out action, out target, out reason));
        Assert.Equal(ModerationAction.Kick, action);
        Assert.Equal("x", target);
        Assert.Equal(string.Empty, reason);
        Assert.False(ModerateWire.TryParseSlashCommand("hello world", out _, out _, out _));
    }

    [Fact]
    public async Task SanctionStore_MuteAndBanPersist_UnmuteUnbanClear()
    {
        var store = new InMemoryAccountSanctionStore();
        var account = Guid.NewGuid();
        var actor = Guid.NewGuid();
        Assert.False(await store.HasActiveMuteAsync(account));
        await store.ApplyAsync(account, SanctionKinds.Mute, actor, "spam");
        Assert.True(await store.HasActiveMuteAsync(account));
        Assert.False(await store.HasActiveBanAsync(account));
        Assert.True(await store.RevokeAsync(account, SanctionKinds.Mute, actor, "ok"));
        Assert.False(await store.HasActiveMuteAsync(account));

        await store.ApplyAsync(account, SanctionKinds.Ban, actor, "cheat");
        Assert.True(await store.HasActiveBanAsync(account));
        Assert.True(await store.RevokeAsync(account, SanctionKinds.Ban, actor, "appeal"));
        Assert.False(await store.HasActiveBanAsync(account));

        var events = await store.ListEventsForTargetAsync(account);
        Assert.Contains(events, e => e.Action == ModerationEventActions.Mute);
        Assert.Contains(events, e => e.Action == ModerationEventActions.Unmute);
        Assert.Contains(events, e => e.Action == ModerationEventActions.Ban);
        Assert.Contains(events, e => e.Action == ModerationEventActions.Unban);
    }

    [Fact]
    public async Task ModerationService_UnprivilegedCannotMute_OperatorMuteKickBanPersist()
    {
        var accounts = new InMemoryAccountRepository();
        var operators = new InMemoryOperatorDirectory(accounts);
        var sanctions = new InMemoryAccountSanctionStore();
        var sessions = new InMemoryAuthSessionRepository();
        var connections = new ConnectionManager();
        var clients = new ClientRegistry();
        var sender = new PacketSender(NullLogger<PacketSender>.Instance);
        var svc = new ModerationService(
            operators,
            accounts,
            sanctions,
            sessions,
            connections,
            clients,
            sender,
            NullLogger<ModerationService>.Instance);

        var player = await accounts.TryCreateAsync("p9player", "password123");
        var gm = await accounts.TryCreateAsync("p9gmop", "password123");
        Assert.Equal(AccountCreateStatus.Created, player.Status);
        Assert.Equal(AccountCreateStatus.Created, gm.Status);

        var denied = await svc.ExecuteAsync(player.AccountId!.Value, ModerationAction.Mute, "p9gmop", "nope");
        Assert.False(denied.Success);
        Assert.Equal(ModerationMessages.NotOperator, denied.Message);
        Assert.False(await sanctions.HasActiveMuteAsync(gm.AccountId!.Value));

        var granted = await operators.GrantAsync(gm.AccountId!.Value, "sql", "p9-1");
        Assert.Equal(OperatorGrantStatus.Granted, granted.Status);

        var muted = await svc.ExecuteAsync(gm.AccountId.Value, ModerationAction.Mute, "p9player", "spam");
        Assert.True(muted.Success);
        Assert.True(await sanctions.HasActiveMuteAsync(player.AccountId.Value));

        var kicked = await svc.ExecuteAsync(gm.AccountId.Value, ModerationAction.Kick, "p9player", "afk");
        Assert.True(kicked.Success);
        Assert.Contains(
            await sanctions.ListEventsForTargetAsync(player.AccountId.Value),
            e => e.Action == ModerationEventActions.Kick);
        Assert.False(await sanctions.HasActiveBanAsync(player.AccountId.Value));

        var issued = await sessions.IssueAsync(player.AccountId.Value, TimeSpan.FromHours(12));
        Assert.Equal(AuthSessionIssueStatus.Issued, issued.Status);
        var banned = await svc.ExecuteAsync(gm.AccountId.Value, ModerationAction.Ban, "p9player", "cheat");
        Assert.True(banned.Success);
        Assert.True(await sanctions.HasActiveBanAsync(player.AccountId.Value));
        var still = await sessions.ValidateTokenAsync(issued.Token!);
        Assert.NotEqual(AuthSessionValidationStatus.Valid, still.Status);
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_UnprivilegedCannotModerate_MuteKickBanEnforced_ChatRegression()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            var gm = UniqueUser("gm");
            var player = UniqueUser("pl");
            var other = UniqueUser("ot");
            const string password = "password123";

            await using var gmClient = new TcpProbe();
            await using var playerClient = new TcpProbe();
            await using var otherClient = new TcpProbe();

            await RegisterAndLoginAsync(gmClient, port, gm, password);
            var playerLogin = await RegisterAndLoginAsync(playerClient, port, player, password);
            await RegisterAndLoginAsync(otherClient, port, other, password);

            await gmClient.SendFrameAsync(BuildModerate(ModerationAction.Mute, player, "nope"));
            var unpriv = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
            Assert.True(TryDecodeStatus(unpriv, out var unprivOk, out var unprivMsg));
            Assert.False(unprivOk);
            Assert.Equal(ModerationMessages.NotOperator, unprivMsg);

            var accounts = host.Services.GetRequiredService<IAccountRepository>();
            var operators = host.Services.GetRequiredService<IOperatorDirectory>();
            var gmAccount = await accounts.FindByUsernameAsync(gm);
            Assert.NotNull(gmAccount);
            Assert.Equal(
                OperatorGrantStatus.Granted,
                (await operators.GrantAsync(gmAccount!.Id, "sql", "p9-1 tcp")).Status);

            await otherClient.SendFrameAsync(BuildChat(ChatChannel.Global, "hello-global"));
            var global = await playerClient.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(global, out var gCh, out _, out var gMsg));
            Assert.Equal(ChatChannel.Global, gCh);
            Assert.Equal("hello-global", gMsg);

            await otherClient.SendFrameAsync(BuildChat(ChatChannel.Map, "hello-map"));
            var map = await playerClient.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(map, out var mCh, out _, out var mMsg));
            Assert.Equal(ChatChannel.Map, mCh);
            Assert.Equal("hello-map", mMsg);

            await otherClient.SendFrameAsync(BuildChat(ChatChannel.Whisper, "psst", player));
            var whisper = await playerClient.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(whisper, out var wCh, out _, out var wMsg));
            Assert.Equal(ChatChannel.Whisper, wCh);
            Assert.Equal("psst", wMsg);

            await gmClient.SendFrameAsync(BuildModerate(ModerationAction.Mute, player, "spam"));
            var muteResult = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
            Assert.True(TryDecodeStatus(muteResult, out var muteOk, out _));
            Assert.True(muteOk);

            await playerClient.DrainPendingAsync(TimeSpan.FromMilliseconds(80));
            await playerClient.SendFrameAsync(BuildChat(ChatChannel.Global, "should-block"));
            var mutedErr = await playerClient.ReadUntilAsync(PacketId.Error);
            Assert.Equal(ModerationMessages.Muted, DecodeError(mutedErr));
            await otherClient.DrainPendingAsync(TimeSpan.FromMilliseconds(120));

            await playerClient.SendFrameAsync([(byte)PacketId.HeartbeatRequest]);
            _ = await playerClient.ReadUntilAsync(PacketId.HeartbeatAck);

            await otherClient.SendFrameAsync(BuildChat(ChatChannel.Global, "still-open"));
            var afterMute = await playerClient.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(afterMute, out _, out _, out var afterMsg));
            Assert.Equal("still-open", afterMsg);

            await gmClient.SendFrameAsync(BuildModerate(ModerationAction.Kick, player, "afk"));
            var kickResult = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
            Assert.True(TryDecodeStatus(kickResult, out var kickOk, out _));
            Assert.True(kickOk);
            await ExpectSessionClosedAsync(playerClient);
            var connections = host.Services.GetRequiredService<ConnectionManager>();
            Assert.False(connections.TryGetSessionByUsername(player, out _));

            await using var player2 = new TcpProbe();
            await player2.ConnectAsync("127.0.0.1", port);
            _ = await player2.ReadFrameAsync();
            await player2.SendFrameAsync(BuildLogin(player, password));
            var relogin = await player2.ReadUntilAsync(PacketId.LoginResult);
            Assert.True(TryDecodeStatus(relogin, out var reloginOk, out _));
            Assert.True(reloginOk);
            await player2.DrainPendingAsync();

            await gmClient.SendFrameAsync(BuildModerate(ModerationAction.Ban, player, "cheat"));
            var banResult = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
            Assert.True(TryDecodeStatus(banResult, out var banOk, out _));
            Assert.True(banOk);
            await ExpectSessionClosedAsync(player2);

            await using var bannedLogin = new TcpProbe();
            await bannedLogin.ConnectAsync("127.0.0.1", port);
            _ = await bannedLogin.ReadFrameAsync();
            await bannedLogin.SendFrameAsync(BuildLogin(player, password));
            var banned = await bannedLogin.ReadUntilAsync(PacketId.LoginResult);
            Assert.True(TryDecodeStatus(banned, out var bannedOk, out var bannedMsg));
            Assert.False(bannedOk);
            Assert.Equal(ModerationMessages.Banned, bannedMsg);

            await using var bannedReconnect = new TcpProbe();
            await bannedReconnect.ConnectAsync("127.0.0.1", port);
            _ = await bannedReconnect.ReadFrameAsync();
            await bannedReconnect.SendFrameAsync(BuildReconnect(playerLogin.Token));
            var reconnect = await bannedReconnect.ReadUntilAsync(PacketId.ReconnectResult);
            Assert.True(TryDecodeStatus(reconnect, out var rcOk, out _));
            Assert.False(rcOk);
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

    private static async Task<(string Token, byte[] LoginFrame)> RegisterAndLoginAsync(
        TcpProbe client,
        int port,
        string user,
        string password)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(BuildRegister(user, password));
        var reg = await client.ReadUntilAsync(PacketId.RegisterResult);
        Assert.True(TryDecodeStatus(reg, out var regOk, out _));
        Assert.True(regOk);
        await client.SendFrameAsync(BuildLogin(user, password));
        var login = await client.ReadUntilAsync(PacketId.LoginResult);
        Assert.True(TryDecodeStatus(login, out var loginOk, out var token));
        Assert.True(loginOk);
        await client.DrainPendingAsync();
        return (token, login);
    }

    private static async Task ExpectSessionClosedAsync(TcpProbe client)
    {
        try
        {
            var frame = await client.ReadFrameAsync(TimeSpan.FromSeconds(3));
            if (frame.Length > 0 && frame[0] == (byte)PacketId.Error)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => client.ReadFrameAsync(TimeSpan.FromSeconds(3)));
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or TimeoutException or SocketException)
        {
        }
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

    private static byte[] BuildRegister(string user, string pass) => BuildLogin(user, pass, PacketId.RegisterRequest);

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

    private static byte[] BuildReconnect(string token)
    {
        var t = Encoding.UTF8.GetBytes(token);
        var payload = new byte[1 + 2 + t.Length];
        payload[0] = (byte)PacketId.ReconnectRequest;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)t.Length);
        t.CopyTo(payload, 3);
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
            var deadline = DateTime.UtcNow + (budget ?? TimeSpan.FromMilliseconds(200));
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
