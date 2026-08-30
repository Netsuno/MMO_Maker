using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase8MultiClientE2ETests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase8MultiClientE2ETests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task PerCharacterSwitchAndQuest_IsolatedBetweenClients()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            var idA = await RegisterAsync(a, port, seed, "IsoA");
            var idB = await RegisterAsync(b, port, seed, "IsoB");

            await Phase8MovementTestHelpers.TeleportToTileAsync(a, seed.GateEventTileX, seed.GateEventTileY);
            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(
                await a.ReadUntilAsync(PacketId.InteractResult), out _, out var lockedA));
            Assert.Contains("Gate locked", lockedA);

            await Phase8MovementTestHelpers.TeleportToTileAsync(a, seed.KeyEventTileX, seed.KeyEventTileY);
            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(
                await a.ReadUntilAsync(PacketId.InteractResult), out var keyOk, out _));
            Assert.True(keyOk);

            await Phase8MovementTestHelpers.TeleportToTileAsync(b, seed.GateEventTileX, seed.GateEventTileY);
            await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
            Assert.True(Phase8WireDecoders.TryDecodeInteractResult(
                await b.ReadUntilAsync(PacketId.InteractResult), out _, out var lockedMsg));
            Assert.Contains("Gate locked", lockedMsg);

            using var gate = CreateGate();
            var world = new PostgresCharacterWorldStateRepository(gate);
            Assert.True(await world.GetSwitchAsync(idA, seed.GateSwitchId));
            Assert.False(await world.GetSwitchAsync(idB, seed.GateSwitchId) ?? false);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task DialogueToken_SingleCharacterOnly()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var owner = new Phase7TcpTestClient();
            await using var intruder = new Phase7TcpTestClient();
            await RegisterAsync(owner, port, seed, "Owner");
            await RegisterAsync(intruder, port, seed, "Intruder");

            var dialogue = await OpenGateDialogueAsync(owner, seed);
            Assert.True(Phase8WireDecoders.TryDecodeDialogueStatePush(
                dialogue, out _, out _, out var token, out _, out _, out _));

            await intruder.SendFrameAsync(Phase7TcpPacketBuilder.BuildDialogueChoice(token, "accept"));
            var stolen = await intruder.ReadUntilAsync(PacketId.DialogueChoiceResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(stolen, out var ok, out _));
            Assert.False(ok);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task QuestTurnInRace_SameCharacter_ExactlyOneReward()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var clientA = new Phase7TcpTestClient();
            await using var clientB = new Phase7TcpTestClient();
            var (token, characterIdStr) = await RegisterReturningTokenAndIdAsync(clientA, port, seed, "QuestRace");
            var characterId = Guid.Parse(characterIdStr);
            _ = await clientA.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            _ = await clientA.ReadUntilAsync(PacketId.EnvironmentStatePush);
            await clientA.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
            await PrepareQuestReadyAsync(clientA, seed, characterId);

            var reqA = Guid.NewGuid();
            var reqB = Guid.NewGuid();
            var turnInFromA = clientA.SendFrameAsync(Phase7TcpPacketBuilder.BuildQuestTurnIn(seed.QuestId, reqA));
            var turnInFromB = Task.Run(async () =>
            {
                await clientB.ConnectAsync("127.0.0.1", port);
                _ = await clientB.ReadFrameAsync();
                await clientB.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
                _ = await clientB.ReadUntilAsync(PacketId.ReconnectResult);
                await clientB.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterIdStr));
                _ = await clientB.ReadUntilAsync(PacketId.CharacterSelectResult);
                await clientB.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
                await clientB.SendFrameAsync(Phase7TcpPacketBuilder.BuildQuestTurnIn(seed.QuestId, reqB));
            });
            await Task.WhenAll(turnInFromA, turnInFromB);

            try
            {
                _ = await clientA.ReadUntilAsync(PacketId.QuestTurnInResult, TimeSpan.FromSeconds(2));
            }
            catch (EndOfStreamException)
            {
                // Reconnect from client B displaces A before the turn-in response is read.
            }

            _ = await clientB.ReadUntilAsync(PacketId.QuestTurnInResult);

            using var gate = CreateGate();
            var chars = new PostgresCharacterRepository(gate);
            var gold = (await chars.FindByIdAsync(characterId))!.Gold;
            Assert.Equal(GameplayLimits.StartingGold + seed.QuestRewardGold, gold);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CraftRace_DuplicateRequestIds_PerCharacterIdempotent()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            var idA = await RegisterAsync(a, port, seed, "CraftA");
            var idB = await RegisterAsync(b, port, seed, "CraftB");
            await AcquireProfessionViaNetworkAsync(a, seed);
            await AcquireProfessionViaNetworkAsync(b, seed);
            await SeedCraftPrerequisitesAsync(idA, seed);
            await SeedCraftPrerequisitesAsync(idB, seed);

            var sharedRequest = Guid.NewGuid();
            await Task.WhenAll(
                a.SendFrameAsync(Phase7TcpPacketBuilder.BuildCraft(seed.RecipeId, sharedRequest)),
                b.SendFrameAsync(Phase7TcpPacketBuilder.BuildCraft(seed.RecipeId, sharedRequest)));

            var resultA = await a.ReadUntilAsync(PacketId.CraftResult);
            var resultB = await b.ReadUntilAsync(PacketId.CraftResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(resultA, out var okA, out _));
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(resultB, out var okB, out _));
            Assert.True(okA);
            Assert.True(okB);

            using var gate = CreateGate();
            var inv = new PostgresInventoryRepository(gate);
            var qtyA = (await inv.GetAsync(idA)).Slots.Where(s => s.ItemId == seed.Phase7.ConsumableId).Sum(s => s.Quantity);
            var qtyB = (await inv.GetAsync(idB)).Slots.Where(s => s.ItemId == seed.Phase7.ConsumableId).Sum(s => s.Quantity);
            Assert.Equal(1, qtyA);
            Assert.Equal(1, qtyB);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CraftConcurrentRetry_SameClient_NoDuplication()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var client = new Phase7TcpTestClient();
            var id = await RegisterAsync(client, port, seed, "Crafter");
            await AcquireProfessionViaNetworkAsync(client, seed);
            await SeedCraftPrerequisitesAsync(id, seed);

            var requestId = Guid.NewGuid();
            await Task.WhenAll(
                client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCraft(seed.RecipeId, requestId)),
                client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCraft(seed.RecipeId, requestId)));

            var result1 = await client.ReadUntilAsync(PacketId.CraftResult);
            var result2 = await client.ReadUntilAsync(PacketId.CraftResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(result1, out var ok1, out _));
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(result2, out var ok2, out _));
            Assert.True(ok1);
            Assert.True(ok2);

            using var gate = CreateGate();
            var inv = new PostgresInventoryRepository(gate);
            var qty = (await inv.GetAsync(id)).Slots.Where(s => s.ItemId == seed.Phase7.ConsumableId).Sum(s => s.Quantity);
            Assert.Equal(1, qty);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task AutorunMapEvent_DoesNotDuplicateOnSecondLogin()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var first = new Phase7TcpTestClient();
            var (token, characterId) = await RegisterReturningTokenAndIdAsync(first, port, seed, "Auto1");
            _ = await first.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            _ = await first.ReadUntilAsync(PacketId.EnvironmentStatePush);
            var autorun1 = await first.ReadUntilAsync(PacketId.InteractResult);
            Assert.Contains("Welcome to Phase8", DecodeInteractMessage(autorun1));

            await first.DisconnectAsync();
            await using var second = new Phase7TcpTestClient();
            await second.ConnectAsync("127.0.0.1", port);
            _ = await second.ReadFrameAsync();
            await second.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            _ = await second.ReadUntilAsync(PacketId.ReconnectResult);
            await second.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            _ = await second.ReadUntilAsync(PacketId.CharacterSelectResult);
            _ = await second.ReadUntilAsync(PacketId.CombatState);
            _ = await second.ReadUntilAsync(PacketId.InventorySnapshot);
            _ = await second.ReadUntilAsync(PacketId.BankSnapshot);
            _ = await second.ReadUntilAsync(PacketId.GroundItemsSnapshot);
            _ = await second.ReadUntilAsync(PacketId.QuestJournalSnapshot);
            _ = await second.ReadUntilAsync(PacketId.EnvironmentStatePush);
            await second.DrainPendingAsync(TimeSpan.FromMilliseconds(300));
            try
            {
                var unexpected = await second.ReadUntilAsync(PacketId.InteractResult, TimeSpan.FromMilliseconds(400));
                Assert.DoesNotContain("Welcome to Phase8", DecodeInteractMessage(unexpected));
            }
            catch (TimeoutException)
            {
                // No interact result is also acceptable on re-login.
            }
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EnvironmentState_ConsistentAcrossClientsOnSameMap()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            var idA = await RegisterAsync(a, port, seed, "WeatherA");
            var idB = await RegisterAsync(b, port, seed, "WeatherB");
            _ = idA;
            _ = idB;
            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(idA.ToString("D")));
            _ = await a.ReadUntilAsync(PacketId.CharacterSelectResult);
            var envA = await a.ReadUntilAsync(PacketId.EnvironmentStatePush);
            await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(idB.ToString("D")));
            _ = await b.ReadUntilAsync(PacketId.CharacterSelectResult);
            var envB = await b.ReadUntilAsync(PacketId.EnvironmentStatePush);
            Assert.True(Phase8WireDecoders.TryDecodeEnvironmentState(envA, out var mapA, out var regionA, out var weatherA, out var lightA));
            Assert.True(Phase8WireDecoders.TryDecodeEnvironmentState(envB, out var mapB, out var regionB, out var weatherB, out var lightB));
            Assert.Equal(mapA, mapB);
            Assert.Equal(regionA, regionB);
            Assert.Equal(weatherA, weatherB);
            Assert.Equal(lightA, lightB);
            Assert.Equal(seed.ExpectedLightingLevel, lightA);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Reconnect_DisplacesStaleConnection()
    {
        var seed = await SeedAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            var user = $"rc-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";
            await using var oldClient = new Phase7TcpTestClient();
            var token = await RegisterLoginCreateAsync(oldClient, port, user, password, "StaleP8", seed.Phase7.ClassId);
            await using var newClient = new Phase7TcpTestClient();
            await newClient.ConnectAsync("127.0.0.1", port);
            _ = await newClient.ReadFrameAsync();
            await newClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await newClient.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await Task.Delay(200);
            await Assert.ThrowsAnyAsync<Exception>(() => oldClient.ReadFrameAsync(TimeSpan.FromMilliseconds(500)));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private async Task PrepareQuestReadyAsync(Phase7TcpTestClient client, Phase8PostgresContentSeedResult seed, Guid characterId)
    {
        // Simultaneous turn-in race: prepare readiness via DB (movement prep collides with multi-client occupancy).
        _ = client;
        using var gate = CreateGate();
        await new PostgresCharacterQuestRepository(gate).UpsertAsync(new CharacterQuestProgress
        {
            CharacterId = characterId,
            QuestId = seed.QuestId,
            Status = CharacterQuestStatus.ReadyToTurnIn,
            StageIndex = 0,
            RewardClaimed = false,
            ObjectiveCounters = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [QuestObjectiveKeys.For(0, 0)] = 1,
            },
        }).ConfigureAwait(false);
    }

    private static async Task<byte[]> OpenGateDialogueAsync(Phase7TcpTestClient client, Phase8PostgresContentSeedResult seed)
    {
        await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.KeyEventTileX, seed.KeyEventTileY);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
        _ = await client.ReadUntilAsync(PacketId.InteractResult);
        await Phase8MovementTestHelpers.TeleportToTileAsync(client, seed.GateEventTileX, seed.GateEventTileY);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
        return await Phase8TcpTestHelpers.ReadDialogueThenInteractAsync(client);
    }

    private static async Task<string> RegisterLoginCreateAsync(
        Phase7TcpTestClient tcp,
        int port,
        string user,
        string password,
        string charName,
        Guid classId)
    {
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
        var login = await tcp.ReadUntilAsync(PacketId.LoginResult);
        var token = Phase7WireDecoders.DecodeLoginToken(login);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, classId));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        return token;
    }

    private async Task SeedCraftPrerequisitesAsync(Guid characterId, Phase8PostgresContentSeedResult seed)
    {
        using var gate = CreateGate();
        await Phase8PostgresContentSeed.SeedInventoryIngredientsAsync(gate, characterId, seed.Phase7.ConsumableId, 2)
            .ConfigureAwait(false);
    }

    private static async Task AcquireProfessionViaNetworkAsync(
        Phase7TcpTestClient client,
        Phase8PostgresContentSeedResult seed)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildAcquireProfession(seed.ProfessionId));
        var result = await client.ReadUntilAsync(PacketId.AcquireProfessionResult);
        Assert.True(Phase8WireDecoders.TryDecodeStatusResult(result, out var ok, out _));
        Assert.True(ok);
    }

    private static async Task SkipPhase8BootstrapAsync(Phase7TcpTestClient client)
    {
        _ = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
        _ = await client.ReadUntilAsync(PacketId.EnvironmentStatePush);
        await client.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
    }

    private static string DecodeInteractMessage(byte[] frame)
    {
        Phase8WireDecoders.TryDecodeInteractResult(frame, out _, out var message);
        return message;
    }

    private async Task<Phase8PostgresContentSeedResult> SeedAsync()
    {
        using var gate = CreateGate();
        return await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<Guid> RegisterAsync(
        Phase7TcpTestClient tcp,
        int port,
        Phase8PostgresContentSeedResult seed,
        string charName)
    {
        var user = $"{charName.ToLowerInvariant()[..2]}-{Guid.NewGuid():N}"[..16];
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, "password12345"));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, "password12345"));
        _ = await tcp.ReadUntilAsync(PacketId.LoginResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, seed.Phase7.ClassId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Phase7WireDecoders.DecodeCharacterId(create);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        await Phase8TcpTestHelpers.DrainAccountSelectSnapshotsAsync(tcp);
        return Guid.Parse(id);
    }

    private static async Task<(string Token, string CharacterId)> RegisterReturningTokenAndIdAsync(
        Phase7TcpTestClient tcp,
        int port,
        Phase8PostgresContentSeedResult seed,
        string charName)
    {
        var user = $"tk-{Guid.NewGuid():N}"[..16];
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, "password12345"));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, "password12345"));
        var login = await tcp.ReadUntilAsync(PacketId.LoginResult);
        var token = Phase7WireDecoders.DecodeLoginToken(login);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, seed.Phase7.ClassId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Phase7WireDecoders.DecodeCharacterId(create);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        _ = await tcp.ReadUntilAsync(PacketId.CombatState);
        _ = await tcp.ReadUntilAsync(PacketId.InventorySnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.BankSnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.GroundItemsSnapshot);
        return (token, id);
    }

    private static async Task<string> RegisterReturningTokenAsync(
        Phase7TcpTestClient tcp,
        int port,
        Phase8PostgresContentSeedResult seed,
        string charName)
    {
        var user = $"tk-{Guid.NewGuid():N}"[..16];
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, "password12345"));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, "password12345"));
        var login = await tcp.ReadUntilAsync(PacketId.LoginResult);
        var token = Phase7WireDecoders.DecodeLoginToken(login);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, seed.Phase7.ClassId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Phase7WireDecoders.DecodeCharacterId(create);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        _ = await tcp.ReadUntilAsync(PacketId.CombatState);
        _ = await tcp.ReadUntilAsync(PacketId.InventorySnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.BankSnapshot);
        _ = await tcp.ReadUntilAsync(PacketId.GroundItemsSnapshot);
        return token;
    }
}
