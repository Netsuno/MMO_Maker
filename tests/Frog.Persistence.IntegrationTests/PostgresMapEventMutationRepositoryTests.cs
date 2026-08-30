using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresMapEventMutationRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMapEventMutationRepositoryTests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Execute_OnceGiveItem_PreCommitFailure_RollsBackSwitchAndInventory()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresMapEventMutationRepository(gate, items)
        {
            TestAfterClaimAsync = _ => throw new InvalidOperationException("injected-after-claim"),
        };

        var commands = new[]
        {
            new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.GiveItem,
                SchemaVersion = 1,
                ParameterJson =
                    $"{{\"itemId\":\"{seed.Phase7.ConsumableId}\",\"quantity\":1,\"onceKey\":\"{Phase8PostgresContentSeed.OnceRewardOnceKey}\"}}",
            },
        };

        var result = await repo.TryExecutePageAsync(
            characterId,
            Guid.NewGuid(),
            placementId: 99,
            catalogAliasId: Phase8PostgresContentSeed.OnceRewardMapEventAliasId,
            commands);

        Assert.Equal(MapEventMutationStatus.Failed, result.Status);

        using var gate2 = CreateGate();
        var world = new PostgresCharacterWorldStateRepository(gate2);
        var switchKey = MapEventOnceGrantKeys.SwitchKeyFor(Phase8PostgresContentSeed.OnceRewardOnceKey);
        Assert.NotEqual(true, await world.GetSwitchAsync(characterId, switchKey));

        var inv = new PostgresInventoryRepository(gate2);
        var qty = (await inv.GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(0, qty);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Execute_MultiCommand_LaterFailure_RollsBackEarlierMutations()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresMapEventMutationRepository(gate, items);

        var commands = new[]
        {
            new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.SetSwitch,
                SchemaVersion = 1,
                ParameterJson = $"{{\"switchId\":\"{Phase8PostgresContentSeed.GateSwitchId}\",\"value\":true}}",
            },
            new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.GiveItem,
                SchemaVersion = 1,
                ParameterJson = $"{{\"itemId\":\"{Guid.Empty}\",\"quantity\":1}}",
            },
        };

        var result = await repo.TryExecutePageAsync(
            characterId,
            Guid.NewGuid(),
            placementId: 100,
            catalogAliasId: 1,
            commands);

        Assert.Equal(MapEventMutationStatus.Failed, result.Status);

        using var gate2 = CreateGate();
        var world = new PostgresCharacterWorldStateRepository(gate2);
        Assert.NotEqual(true, await world.GetSwitchAsync(characterId, Phase8PostgresContentSeed.GateSwitchId));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Execute_IdempotentReplay_ReturnsSameOutcomeWithoutDuplicateItem()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresMapEventMutationRepository(gate, items);
        var requestId = Guid.NewGuid();
        var commands = new[]
        {
            new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.GiveItem,
                SchemaVersion = 1,
                ParameterJson =
                    $"{{\"itemId\":\"{seed.Phase7.ConsumableId}\",\"quantity\":1,\"onceKey\":\"replay-once\"}}",
            },
        };

        var first = await repo.TryExecutePageAsync(characterId, requestId, 101, 1, commands);
        Assert.Equal(MapEventMutationStatus.Executed, first.Status);

        var replay = await repo.TryExecutePageAsync(characterId, requestId, 101, 1, commands);
        Assert.Equal(MapEventMutationStatus.IdempotentReplay, replay.Status);

        using var gate2 = CreateGate();
        var qty = (await new PostgresInventoryRepository(gate2).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, qty);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Execute_RetryAfterRollback_SucceedsNormally()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresMapEventMutationRepository(gate, items);
        var injectOnce = 0;
        repo.TestBeforeCommitAsync = _ =>
        {
            if (Interlocked.CompareExchange(ref injectOnce, 1, 0) == 0)
            {
                throw new InvalidOperationException("injected-before-commit");
            }

            return Task.CompletedTask;
        };

        var commands = new[]
        {
            new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.GiveItem,
                SchemaVersion = 1,
                ParameterJson = $"{{\"itemId\":\"{seed.Phase7.ConsumableId}\",\"quantity\":1}}",
            },
        };

        var failed = await repo.TryExecutePageAsync(characterId, Guid.NewGuid(), 102, 1, commands);
        Assert.Equal(MapEventMutationStatus.Failed, failed.Status);

        var retry = await repo.TryExecutePageAsync(characterId, Guid.NewGuid(), 102, 1, commands);
        Assert.Equal(MapEventMutationStatus.Executed, retry.Status);

        using var gate2 = CreateGate();
        var qty = (await new PostgresInventoryRepository(gate2).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, qty);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<Guid> CreateCharacterAsync(FrogDbContextGate gate, Phase8PostgresContentSeedResult seed)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"mut-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var result = await chars.CreateAsync(
            created.AccountId!.Value,
            "MutHero",
            seed.Phase7.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            seed.Phase7.SpellId,
            1,
            32,
            48);
        Assert.Equal(CharacterCreateStatus.Created, result.Status);
        return result.Character!.Id;
    }
}
