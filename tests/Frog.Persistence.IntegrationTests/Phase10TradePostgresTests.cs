using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Gameplay;
using Frog.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase10TradePostgresTests
{
    private readonly IsolatedPostgresFixture _fixture;
    private const string Password = "password12345";

    public Phase10TradePostgresTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task TradeCommit_Replay_CrashRollback_LedgerAndGold()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        _ = await Phase7PostgresContentSeed.PublishAsync(seedGate);

        var port = Phase7TcpTestPorts.GetFreePort();
        var (builder, _) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            var aId = await RegisterLoginSelectAsync(a, port, UniqueUser("ta"), "TaHero");
            var bId = await RegisterLoginSelectAsync(b, port, UniqueUser("tb"), "TbHero");
            await SeedAsync(host, aId, 80, Phase7ContentSeed.DefaultItemId, 4);
            await SeedAsync(host, bId, 40, Phase7ContentSeed.DefaultWeaponId, 1);

            var repo = (PostgresTradeCommitRepository)host.Services.GetRequiredService<ITradeCommitRepository>();
            repo.TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected");

            var tradeId = await OpenAndOfferAsync(a, b, aId, bId, initiatorGold: 10, partnerGold: 5);
            var commitReq = Guid.NewGuid();
            var snap = await ReadTradeSnapshotAsync(a);
            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
                (byte)TradeAction.Confirm,
                tradeId,
                Guid.NewGuid(),
                TradeWire.BuildRevisionPayload(snap.Revision)));
            Assert.True(DecodeTrade(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
            await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
                (byte)TradeAction.Confirm,
                tradeId,
                commitReq,
                TradeWire.BuildRevisionPayload(snap.Revision)));
            var failed = DecodeTrade(await b.ReadUntilAsync(PacketId.TradeResult));
            Assert.False(failed.Success);

            var chars = host.Services.GetRequiredService<ICharacterRepository>();
            Assert.Equal(80, (await chars.FindByIdAsync(aId))!.Gold);
            Assert.Equal(40, (await chars.FindByIdAsync(bId))!.Gold);
            Assert.Null(await repo.FindExecutionAsync(tradeId));

            repo.TestBeforeCommitAsync = null;
            var retryReq = Guid.NewGuid();
            snap = await LatestOpenSnapshotAsync(a, b, tradeId);
            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
                (byte)TradeAction.Confirm,
                tradeId,
                Guid.NewGuid(),
                TradeWire.BuildRevisionPayload(snap.Revision)));
            _ = await a.ReadUntilAsync(PacketId.TradeResult);
            await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
                (byte)TradeAction.Confirm,
                tradeId,
                retryReq,
                TradeWire.BuildRevisionPayload(snap.Revision)));
            Assert.True(DecodeTrade(await b.ReadUntilAsync(PacketId.TradeResult)).Success);

            Assert.Equal(75, (await chars.FindByIdAsync(aId))!.Gold);
            Assert.Equal(45, (await chars.FindByIdAsync(bId))!.Gold);
            var ledger = await repo.FindExecutionAsync(tradeId);
            Assert.NotNull(ledger);
            Assert.Contains("initiatorGold", ledger!.ContentsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("password", ledger.ContentsJson, StringComparison.OrdinalIgnoreCase);

            await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
                (byte)TradeAction.Confirm,
                tradeId,
                retryReq,
                TradeWire.BuildRevisionPayload(snap.Revision)));
            var replay = DecodeTrade(await b.ReadUntilAsync(PacketId.TradeResult));
            Assert.True(replay.Success);
            Assert.Equal(75, (await chars.FindByIdAsync(aId))!.Gold);
            Assert.Equal(45, (await chars.FindByIdAsync(bId))!.Gold);

            var invA = await host.Services.GetRequiredService<IInventoryRepository>().GetAsync(aId);
            var invB = await host.Services.GetRequiredService<IInventoryRepository>().GetAsync(bId);
            Assert.Equal(2, invA.Slots.Where(s => s.ItemId == Phase7ContentSeed.DefaultItemId).Sum(s => s.Quantity));
            Assert.Contains(invA.Slots, s => s.ItemId == Phase7ContentSeed.DefaultWeaponId);
            Assert.Equal(2, invB.Slots.Where(s => s.ItemId == Phase7ContentSeed.DefaultItemId).Sum(s => s.Quantity));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task<Guid> OpenAndOfferAsync(
        Phase7TcpTestClient a,
        Phase7TcpTestClient b,
        Guid aId,
        Guid bId,
        int initiatorGold,
        int partnerGold)
    {
        await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildTradeInvite(Guid.NewGuid(), bId));
        var invite = DecodeTrade(await a.ReadUntilAsync(PacketId.TradeResult));
        Assert.True(invite.Success);
        _ = await b.ReadUntilAsync(PacketId.TradeSnapshot);
        await a.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
        await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
            (byte)TradeAction.Accept, invite.TradeId, Guid.NewGuid()));
        Assert.True(DecodeTrade(await b.ReadUntilAsync(PacketId.TradeResult)).Success);
        var open = DecodeSnap(await a.ReadUntilAsync(PacketId.TradeSnapshot));
        if (open.Status != TradeStatus.Open)
        {
            open = DecodeSnap(await a.ReadUntilAsync(PacketId.TradeSnapshot));
        }

        await b.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
        var extraA = TradeWire.BuildSetOfferPayload(
            open.Revision,
            initiatorGold,
            [new TradeStackWire(Phase7ContentSeed.DefaultItemId, 2, "")]);
        await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
            (byte)TradeAction.SetOffer, invite.TradeId, Guid.NewGuid(), extraA));
        Assert.True(DecodeTrade(await a.ReadUntilAsync(PacketId.TradeResult)).Success);
        var afterA = DecodeSnap(await b.ReadUntilAsync(PacketId.TradeSnapshot));
        var extraB = TradeWire.BuildSetOfferPayload(afterA.Revision, partnerGold, []);
        await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
            (byte)TradeAction.SetOffer, invite.TradeId, Guid.NewGuid(), extraB));
        Assert.True(DecodeTrade(await b.ReadUntilAsync(PacketId.TradeResult)).Success);
        _ = await a.ReadUntilAsync(PacketId.TradeSnapshot);
        return invite.TradeId;
    }

    private static async Task<TradeSnapshotWire> LatestOpenSnapshotAsync(
        Phase7TcpTestClient a,
        Phase7TcpTestClient b,
        Guid tradeId)
    {
        await a.DrainPendingAsync(TimeSpan.FromMilliseconds(80));
        await b.DrainPendingAsync(TimeSpan.FromMilliseconds(80));
        await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildTrade(
            (byte)TradeAction.Unconfirm, tradeId, Guid.NewGuid()));
        _ = await a.ReadUntilAsync(PacketId.TradeResult);
        return DecodeSnap(await b.ReadUntilAsync(PacketId.TradeSnapshot));
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
            Assert.Equal(InventoryMutationStatus.Ok, (await inv.TryAddAsync(characterId, itemId, qty, 20)).Status);
        }

        foreach (var s in host.Services.GetRequiredService<ConnectionManager>().GetActiveSessions())
        {
            if (s.CharacterGuid == characterId)
            {
                s.Gold = gold;
            }
        }
    }

    private async Task<Guid> RegisterLoginSelectAsync(Phase7TcpTestClient client, int port, string user, string name)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, Password));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.RegisterResult))[1]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, Password));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.LoginResult))[1]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(name, Phase7ContentSeed.DefaultClassId));
        var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
        var characterId = Phase7WireDecoders.DecodeCharacterId(create);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
        await client.DrainPendingAsync();
        return Guid.Parse(characterId);
    }

    private static TradeResultWire DecodeTrade(byte[] frame)
    {
        Assert.Equal((byte)PacketId.TradeResult, frame[0]);
        Assert.True(TradeWire.TryParseResult(frame.AsSpan(1), out var result));
        return result;
    }

    private static TradeSnapshotWire DecodeSnap(byte[] frame)
    {
        Assert.Equal((byte)PacketId.TradeSnapshot, frame[0]);
        Assert.True(TradeWire.TryParseSnapshot(frame.AsSpan(1), out var snap));
        return snap;
    }

    private static async Task<TradeSnapshotWire> ReadTradeSnapshotAsync(Phase7TcpTestClient client)
        => DecodeSnap(await client.ReadUntilAsync(PacketId.TradeSnapshot));

    private static string UniqueUser(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
