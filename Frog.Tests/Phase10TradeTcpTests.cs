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
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Gameplay;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10TradeTcpTests
{
    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_DoubleConfirm_Replay_ChangeOffer_ConcurrentSell_Disconnect()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var a = new TcpProbe();
            await using var b = new TcpProbe();
            var aId = await RegisterLoginSelectAsync(a, port, UniqueUser("ta"), password, "TaHero");
            var bId = await RegisterLoginSelectAsync(b, port, UniqueUser("tb"), password, "TbHero");
            await SeedAsync(host, aId, gold: 80, itemId: Phase7ContentSeed.DefaultItemId, qty: 4);
            await SeedAsync(host, bId, gold: 50, itemId: Phase7ContentSeed.DefaultWeaponId, qty: 1);

            var inviteReq = Guid.NewGuid();
            await a.SendFrameAsync(BuildTradeInvite(inviteReq, bId));
            var invite = DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult));
            Assert.True(invite.Success);
            var snap = DecodeTradeSnapshot(await b.ReadUntilAsync(PacketId.TradeSnapshot));
            Assert.Equal(TradeStatus.Inviting, snap.Status);
            await a.DrainPendingAsync();

            await b.SendFrameAsync(BuildTrade((byte)TradeAction.Accept, invite.TradeId, Guid.NewGuid()));
            Assert.True(DecodeTradeResult(await b.ReadUntilAsync(PacketId.TradeResult)).Success);
            snap = DecodeTradeSnapshot(await a.ReadUntilAsync(PacketId.TradeSnapshot));
            if (snap.Status != TradeStatus.Open)
            {
                snap = DecodeTradeSnapshot(await a.ReadUntilAsync(PacketId.TradeSnapshot));
            }

            Assert.Equal(TradeStatus.Open, snap.Status);
            await b.DrainPendingAsync();

            var offerExtra = TradeWire.BuildSetOfferPayload(
                snap.Revision,
                10,
                [new TradeStackWire(Phase7ContentSeed.DefaultItemId, 2, "")]);
            await a.SendFrameAsync(BuildTrade((byte)TradeAction.SetOffer, invite.TradeId, Guid.NewGuid(), offerExtra));
            Assert.True(DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
            snap = DecodeTradeSnapshot(await b.ReadUntilAsync(PacketId.TradeSnapshot));
            Assert.True(snap.Revision > 0);
            await a.DrainPendingAsync();

            await a.SendFrameAsync(BuildShopSell(0, 3, Guid.NewGuid()));
            var sell = DecodeStatus(await a.ReadUntilAsync(PacketId.ShopSellResult));
            Assert.False(sell.ok);

            var confA = Guid.NewGuid();
            await a.SendFrameAsync(BuildTrade(
                (byte)TradeAction.Confirm,
                invite.TradeId,
                confA,
                TradeWire.BuildRevisionPayload(snap.Revision)));
            Assert.True(DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
            await a.DrainPendingAsync();
            await b.DrainPendingAsync();

            var offerB = TradeWire.BuildSetOfferPayload(snap.Revision, 5, Array.Empty<TradeStackWire>());
            await b.SendFrameAsync(BuildTrade((byte)TradeAction.SetOffer, invite.TradeId, Guid.NewGuid(), offerB));
            Assert.True(DecodeTradeResult(await b.ReadUntilAsync(PacketId.TradeResult)).Success);
            snap = DecodeTradeSnapshot(await a.ReadUntilAsync(PacketId.TradeSnapshot));
            Assert.False(snap.InitiatorConfirmed);
            await b.DrainPendingAsync();

            var commitReq = Guid.NewGuid();
            await a.SendFrameAsync(BuildTrade(
                (byte)TradeAction.Confirm,
                invite.TradeId,
                Guid.NewGuid(),
                TradeWire.BuildRevisionPayload(snap.Revision)));
            Assert.True(DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
            await b.SendFrameAsync(BuildTrade(
                (byte)TradeAction.Confirm,
                invite.TradeId,
                commitReq,
                TradeWire.BuildRevisionPayload(snap.Revision)));
            var committed = DecodeTradeResult(await b.ReadUntilAsync(PacketId.TradeResult));
            Assert.True(committed.Success);

            var chars = host.Services.GetRequiredService<ICharacterRepository>();
            Assert.Equal(75, (await chars.FindByIdAsync(aId))!.Gold);
            Assert.Equal(55, (await chars.FindByIdAsync(bId))!.Gold);

            await b.SendFrameAsync(BuildTrade(
                (byte)TradeAction.Confirm,
                invite.TradeId,
                commitReq,
                TradeWire.BuildRevisionPayload(snap.Revision)));
            var replay = DecodeTradeResult(await b.ReadUntilAsync(PacketId.TradeResult));
            Assert.True(replay.Success);
            Assert.Equal(75, (await chars.FindByIdAsync(aId))!.Gold);

            await using var c = new TcpProbe();
            var cId = await RegisterLoginSelectAsync(c, port, UniqueUser("tc"), password, "TcHero");
            await SeedAsync(host, cId, 30, Phase7ContentSeed.DefaultItemId, 1);
            await a.SendFrameAsync(BuildTradeInvite(Guid.NewGuid(), cId));
            var inv2 = DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult));
            Assert.True(inv2.Success);
            await c.DisconnectAsync();
            await Task.Delay(200);
            var leftover = await chars.FindByIdAsync(aId);
            Assert.Equal(75, leftover!.Gold);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_Rejects_OutOfRange_Block_UnknownAction_FullInventory_LowGold()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var a = new TcpProbe();
            await using var b = new TcpProbe();
            var aUser = UniqueUser("ra");
            var bUser = UniqueUser("rb");
            var aId = await RegisterLoginSelectAsync(a, port, aUser, password, "RaHero");
            var bId = await RegisterLoginSelectAsync(b, port, bUser, password, "RbHero");

            await a.SendFrameAsync(BuildTrade((byte)99, Guid.NewGuid(), Guid.NewGuid()));
            Assert.False(DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult)).Success);

            var connections = host.Services.GetRequiredService<ConnectionManager>();
            foreach (var s in connections.GetActiveSessions())
            {
                if (s.CharacterGuid == bId)
                {
                    s.PixelX += 400;
                }
            }

            await a.SendFrameAsync(BuildTradeInvite(Guid.NewGuid(), bId));
            var tooFar = DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult));
            Assert.False(tooFar.Success);
            Assert.Contains("loin", tooFar.Message, StringComparison.OrdinalIgnoreCase);

            foreach (var s in connections.GetActiveSessions())
            {
                if (s.CharacterGuid == bId)
                {
                    s.PixelX = connections.GetActiveSessions().First(x => x.CharacterGuid == aId).PixelX;
                    s.PixelY = connections.GetActiveSessions().First(x => x.CharacterGuid == aId).PixelY;
                }
            }

            await b.SendFrameAsync(BuildSocial(SocialKind.Block, (byte)BlockAction.Block, Guid.NewGuid(), aId));
            Assert.True(DecodeSocialResult(await b.ReadUntilAsync(PacketId.SocialResult)).Success);
            await a.SendFrameAsync(BuildTradeInvite(Guid.NewGuid(), bId));
            Assert.False(DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "InMemorySmoke")]
    public async Task Tcp_InviteByName_FullInventoryResetsConfirm_DisconnectCancels()
    {
        var port = GetFreePort();
        using var host = CreateInMemoryHost(port);
        await host.StartAsync();
        try
        {
            const string password = "password123";
            await using var a = new TcpProbe();
            await using var b = new TcpProbe();
            var aUser = UniqueUser("na");
            var bUser = UniqueUser("nb");
            var aId = await RegisterLoginSelectAsync(a, port, aUser, password, "NaHero");
            var bId = await RegisterLoginSelectAsync(b, port, bUser, password, "NbHero");
            await SeedAsync(host, aId, gold: 40, itemId: Phase7ContentSeed.DefaultItemId, qty: 1);
            await SeedAsync(host, bId, gold: 10, itemId: Guid.NewGuid(), qty: 1);
            var inv = host.Services.GetRequiredService<IInventoryRepository>();
            for (var i = 0; i < GameplayLimits.InventorySlotCount - 1; i++)
            {
                var added = await inv.TryAddAsync(bId, Guid.NewGuid(), 1, 1);
                Assert.Equal(InventoryMutationStatus.Ok, added.Status);
            }

            await a.SendFrameAsync(BuildTrade(
                (byte)TradeAction.Invite,
                Guid.Empty,
                Guid.NewGuid(),
                TradeWire.BuildInviteName(bUser)));
            var invite = DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult));
            Assert.True(invite.Success);
            var snap = DecodeTradeSnapshot(await b.ReadUntilAsync(PacketId.TradeSnapshot));
            Assert.Equal(TradeStatus.Inviting, snap.Status);
            await a.DrainPendingAsync();

            await b.SendFrameAsync(BuildTrade((byte)TradeAction.Accept, invite.TradeId, Guid.NewGuid()));
            Assert.True(DecodeTradeResult(await b.ReadUntilAsync(PacketId.TradeResult)).Success);
            snap = DecodeTradeSnapshot(await a.ReadUntilAsync(PacketId.TradeSnapshot));
            if (snap.Status != TradeStatus.Open)
            {
                snap = DecodeTradeSnapshot(await a.ReadUntilAsync(PacketId.TradeSnapshot));
            }

            Assert.Equal(TradeStatus.Open, snap.Status);
            await b.DrainPendingAsync();

            var offer = TradeWire.BuildSetOfferPayload(
                snap.Revision,
                0,
                [new TradeStackWire(Phase7ContentSeed.DefaultItemId, 1, "")]);
            await a.SendFrameAsync(BuildTrade((byte)TradeAction.SetOffer, invite.TradeId, Guid.NewGuid(), offer));
            Assert.True(DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
            snap = DecodeTradeSnapshot(await b.ReadUntilAsync(PacketId.TradeSnapshot));
            await a.DrainPendingAsync();

            await a.SendFrameAsync(BuildTrade(
                (byte)TradeAction.Confirm,
                invite.TradeId,
                Guid.NewGuid(),
                TradeWire.BuildRevisionPayload(snap.Revision)));
            Assert.True(DecodeTradeResult(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
            await b.SendFrameAsync(BuildTrade(
                (byte)TradeAction.Confirm,
                invite.TradeId,
                Guid.NewGuid(),
                TradeWire.BuildRevisionPayload(snap.Revision)));
            var failed = DecodeTradeResult(await b.ReadUntilAsync(PacketId.TradeResult));
            Assert.False(failed.Success);
            Assert.Contains("plein", failed.Message, StringComparison.OrdinalIgnoreCase);
            snap = DecodeTradeSnapshot(await a.ReadUntilAsync(PacketId.TradeSnapshot));
            Assert.Equal(TradeStatus.Open, snap.Status);
            Assert.False(snap.InitiatorConfirmed);
            Assert.False(snap.PartnerConfirmed);
            Assert.Contains("plein", snap.Notice, StringComparison.OrdinalIgnoreCase);
            await b.DrainPendingAsync();

            await a.DisconnectAsync();
            snap = DecodeTradeSnapshot(await b.ReadUntilAsync(PacketId.TradeSnapshot));
            Assert.Equal(TradeStatus.Cancelled, snap.Status);
            Assert.Contains("deconnecte", snap.Notice, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task SeedAsync(IHost host, Guid characterId, int gold, Guid itemId, int qty)
    {
        var chars = host.Services.GetRequiredService<ICharacterRepository>();
        var inv = host.Services.GetRequiredService<IInventoryRepository>();
        var rec = await chars.FindByIdAsync(characterId);
        Assert.NotNull(rec);
        await chars.SaveAsync(rec! with { Gold = gold });
        if (qty > 0)
        {
            var added = await inv.TryAddAsync(characterId, itemId, qty, 20);
            Assert.Equal(InventoryMutationStatus.Ok, added.Status);
        }

        foreach (var s in host.Services.GetRequiredService<ConnectionManager>().GetActiveSessions())
        {
            if (s.CharacterGuid == characterId)
            {
                s.Gold = gold;
            }
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

    private static byte[] BuildCharacterSelect(string id)
    {
        var b = Encoding.UTF8.GetBytes(id);
        var payload = new byte[1 + 1 + b.Length];
        payload[0] = (byte)PacketId.CharacterSelectRequest;
        payload[1] = (byte)b.Length;
        b.CopyTo(payload, 2);
        return payload;
    }

    private static byte[] BuildTrade(byte action, Guid tradeId, Guid requestId, ReadOnlySpan<byte> extra = default)
    {
        var body = TradeWire.BuildRequest(action, tradeId, requestId, extra);
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)PacketId.TradeRequest;
        body.CopyTo(payload.AsSpan(1));
        return payload;
    }

    private static byte[] BuildTradeInvite(Guid requestId, Guid target)
        => BuildTrade((byte)TradeAction.Invite, Guid.Empty, requestId, TradeWire.BuildGuidPayload(target));

    private static byte[] BuildShopSell(byte slot, int qty, Guid requestId)
    {
        var payload = new byte[1 + 1 + 4 + 16];
        payload[0] = (byte)PacketId.ShopSellRequest;
        payload[1] = slot;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), qty);
        requestId.TryWriteBytes(payload.AsSpan(6));
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

    private static TradeResultWire DecodeTradeResult(byte[] frame)
    {
        Assert.Equal((byte)PacketId.TradeResult, frame[0]);
        Assert.True(TradeWire.TryParseResult(frame.AsSpan(1), out var result));
        return result;
    }

    private static TradeSnapshotWire DecodeTradeSnapshot(byte[] frame)
    {
        Assert.Equal((byte)PacketId.TradeSnapshot, frame[0]);
        Assert.True(TradeWire.TryParseSnapshot(frame.AsSpan(1), out var snap));
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
