using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Gameplay;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresQuestMutationRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresQuestMutationRepositoryTests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task TurnIn_GrantsRewardOnce_IdempotentReplayOnRetry()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await MarkQuestReadyAsync(gate, characterId, seed.QuestId).ConfigureAwait(false);

        var quests = new PostgresPhase8PublishedCatalogs(gate);
        var items = new PostgresItemRepository(gate);
        IPublishedQuestCatalog questCatalog = quests;
        IPublishedItemCatalog itemCatalog = items;
        var repo = new PostgresQuestMutationRepository(gate, questCatalog, itemCatalog);
        var requestId = Guid.NewGuid();

        var first = await repo.TryTurnInAsync(characterId, seed.QuestId, requestId);
        Assert.Equal(QuestTurnInStatus.TurnedIn, first.Status);
        Assert.Equal(seed.QuestRewardGold, first.GoldGranted);

        var replay = await repo.TryTurnInAsync(characterId, seed.QuestId, requestId);
        Assert.Equal(QuestTurnInStatus.IdempotentReplay, replay.Status);

        using var gate2 = CreateGate();
        var chars = new PostgresCharacterRepository(gate2);
        var gold = (await chars.FindByIdAsync(characterId))!.Gold;
        Assert.Equal(GameplayLimits.StartingGold + seed.QuestRewardGold, gold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task TurnIn_ConcurrentRequests_ExactlyOneReward()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await MarkQuestReadyAsync(gate, characterId, seed.QuestId).ConfigureAwait(false);

        var quests = new PostgresPhase8PublishedCatalogs(gate);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresQuestMutationRepository(gate, quests, items);
        var requestId = Guid.NewGuid();

        var results = await Task.WhenAll(
            repo.TryTurnInAsync(characterId, seed.QuestId, requestId),
            repo.TryTurnInAsync(characterId, seed.QuestId, requestId));

        var successes = results.Count(r => r.Status is QuestTurnInStatus.TurnedIn or QuestTurnInStatus.IdempotentReplay);
        Assert.Equal(2, successes);
        Assert.All(results, r => Assert.NotEqual(QuestTurnInStatus.Failed, r.Status));

        using var gate2 = CreateGate();
        var gold = (await new PostgresCharacterRepository(gate2).FindByIdAsync(characterId))!.Gold;
        Assert.Equal(GameplayLimits.StartingGold + seed.QuestRewardGold, gold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task TurnIn_PreCommitFailure_RollsBack()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await MarkQuestReadyAsync(gate, characterId, seed.QuestId).ConfigureAwait(false);

        var quests = new PostgresPhase8PublishedCatalogs(gate);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresQuestMutationRepository(gate, quests, items)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected"),
        };

        var failed = await repo.TryTurnInAsync(characterId, seed.QuestId, Guid.NewGuid());
        Assert.Equal(QuestTurnInStatus.Failed, failed.Status);
        repo.TestBeforeCommitAsync = null;

        using var gate2 = CreateGate();
        var progress = await new PostgresCharacterQuestRepository(gate2).TryGetAsync(characterId, seed.QuestId);
        Assert.Equal(CharacterQuestStatus.ReadyToTurnIn, progress!.Status);
        Assert.False(progress.RewardClaimed);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task TurnIn_IdempotentReplay_PersistsAcrossRestartGate()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await MarkQuestReadyAsync(gate, characterId, seed.QuestId).ConfigureAwait(false);

        var requestId = Guid.NewGuid();
        var quests = new PostgresPhase8PublishedCatalogs(gate);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresQuestMutationRepository(gate, quests, items);
        Assert.Equal(QuestTurnInStatus.TurnedIn, (await repo.TryTurnInAsync(characterId, seed.QuestId, requestId)).Status);

        using var gate2 = CreateGate();
        var replayRepo = new PostgresQuestMutationRepository(
            gate2,
            new PostgresPhase8PublishedCatalogs(gate2),
            new PostgresItemRepository(gate2));
        var replay = await replayRepo.TryTurnInAsync(characterId, seed.QuestId, requestId);
        Assert.Equal(QuestTurnInStatus.IdempotentReplay, replay.Status);
    }

    private static async Task MarkQuestReadyAsync(FrogDbContextGate gate, Guid characterId, Guid questId)
    {
        var progress = new PostgresCharacterQuestRepository(gate);
        await progress.UpsertAsync(new CharacterQuestProgress
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

    private static async Task<Guid> CreateCharacterAsync(FrogDbContextGate gate, Phase8PostgresContentSeedResult seed)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"qm-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var result = await chars.CreateAsync(
            created.AccountId!.Value,
            "QuestMutHero",
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

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
}
