using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// Public TCP proof: InteractRequest activationId is the PostgreSQL ledger identity.
/// </summary>
[Collection("PostgresIsolated")]
public sealed class Phase8InteractIdentityTcpTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase8InteractIdentityTcpTests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact(Timeout = 90_000)]
    [Trait("Category", "PostgreSql")]
    public async Task SameIdentityTwice_OneMutation_OneLedgerRow_SecondIdempotent()
    {
        await using var ctx = await StartAsync();
        var activationId = Guid.NewGuid();
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.OnceRewardEventTileX, ctx.Seed.OnceRewardEventTileY);

        var first = await InteractAsync(ctx.Client, activationId);
        Assert.True(first.Ok);
        Assert.Contains("Once chest opened", first.Message, StringComparison.OrdinalIgnoreCase);

        var second = await InteractAsync(ctx.Client, activationId);
        Assert.True(second.Ok);
        Assert.Contains("Once chest opened", second.Message, StringComparison.OrdinalIgnoreCase);

        var snap = await Phase8TcpTestHelpers.ReselectAndReadSnapshotsAsync(ctx.Client, ctx.CharacterId);
        Assert.Equal(1, Phase8WireDecoders.CountItemQuantity(snap.Inventory, ctx.Seed.Phase7.ConsumableId));
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, activationId));
    }

    [PostgresFact(Timeout = 90_000)]
    [Trait("Category", "PostgreSql")]
    public async Task CommitUnreadReconnect_ResendSameIdentity_NoDuplicate()
    {
        await using var ctx = await StartAsync();
        var activationId = Guid.NewGuid();
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.OnceRewardEventTileX, ctx.Seed.OnceRewardEventTileY);

        await ctx.Client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract(activationId));
        await ctx.Client.DisconnectAsync();
        await Task.Delay(250);

        await using var client2 = new Phase7TcpTestClient();
        await ReconnectSelectAsync(client2, ctx.Port, ctx.Token, ctx.CharacterId);
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            client2, ctx.Seed.OnceRewardEventTileX, ctx.Seed.OnceRewardEventTileY);

        var replay = await InteractAsync(client2, activationId);
        Assert.True(replay.Ok);

        var snap = await Phase8TcpTestHelpers.ReselectAndReadSnapshotsAsync(client2, ctx.CharacterId);
        Assert.Equal(1, Phase8WireDecoders.CountItemQuantity(snap.Inventory, ctx.Seed.Phase7.ConsumableId));
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, activationId));
    }

    [PostgresFact(Timeout = 90_000)]
    [Trait("Category", "PostgreSql")]
    public async Task RollbackThenRetrySameIdentity_Succeeds()
    {
        await using var ctx = await StartAsync();
        var takeId = Guid.NewGuid();
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.TakeItemEventTileX, ctx.Seed.TakeItemEventTileY);

        var failed = await InteractAsync(ctx.Client, takeId);
        Assert.False(failed.Ok);
        Assert.Contains("insuffisante", failed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountLedgerByActivationAsync(ctx.CharacterGuid, takeId));

        var grantId = Guid.NewGuid();
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.OnceRewardEventTileX, ctx.Seed.OnceRewardEventTileY);
        var granted = await InteractAsync(ctx.Client, grantId);
        Assert.True(granted.Ok);

        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.TakeItemEventTileX, ctx.Seed.TakeItemEventTileY);
        var retry = await InteractAsync(ctx.Client, takeId);
        Assert.True(retry.Ok);
        Assert.Contains("Item taken", retry.Message, StringComparison.OrdinalIgnoreCase);

        var snap = await Phase8TcpTestHelpers.ReselectAndReadSnapshotsAsync(ctx.Client, ctx.CharacterId);
        Assert.Equal(0, Phase8WireDecoders.CountItemQuantity(snap.Inventory, ctx.Seed.Phase7.ConsumableId));
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, takeId));
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, grantId));
    }

    [PostgresFact(Timeout = 90_000)]
    [Trait("Category", "PostgreSql")]
    public async Task NewIdentityOnSameEvent_IsLegitimateNewActivation()
    {
        await using var ctx = await StartAsync();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.KeyEventTileX, ctx.Seed.KeyEventTileY);

        var first = await InteractAsync(ctx.Client, firstId);
        Assert.True(first.Ok);
        var second = await InteractAsync(ctx.Client, secondId);
        Assert.True(second.Ok);

        Assert.NotEqual(firstId, secondId);
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, firstId));
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, secondId));
    }

    [PostgresFact(Timeout = 90_000)]
    [Trait("Category", "PostgreSql")]
    public async Task IdentityReusedForOtherEventOrCharacter_IsRejected()
    {
        await using var ctx = await StartAsync();
        var sharedId = Guid.NewGuid();
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.OnceRewardEventTileX, ctx.Seed.OnceRewardEventTileY);
        var first = await InteractAsync(ctx.Client, sharedId);
        Assert.True(first.Ok);

        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.KeyEventTileX, ctx.Seed.KeyEventTileY);
        var otherEvent = await InteractAsync(ctx.Client, sharedId);
        Assert.False(otherEvent.Ok);
        Assert.Contains("événement différent", otherEvent.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, sharedId));

        await using var other = new Phase7TcpTestClient();
        var otherId = await RegisterSecondAccountAsync(other, ctx.Port, ctx.Seed, "OtherHero");
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            other, ctx.Seed.OnceRewardEventTileX, ctx.Seed.OnceRewardEventTileY);
        var otherChar = await InteractAsync(other, sharedId);
        Assert.False(otherChar.Ok);
        Assert.Contains("personnage différent", otherChar.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await CountLedgerByRequestIdAsync(sharedId));
        Assert.Equal(0, await CountLedgerByActivationAsync(otherId, sharedId));
    }

    [PostgresFact(Timeout = 90_000)]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentSameIdentity_ExactlyOneCommittedExecution()
    {
        await using var ctx = await StartAsync();
        var activationId = Guid.NewGuid();
        await Phase8MovementTestHelpers.TeleportToTileAsync(
            ctx.Client, ctx.Seed.OnceRewardEventTileX, ctx.Seed.OnceRewardEventTileY);

        await Task.WhenAll(
            ctx.Client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract(activationId)),
            ctx.Client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract(activationId)));

        var result1 = await ctx.Client.ReadUntilAsync(PacketId.InteractResult);
        var result2 = await ctx.Client.ReadUntilAsync(PacketId.InteractResult);
        Assert.True(Phase8WireDecoders.TryDecodeInteractResult(result1, out var ok1, out _, out var id1));
        Assert.True(Phase8WireDecoders.TryDecodeInteractResult(result2, out var ok2, out _, out var id2));
        Assert.True(ok1);
        Assert.True(ok2);
        Assert.Equal(activationId, id1);
        Assert.Equal(activationId, id2);

        var snap = await Phase8TcpTestHelpers.ReselectAndReadSnapshotsAsync(ctx.Client, ctx.CharacterId);
        Assert.Equal(1, Phase8WireDecoders.CountItemQuantity(snap.Inventory, ctx.Seed.Phase7.ConsumableId));
        Assert.Equal(1, await CountLedgerByActivationAsync(ctx.CharacterGuid, activationId));
    }

    private async Task<HostContext> StartAsync()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        var client = new Phase7TcpTestClient();
        try
        {
            var (token, characterId) = await RegisterAsync(client, port, seed, "IdHero");
            return new HostContext(host, client, seed, port, token, characterId, Guid.Parse(characterId));
        }
        catch
        {
            await client.DisposeAsync();
            await host.StopAsync();
            host.Dispose();
            throw;
        }
    }

    private sealed class HostContext(
        IHost host,
        Phase7TcpTestClient client,
        Phase8PostgresContentSeedResult seed,
        int port,
        string token,
        string characterId,
        Guid characterGuid) : IAsyncDisposable
    {
        public IHost Host { get; } = host;

        public Phase7TcpTestClient Client { get; } = client;

        public Phase8PostgresContentSeedResult Seed { get; } = seed;

        public int Port { get; } = port;

        public string Token { get; } = token;

        public string CharacterId { get; } = characterId;

        public Guid CharacterGuid { get; } = characterGuid;

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Host.StopAsync();
            Host.Dispose();
        }
    }

    private static async Task<(bool Ok, string Message)> InteractAsync(Phase7TcpTestClient client, Guid activationId)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract(activationId));
        var frame = await client.ReadUntilAsync(PacketId.InteractResult);
        Assert.True(Phase8WireDecoders.TryDecodeInteractResult(frame, out var ok, out var message, out var echoed));
        Assert.Equal(activationId, echoed);
        return (ok, message);
    }

    private static async Task<(string Token, string CharacterId)> RegisterAsync(
        Phase7TcpTestClient tcp,
        int port,
        Phase8PostgresContentSeedResult seed,
        string charName)
    {
        var user = $"id-{Guid.NewGuid():N}"[..16];
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, "password12345"));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.RegisterResult))[1]);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, "password12345"));
        var token = Phase7WireDecoders.DecodeLoginToken(await tcp.ReadUntilAsync(PacketId.LoginResult));
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, seed.Phase7.ClassId));
        var characterId = Phase7WireDecoders.DecodeCharacterId(await tcp.ReadUntilAsync(PacketId.CharacterCreateResult));
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
        _ = await Phase8TcpTestHelpers.DrainAccountSelectSnapshotsAsync(tcp);
        return (token, characterId);
    }

    private static async Task<Guid> RegisterSecondAccountAsync(
        Phase7TcpTestClient tcp,
        int port,
        Phase8PostgresContentSeedResult seed,
        string charName)
    {
        var (token, characterId) = await RegisterAsync(tcp, port, seed, charName);
        _ = token;
        return Guid.Parse(characterId);
    }

    private static async Task ReconnectSelectAsync(
        Phase7TcpTestClient tcp,
        int port,
        string token,
        string characterId)
    {
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.ReconnectResult))[1]);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
        Assert.NotEqual(0, (await tcp.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
        _ = await tcp.ReadUntilAsync(PacketId.CombatState);
        _ = await tcp.ReadUntilAsync(PacketId.InventorySnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.BankSnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.GroundItemsSnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.QuestJournalSnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.EnvironmentStatePush);
        await tcp.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
    }

    private async Task<Phase8PostgresContentSeedResult> SeedAsync()
    {
        using var gate = CreateGate();
        return await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private async Task<int> CountLedgerByActivationAsync(Guid characterId, Guid activationId)
    {
        using var gate = CreateGate();
        return await gate.ExecuteAsync(
            async (db, ct) => await db.PlayerMapEventExecutionRequests.CountAsync(
                r => r.CharacterId == characterId && r.ActivationId == activationId,
                ct),
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<int> CountLedgerByRequestIdAsync(Guid requestId)
    {
        using var gate = CreateGate();
        return await gate.ExecuteAsync(
            async (db, ct) => await db.PlayerMapEventExecutionRequests.CountAsync(
                r => r.RequestId == requestId,
                ct),
            CancellationToken.None).ConfigureAwait(false);
    }
}
