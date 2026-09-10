using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

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

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ExecutePlan_FailedPlan_DoesNotWriteLedger()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.Create(characterId, 200, 1);
        var plan = MapEventExecutionPlan.Fail(identity, "plan-error-from-core");
        var repo = CreateRepo(gate);

        var result = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Failed, result.Status);
        Assert.Equal("plan-error-from-core", result.ErrorMessage);
        Assert.Equal(0, repo.TransactionsBegun);
        Assert.Equal(0, await CountLedgerRowsAsync(gate, identity.LedgerKey));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ExecutePlan_BranchLeftInEffects_FailsWithoutSqlResolution()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.Create(characterId, 201, 1);
        var plan = MapEventExecutionPlan.Ok(
            identity,
            [
                Cmd(MapEventCommandDiscriminators.SetSwitch, $"{{\"switchId\":\"{Phase8PostgresContentSeed.GateSwitchId}\",\"value\":true}}"),
                Cmd(MapEventCommandDiscriminators.Branch, """{"condition":{"kind":"always"},"then":[],"else":[]}"""),
            ]);
        var repo = CreateRepo(gate);

        var result = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Failed, result.Status);
        Assert.Contains("re-résolution", result.ErrorMessage, StringComparison.Ordinal);

        using var gate2 = CreateGate();
        Assert.NotEqual(true, await new PostgresCharacterWorldStateRepository(gate2)
            .GetSwitchAsync(characterId, Phase8PostgresContentSeed.GateSwitchId));
        Assert.Equal(0, await CountLedgerRowsAsync(gate2, identity.LedgerKey));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ExecutePlan_FullEffects_MidFailure_RollsBackAllDomains()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.Create(characterId, 202, 1);
        var effects = FullPersistentEffects(seed)
            .Append(Cmd(MapEventCommandDiscriminators.GiveItem, $"{{\"itemId\":\"{Guid.Empty}\",\"quantity\":1}}"))
            .ToArray();
        var plan = MapEventExecutionPlan.Ok(identity, effects);
        Assert.Equal((characterId, identity.RequestId), plan.Identity.LedgerKey);

        var repo = CreateRepo(gate);
        var result = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Failed, result.Status);
        Assert.Equal(1, repo.TransactionsBegun);

        await AssertNoPersistentEffectsAsync(characterId, seed, identity.LedgerKey);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ExecutePlan_RetryAfterFullRollback_SucceedsWithoutPoison()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var failedIdentity = MapEventExecutionIdentity.Create(characterId, 203, 1);
        var retryIdentity = MapEventExecutionIdentity.Create(characterId, 203, 1);
        var effects = FullPersistentEffects(seed).ToArray();
        var repo = CreateRepo(gate);
        var injectOnce = 0;
        repo.TestBeforeCommitAsync = _ =>
        {
            if (Interlocked.CompareExchange(ref injectOnce, 1, 0) == 0)
            {
                throw new InvalidOperationException("injected-before-commit-full");
            }

            return Task.CompletedTask;
        };

        var failed = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(failedIdentity, effects));
        Assert.Equal(MapEventMutationStatus.Failed, failed.Status);
        await AssertNoPersistentEffectsAsync(characterId, seed, failedIdentity.LedgerKey);

        var retry = await repo.TryExecutePlanAsync(MapEventExecutionPlan.Ok(retryIdentity, effects));
        Assert.Equal(MapEventMutationStatus.Executed, retry.Status);
        Assert.Equal(2, repo.TransactionsBegun);
        await AssertFullPersistentEffectsAsync(characterId, seed, retryIdentity.LedgerKey, expectedItemQty: 1);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ExecutePlan_ReplayAfterCommit_IdempotentNoDoubleEffects()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        var identity = MapEventExecutionIdentity.Create(characterId, 204, 1);
        Assert.Equal((characterId, identity.RequestId), identity.LedgerKey);
        var plan = MapEventExecutionPlan.Ok(identity, FullPersistentEffects(seed).ToArray());
        var repo = CreateRepo(gate);

        var first = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Executed, first.Status);
        Assert.Equal(1, repo.TransactionsBegun);
        Assert.True(first.Snapshot!.QuestsChanged);
        Assert.True(first.Snapshot.ProfessionsChanged);
        Assert.True(first.Snapshot.RecipesChanged);

        var replay = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.IdempotentReplay, replay.Status);
        Assert.Equal(1, repo.TransactionsBegun);
        Assert.True(replay.Snapshot!.QuestsChanged);
        Assert.True(replay.Snapshot.ProfessionsChanged);

        await AssertFullPersistentEffectsAsync(characterId, seed, identity.LedgerKey, expectedItemQty: 1);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ExecutePlan_ConcurrentSameLedgerKey_ExactlyOneGrant()
    {
        using var seedGate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(seedGate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(seedGate, seed);
        var identity = MapEventExecutionIdentity.Create(characterId, 205, 1);
        var plan = MapEventExecutionPlan.Ok(
            identity,
            [
                Cmd(MapEventCommandDiscriminators.GiveItem,
                    $"{{\"itemId\":\"{seed.Phase7.ConsumableId}\",\"quantity\":1}}"),
                Cmd(MapEventCommandDiscriminators.GiveGold, """{"amount":25}"""),
                Cmd(MapEventCommandDiscriminators.SetSwitch,
                    $"{{\"switchId\":\"{Phase8PostgresContentSeed.GateSwitchId}\",\"value\":true}}"),
            ]);

        using var gateA = CreateGate();
        using var gateB = CreateGate();
        var results = await Task.WhenAll(
            CreateRepo(gateA).TryExecutePlanAsync(plan),
            CreateRepo(gateB).TryExecutePlanAsync(plan));

        Assert.Equal(1, results.Count(r => r.Status == MapEventMutationStatus.Executed));
        Assert.Equal(1, results.Count(r => r.Status == MapEventMutationStatus.IdempotentReplay));
        Assert.All(results, r => Assert.NotEqual(MapEventMutationStatus.Failed, r.Status));

        using var verify = CreateGate();
        var qty = (await new PostgresInventoryRepository(verify).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, qty);
        var gold = (await new PostgresCharacterRepository(verify).FindByIdAsync(characterId))!.Gold;
        Assert.Equal(GameplayLimits.StartingGold + 25, gold);
        Assert.Equal(true, await new PostgresCharacterWorldStateRepository(verify)
            .GetSwitchAsync(characterId, Phase8PostgresContentSeed.GateSwitchId));
        Assert.Equal(1, await CountLedgerRowsAsync(verify, identity.LedgerKey));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ExecutePlan_TurnInQuest_AtomicRewardsAndReplay()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await MarkQuestReadyAsync(gate, characterId, seed.QuestId).ConfigureAwait(false);
        var identity = MapEventExecutionIdentity.Create(characterId, 206, 1);
        var plan = MapEventExecutionPlan.Ok(
            identity,
            [Cmd(MapEventCommandDiscriminators.TurnInQuest, $"{{\"questId\":\"{seed.QuestId}\"}}")]);
        var repo = CreateRepo(gate);

        var first = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.Executed, first.Status);
        Assert.True(first.Snapshot!.QuestsChanged);
        Assert.True(first.Snapshot.GoldChanged);
        Assert.True(first.Snapshot.InventoryChanged);

        var replay = await repo.TryExecutePlanAsync(plan);
        Assert.Equal(MapEventMutationStatus.IdempotentReplay, replay.Status);

        using var verify = CreateGate();
        var gold = (await new PostgresCharacterRepository(verify).FindByIdAsync(characterId))!.Gold;
        Assert.Equal(GameplayLimits.StartingGold + seed.QuestRewardGold, gold);
        var qty = (await new PostgresInventoryRepository(verify).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, qty);
        var progress = await new PostgresCharacterQuestRepository(verify).TryGetAsync(characterId, seed.QuestId);
        Assert.Equal(CharacterQuestStatus.Completed, progress!.Status);
        Assert.True(progress.RewardClaimed);
        Assert.Equal(1, await CountLedgerRowsAsync(verify, identity.LedgerKey));
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static PostgresMapEventMutationRepository CreateRepo(FrogDbContextGate gate)
    {
        var catalogs = new PostgresPhase8PublishedCatalogs(gate);
        return new PostgresMapEventMutationRepository(
            gate,
            new PostgresItemRepository(gate),
            catalogs,
            catalogs,
            catalogs);
    }

    private static MapEventCommandDefinition Cmd(string discriminator, string json) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = json,
        };

    private static IEnumerable<MapEventCommandDefinition> FullPersistentEffects(Phase8PostgresContentSeedResult seed)
    {
        yield return Cmd(
            MapEventCommandDiscriminators.SetSwitch,
            $"{{\"switchId\":\"{Phase8PostgresContentSeed.GateSwitchId}\",\"value\":true}}");
        yield return Cmd(
            MapEventCommandDiscriminators.SetVariable,
            """{"variableId":"score","value":4}""");
        yield return Cmd(
            MapEventCommandDiscriminators.AddVariable,
            """{"variableId":"score","delta":6}""");
        yield return Cmd(
            MapEventCommandDiscriminators.GiveItem,
            $"{{\"itemId\":\"{seed.Phase7.ConsumableId}\",\"quantity\":1}}");
        yield return Cmd(
            MapEventCommandDiscriminators.GiveGold,
            """{"amount":15}""");
        yield return Cmd(
            MapEventCommandDiscriminators.StartQuest,
            $"{{\"questId\":\"{seed.QuestId}\"}}");
        yield return Cmd(
            MapEventCommandDiscriminators.LearnProfession,
            $"{{\"professionId\":\"{seed.ProfessionId}\"}}");
    }

    private async Task AssertNoPersistentEffectsAsync(
        Guid characterId,
        Phase8PostgresContentSeedResult seed,
        (Guid CharacterId, Guid RequestId) ledgerKey)
    {
        using var verify = CreateGate();
        Assert.NotEqual(true, await new PostgresCharacterWorldStateRepository(verify)
            .GetSwitchAsync(characterId, Phase8PostgresContentSeed.GateSwitchId));
        Assert.Null(await new PostgresCharacterWorldStateRepository(verify)
            .GetVariableAsync(characterId, "score"));
        var qty = (await new PostgresInventoryRepository(verify).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(0, qty);
        var gold = (await new PostgresCharacterRepository(verify).FindByIdAsync(characterId))!.Gold;
        Assert.Equal(GameplayLimits.StartingGold, gold);
        Assert.Null(await new PostgresCharacterQuestRepository(verify).TryGetAsync(characterId, seed.QuestId));
        Assert.Null(await new PostgresCharacterProfessionRepository(verify).TryGetAsync(characterId, seed.ProfessionId));
        Assert.Equal(0, await CountLedgerRowsAsync(verify, ledgerKey));
    }

    private async Task AssertFullPersistentEffectsAsync(
        Guid characterId,
        Phase8PostgresContentSeedResult seed,
        (Guid CharacterId, Guid RequestId) ledgerKey,
        int expectedItemQty)
    {
        using var verify = CreateGate();
        Assert.Equal(true, await new PostgresCharacterWorldStateRepository(verify)
            .GetSwitchAsync(characterId, Phase8PostgresContentSeed.GateSwitchId));
        Assert.Equal(10, await new PostgresCharacterWorldStateRepository(verify)
            .GetVariableAsync(characterId, "score"));
        var qty = (await new PostgresInventoryRepository(verify).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(expectedItemQty, qty);
        var gold = (await new PostgresCharacterRepository(verify).FindByIdAsync(characterId))!.Gold;
        Assert.Equal(GameplayLimits.StartingGold + 15, gold);
        var quest = await new PostgresCharacterQuestRepository(verify).TryGetAsync(characterId, seed.QuestId);
        Assert.Equal(CharacterQuestStatus.Active, quest!.Status);
        var profession = await new PostgresCharacterProfessionRepository(verify)
            .TryGetAsync(characterId, seed.ProfessionId);
        Assert.Equal(1, profession!.Level);
        IPublishedRecipeCatalog recipes = new PostgresPhase8PublishedCatalogs(verify);
        var recipe = await recipes.TryGetPublishedByIdAsync(seed.RecipeId);
        Assert.NotNull(recipe);
        Assert.Equal(seed.ProfessionId, recipe!.ProfessionId);
        Assert.True(profession.Level >= recipe.RequiredProfessionLevel);
        Assert.Equal(1, await CountLedgerRowsAsync(verify, ledgerKey));
    }

    private static async Task MarkQuestReadyAsync(FrogDbContextGate gate, Guid characterId, Guid questId)
    {
        await new PostgresCharacterQuestRepository(gate).UpsertAsync(new CharacterQuestProgress
        {
            CharacterId = characterId,
            QuestId = questId,
            Status = CharacterQuestStatus.ReadyToTurnIn,
            StageIndex = 0,
            RewardClaimed = false,
            ObjectiveCounters = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [QuestObjectiveKeys.For(0, 0)] = 1,
            },
        }).ConfigureAwait(false);
    }

    private static async Task<int> CountLedgerRowsAsync(
        FrogDbContextGate gate,
        (Guid CharacterId, Guid RequestId) ledgerKey)
    {
        return await gate.ExecuteAsync(async (db, ct) =>
            await db.PlayerMapEventExecutionRequests.CountAsync(
                r => r.CharacterId == ledgerKey.CharacterId && r.RequestId == ledgerKey.RequestId,
                ct),
            CancellationToken.None).ConfigureAwait(false);
    }

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
