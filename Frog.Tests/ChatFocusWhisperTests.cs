using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Chat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Core.Social;
using Frog.Server;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

/// <summary>Focus chat + chuchotement. Hello 11, tuile TileAsset 48, opcodes sociaux inchangés.</summary>
public sealed class ChatFocusWhisperTests
{
    [Fact]
    public void Protocol_Stays11_AndTileAssetStays48()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        var protocol = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        var chat = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Chat", "ChatCompose.cs"));
        Assert.DoesNotContain("PacketId", chat, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_LocksMovement_EnterFocuses_EscapeAndWorldClickRelease()
    {
        var idle = ChatCompose.Decide(false, false, ChatCompose.Key.World);
        Assert.False(idle.BlockWorldInput);
        Assert.False(idle.FocusChat);

        var focus = ChatCompose.Decide(false, false, ChatCompose.Key.Enter);
        Assert.True(focus.FocusChat);
        Assert.False(focus.Send);
        Assert.True(focus.BlockWorldInput);

        var typing = ChatCompose.Decide(true, false, ChatCompose.Key.World);
        Assert.True(typing.BlockWorldInput);
        Assert.False(typing.Send);
        Assert.False(typing.ReleaseFocus);

        var letter = ChatCompose.Decide(true, false, ChatCompose.Key.Text);
        Assert.True(letter.BlockWorldInput);
        Assert.False(letter.Send);

        var send = ChatCompose.Decide(true, false, ChatCompose.Key.Enter);
        Assert.True(send.Send);
        Assert.True(send.FocusChat);
        Assert.False(send.ReleaseFocus);

        var release = ChatCompose.Decide(true, false, ChatCompose.Key.Escape);
        Assert.True(release.ReleaseFocus);
        Assert.False(release.Send);

        var other = ChatCompose.Decide(false, true, ChatCompose.Key.Enter);
        Assert.True(other.BlockWorldInput);
        Assert.False(other.FocusChat);
        Assert.False(other.Send);

        var click = ChatCompose.OnWorldClick(true);
        Assert.True(click.ReleaseFocus);
        Assert.False(ChatCompose.OnWorldClick(false).ReleaseFocus);
    }

    [Fact]
    public void Whisper_ResolvesNameFriendOrTarget_AndFrenchFeedback()
    {
        Assert.Equal(ChatWhisper.Slash.Ready, ChatWhisper.ParseSlash("/w Aline bonjour", out var target, out var body));
        Assert.Equal("Aline", target);
        Assert.Equal("bonjour", body);
        Assert.Equal(ChatWhisper.Slash.Ready, ChatWhisper.ParseSlash("/chuchoter Bob salut", out target, out body));
        Assert.Equal("Bob", target);
        Assert.Equal("salut", body);
        Assert.Equal(ChatWhisper.Slash.Incomplete, ChatWhisper.ParseSlash("/w Aline", out _, out _));
        Assert.Equal(ChatWhisper.Slash.None, ChatWhisper.ParseSlash("bonjour tout le monde", out _, out _));

        Assert.True(ChatWhisper.TryResolveTarget("  Aline ", null, "Mannequin", "Mannequin", out var resolved));
        Assert.Equal("Aline", resolved);
        Assert.True(ChatWhisper.TryResolveTarget("", "Bruno", "Slime", "Mannequin", out resolved));
        Assert.Equal("Bruno", resolved);
        Assert.True(ChatWhisper.TryResolveTarget(" ", null, "Slime", "Mannequin", out resolved));
        Assert.Equal("Slime", resolved);
        Assert.False(ChatWhisper.TryResolveTarget("", null, "Mannequin", "Mannequin", out _));

        Assert.True(ChatWhisper.IsSelf("Aline", "aline", null));
        Assert.True(ChatWhisper.IsSelf("Héros", "compte", "héros"));
        Assert.False(ChatWhisper.IsSelf("Bruno", "aline", "Héros"));

        Assert.True(ChatWhisper.TryPresent(ChatWhisper.OfflineWire, true, out var french));
        Assert.Equal("Joueur hors ligne.", french);
        Assert.True(ChatWhisper.TryPresent(ChatWhisper.OfflineWire, false, out french));
        Assert.Equal("Joueur hors ligne ou inconnu.", french);
        Assert.True(ChatWhisper.TryPresent(ChatWhisper.UnknownWire, false, out french));
        Assert.Equal("Joueur inconnu.", french);
        Assert.True(ChatWhisper.TryPresent(ChatWhisper.UnknownWire, true, out french));
        Assert.Equal("Joueur hors ligne.", french);
        Assert.True(ChatWhisper.TryPresent(ChatWhisper.BlockedWire, false, out french));
        Assert.Equal("Vous êtes bloqué.", french);
        Assert.Equal("Chuchotement envoyé à Aline.", ChatWhisper.SentTo("Aline"));
        Assert.False(ChatWhisper.TryPresent("Identifiants invalides.", false, out _));
    }

    [Fact]
    public void Roster_KnownOfflineFriend_UpgradesUnknown()
    {
        var roster = new ClientSocialRoster();
        roster.ApplySnapshot(new SocialSnapshotWire(
            SocialKind.Friend,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            [
                new SocialMemberWire(Guid.NewGuid(), ClientSocialRoster.FriendRoleAccepted, false, "Aline"),
                new SocialMemberWire(Guid.NewGuid(), ClientSocialRoster.FriendRoleAccepted, true, "Bruno"),
            ]));

        Assert.True(roster.IsKnownOffline("aline"));
        Assert.False(roster.IsKnownOffline("Bruno"));
        Assert.False(roster.IsKnownOffline("Inconnu"));
    }

    [Fact]
    public void Shell_WiresFocusLockAndWhisper_WithoutNewOpcode()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        var input = File.ReadAllText(Path.Combine(root, "Frog.Client", "Services", "InputService.cs"));
        var dock = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "HudChatDock.cs"));
        var help = File.ReadAllText(Path.Combine(root, "Frog.Client", "Forms", "HelpForm.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(root, "Frog.Server", "Network", "PacketDispatcher.cs"));
        var packetId = File.ReadAllText(Path.Combine(root, "Frog.Core", "Enums", "PacketId.cs"));

        Assert.Contains("TryHandleChatComposeKey", shell, StringComparison.Ordinal);
        Assert.Contains("ChatCompose.Decide", shell, StringComparison.Ordinal);
        Assert.Contains("OnWorldSurfaceClick", shell, StringComparison.Ordinal);
        Assert.Contains("ReleaseChatFocus", shell, StringComparison.Ordinal);
        Assert.Contains("StopMovementForChat", shell, StringComparison.Ordinal);
        Assert.Contains("IsTextInputFocus(ActiveControl)", shell, StringComparison.Ordinal);
        Assert.Contains("ChatWhisper.TryResolveTarget", shell, StringComparison.Ordinal);
        Assert.Contains("ChatWhisper.TryPresent", shell, StringComparison.Ordinal);
        Assert.Contains("SendChatAsync(ch, whisperTo, body)", shell, StringComparison.Ordinal);
        Assert.Contains("\"Chuchoter\"", shell, StringComparison.Ordinal);
        Assert.Contains("DeepActive", input, StringComparison.Ordinal);
        Assert.Contains("\"Chuchoter\"", dock, StringComparison.Ordinal);
        Assert.Contains("Échap", help, StringComparison.Ordinal);
        Assert.Contains("ProcessCmdKey", shell, StringComparison.Ordinal);
        Assert.Contains("_hudChat.AppendChat", shell, StringComparison.Ordinal);
        Assert.Contains("ChatWhisper.ParseSlash", shell, StringComparison.Ordinal);
        Assert.Contains("case ChatChannel.Whisper:", dispatcher, StringComparison.Ordinal);
        Assert.Contains("TryGetSessionByUsername(whisperTarget", dispatcher, StringComparison.Ordinal);
        Assert.Contains("\"Joueur hors ligne.\"", dispatcher, StringComparison.Ordinal);
        Assert.Contains("\"Vous etes bloque.\"", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveWhisperTargetAsync", dispatcher, StringComparison.Ordinal);
        Assert.Contains("SocialRequest = 80", packetId, StringComparison.Ordinal);
        Assert.DoesNotContain("WhisperRequest", packetId, StringComparison.Ordinal);

        var status = File.ReadAllText(Path.Combine(root, "docs", "progress", "client-ui", "STATUS-chat-focus.md"));
        Assert.Contains("**Propriétaire** | Netsun", status, StringComparison.Ordinal);
        Assert.Contains("48", status, StringComparison.Ordinal);
        Assert.Contains("11", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", status, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_ExistingWhisperRoute_UsernameOfflineAndPublicChat()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            var userA = UniqueUser("wa");
            var userB = UniqueUser("wb");
            var userC = UniqueUser("wc");

            await using var a = new TcpProbe();
            await using var b = new TcpProbe();
            await using var c = new TcpProbe();
            await RegisterLoginSelectAsync(a, port, userA, password, "Aline");
            await RegisterLoginSelectAsync(b, port, userB, password, "Bruno");
            await RegisterLoginSelectAsync(c, port, userC, password, "Celine");

            await a.SendFrameAsync(BuildChat(ChatChannel.Global, "public-hello"));
            var global = await b.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(global, out var gCh, out var gFrom, out var gMsg));
            Assert.Equal(ChatChannel.Global, gCh);
            Assert.Equal(userA, gFrom);
            Assert.Equal("public-hello", gMsg);
            var globalC = await c.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(globalC, out _, out _, out var gMsgC));
            Assert.Equal("public-hello", gMsgC);
            await a.DrainPendingAsync(TimeSpan.FromMilliseconds(200));

            await a.SendFrameAsync(BuildChat(ChatChannel.Whisper, "psst", userB));
            var echo = await a.ReadUntilAsync(PacketId.ChatMessage);
            var delivered = await b.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(echo, out var wCh, out _, out var wMsg));
            Assert.Equal(ChatChannel.Whisper, wCh);
            Assert.Equal("psst", wMsg);
            Assert.True(TryDecodeChat(delivered, out var dCh, out var dFrom, out var dMsg));
            Assert.Equal(ChatChannel.Whisper, dCh);
            Assert.Equal(userA, dFrom);
            Assert.Equal("psst", dMsg);
            await c.DrainPendingAsync(TimeSpan.FromMilliseconds(200));

            await a.SendFrameAsync(BuildChat(ChatChannel.Whisper, "qui", "NoSuchPlayer"));
            Assert.Equal("Joueur hors ligne.", DecodeError(await a.ReadUntilAsync(PacketId.Error)));

            await b.SendFrameAsync([(byte)PacketId.LogoutRequest]);
            _ = await b.ReadUntilAsync(PacketId.LogoutAck);
            await a.SendFrameAsync(BuildChat(ChatChannel.Whisper, "reviens", userB));
            Assert.Equal(ChatWhisper.OfflineWire, DecodeError(await a.ReadUntilAsync(PacketId.Error)));

            await a.SendFrameAsync(BuildChat(ChatChannel.Map, "local-still"));
            var map = await c.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(TryDecodeChat(map, out var mCh, out _, out var mMsg));
            Assert.Equal(ChatChannel.Map, mCh);
            Assert.Equal("local-still", mMsg);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IHost CreateInMemoryHost(int port)
        => FrogServerHostFactory
            .CreateHostBuilder(configureServices: services =>
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

    private static async Task RegisterLoginSelectAsync(
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

        throw new InvalidOperationException("Repo root not found.");
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
                    var frame = await ReadFrameAsync(remaining);
                    if (frame.Length > 2
                        && frame[0] == (byte)PacketId.ChatMessage
                        && frame[1] == (byte)ChatChannel.Whisper)
                    {
                        throw new InvalidOperationException("whisper leaked");
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
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
