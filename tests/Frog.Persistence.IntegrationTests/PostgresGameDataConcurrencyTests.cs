using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresGameDataConcurrencyTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresGameDataConcurrencyTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SharedGate_RapidParallelRepositoryReads_DoNotThrow()
    {
        using var gate = CreateGate();
        var tilesets = new PostgresTilesetRepository(gate);
        var items = new PostgresItemRepository(gate);
        var maps = new PostgresMapRepository(gate);

        var tasks = new List<Task>();
        for (var i = 0; i < 30; i++)
        {
            tasks.Add(tilesets.ListSummariesAsync());
            tasks.Add(items.ListSummariesAsync(null, null));
            tasks.Add(maps.ListSummariesAsync());
        }

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks)).ConfigureAwait(false);
        Assert.Null(exception);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ResourceSpawnSession_RapidFilterChanges_DoNotThrow()
    {
        using var gate = CreateGate();
        var mapRepo = new PostgresMapRepository(gate);
        var itemRepo = new PostgresItemRepository(gate);
        var resourceRepo = new PostgresResourceRepository(gate, itemRepo);
        var spawnRepo = new PostgresResourceSpawnRepository(gate, mapRepo, resourceRepo);

        var map = DemoMapFactory.CreateStarter("Concurrency map");
        var savedMap = Assert.IsType<SaveMapResult.Success>(
            await mapRepo.SaveAsync(
                new SaveMapRequest
                {
                    Map = map,
                    ExpectedRevision = 0,
                    Intent = SaveMapIntent.SaveDraft,
                }).ConfigureAwait(false));

        var yieldItemId = await PublishItemAsync(itemRepo, "Yield Concurrency").ConfigureAwait(false);
        var toolItemId = await PublishItemAsync(itemRepo, "Tool Concurrency").ConfigureAwait(false);
        var resourceDef = CreateResource(yieldItemId, toolItemId, "Tree Concurrency");
        var savedResource = Assert.IsType<SaveResourceResult.Success>(
            await resourceRepo.SaveAsync(
                new SaveResourceRequest
                {
                    Definition = resourceDef,
                    ExpectedRevision = 0,
                    Intent = SaveContentIntent.Publish,
                }).ConfigureAwait(false));

        var session = new ResourceSpawnWorkspaceSession(spawnRepo);
        for (var i = 0; i < 40; i++)
        {
            session.MapFilter = i % 2 == 0 ? savedMap.MapId : null;
            session.ResourceFilter = i % 3 == 0 ? savedResource.ResourceId : null;
            session.StatusFilter = (i % 4) switch
            {
                0 => ContentPublishStatus.Draft,
                1 => ContentPublishStatus.Published,
                _ => null,
            };

            var exception = await Record.ExceptionAsync(() => session.RefreshCatalogAsync()).ConfigureAwait(false);
            Assert.Null(exception);
        }

        await gate.DrainAsync().ConfigureAwait(false);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<Guid> PublishItemAsync(PostgresItemRepository repository, string name)
    {
        var published = Assert.IsType<SaveItemResult.Success>(
            await repository.SaveAsync(
                new SaveItemRequest
                {
                    Definition = CreateItem(name),
                    ExpectedRevision = 0,
                    Intent = SaveContentIntent.Publish,
                }).ConfigureAwait(false));
        return published.ItemId;
    }

    private static ItemDefinition CreateItem(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = ItemType.Quest,
        IconLogicalPath = $"icons/items/{Guid.NewGuid():N}.png",
        MaxStack = 99,
        BuyPrice = 0,
        SellPrice = 1,
    };

    private static ResourceDefinition CreateResource(
        Guid yieldItemId,
        Guid? toolItemId,
        string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Concurrency resource",
        SpriteLogicalPath = $"sprites/resources/{Guid.NewGuid():N}.png",
        RespawnSeconds = 30,
        ToolItemId = toolItemId,
        YieldItemId = yieldItemId,
        YieldQuantity = 2,
    };
}
