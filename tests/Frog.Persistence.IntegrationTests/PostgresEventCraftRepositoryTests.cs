using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Gameplay;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresEventCraftRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresEventCraftRepositoryTests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Craft_ConsumesIngredientsOnce_IdempotentReplayOnRetry()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await Phase8PostgresContentSeed.SeedInventoryIngredientsAsync(gate, characterId, seed.Phase7.ConsumableId, 2)
            .ConfigureAwait(false);

        var recipes = new PostgresPhase8PublishedCatalogs(gate);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresEventCraftRepository(gate, recipes, items);
        var requestId = Guid.NewGuid();

        var first = await repo.TryCraftAsync(characterId, seed.RecipeId, requestId);
        Assert.Equal(EventCraftStatus.Crafted, first.Status);

        var replay = await repo.TryCraftAsync(characterId, seed.RecipeId, requestId);
        Assert.Equal(EventCraftStatus.IdempotentReplay, replay.Status);

        using var gate2 = CreateGate();
        var inv = new PostgresInventoryRepository(gate2);
        var qty = (await inv.GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, qty);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Craft_ConcurrentRequests_ExactlyOneCraft()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await Phase8PostgresContentSeed.SeedInventoryIngredientsAsync(gate, characterId, seed.Phase7.ConsumableId, 2)
            .ConfigureAwait(false);

        var recipes = new PostgresPhase8PublishedCatalogs(gate);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresEventCraftRepository(gate, recipes, items);
        var requestA = Guid.NewGuid();
        var requestB = Guid.NewGuid();

        var results = await Task.WhenAll(
            repo.TryCraftAsync(characterId, seed.RecipeId, requestA),
            repo.TryCraftAsync(characterId, seed.RecipeId, requestB));

        Assert.Equal(1, results.Count(r => r.Status == EventCraftStatus.Crafted));
        Assert.Equal(2, results.Count(r => r.Status is EventCraftStatus.Crafted or EventCraftStatus.InsufficientIngredients));

        using var gate2 = CreateGate();
        var qty = (await new PostgresInventoryRepository(gate2).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, qty);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Craft_PreCommitFailure_RollsBack()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await Phase8PostgresContentSeed.SeedInventoryIngredientsAsync(gate, characterId, seed.Phase7.ConsumableId, 2)
            .ConfigureAwait(false);

        var recipes = new PostgresPhase8PublishedCatalogs(gate);
        var items = new PostgresItemRepository(gate);
        var repo = new PostgresEventCraftRepository(gate, recipes, items)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected"),
        };

        var failed = await repo.TryCraftAsync(characterId, seed.RecipeId, Guid.NewGuid());
        Assert.Equal(EventCraftStatus.Failed, failed.Status);
        repo.TestBeforeCommitAsync = null;

        using var gate2 = CreateGate();
        var qty = (await new PostgresInventoryRepository(gate2).GetAsync(characterId)).Slots
            .Where(s => s.ItemId == seed.Phase7.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(2, qty);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Craft_IdempotentReplay_PersistsAcrossRestartGate()
    {
        using var gate = CreateGate();
        var seed = await Phase8PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        var characterId = await CreateCharacterAsync(gate, seed);
        await Phase8PostgresContentSeed.SeedInventoryIngredientsAsync(gate, characterId, seed.Phase7.ConsumableId, 2)
            .ConfigureAwait(false);

        var requestId = Guid.NewGuid();
        var repo = new PostgresEventCraftRepository(
            gate,
            new PostgresPhase8PublishedCatalogs(gate),
            new PostgresItemRepository(gate));
        Assert.Equal(EventCraftStatus.Crafted, (await repo.TryCraftAsync(characterId, seed.RecipeId, requestId)).Status);

        using var gate2 = CreateGate();
        var replayRepo = new PostgresEventCraftRepository(
            gate2,
            new PostgresPhase8PublishedCatalogs(gate2),
            new PostgresItemRepository(gate2));
        var replay = await replayRepo.TryCraftAsync(characterId, seed.RecipeId, requestId);
        Assert.Equal(EventCraftStatus.IdempotentReplay, replay.Status);
    }

    private static async Task<Guid> CreateCharacterAsync(FrogDbContextGate gate, Phase8PostgresContentSeedResult seed)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"cr-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var result = await chars.CreateAsync(
            created.AccountId!.Value,
            "CraftHero",
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
