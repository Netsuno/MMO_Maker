using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresResourceRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresResourceRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ResourcesAndSpawns_DraftPublish_Refs_InvalidPublish_ReloadAndRollback()
    {
        await using var db = CreateDb();
        var items = new PostgresItemRepository(db);
        var yieldItemId = await PublishItemAsync(items, "Bois PG");
        var toolItemId = await PublishItemAsync(items, "Hache PG");
        var resources = new PostgresResourceRepository(db, items);
        var definition = CreateResource(yieldItemId, toolItemId, "Chêne PG");

        var draft = Assert.IsType<SaveResourceResult.Success>(await resources.SaveAsync(
            new SaveResourceRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        definition.Description = "Ressource publiée PG";
        var published = Assert.IsType<SaveResourceResult.Success>(await resources.SaveAsync(
            new SaveResourceRequest
            {
                ResourceId = draft.ResourceId,
                Definition = definition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.Equal(2, published.PublishedRevision);

        definition.Description = "Brouillon distinct PG";
        definition.RespawnSeconds = 45;
        Assert.IsType<SaveResourceResult.Success>(await resources.SaveAsync(
            new SaveResourceRequest
            {
                ResourceId = draft.ResourceId,
                Definition = definition,
                ExpectedRevision = 2,
                Intent = SaveContentIntent.SaveDraft,
            }));

        await using var db2 = CreateDb();
        var resources2 = new PostgresResourceRepository(db2);
        Assert.Equal(
            "Brouillon distinct PG",
            (await resources2.LoadByIdAsync(draft.ResourceId))!.Definition.Description);
        var resourceSnapshot =
            (await resources2.LoadPublishedByIdAsync(draft.ResourceId))!.Definition;
        Assert.Equal("Ressource publiée PG", resourceSnapshot.Description);
        Assert.Equal(30, resourceSnapshot.RespawnSeconds);
        Assert.IsType<SaveResourceResult.Conflict>(await resources2.SaveAsync(
            new SaveResourceRequest
            {
                ResourceId = draft.ResourceId,
                Definition = definition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.SaveDraft,
            }));

        var invalidResource = CreateResource(Guid.NewGuid(), name: "Référence invalide PG");
        Assert.IsType<SaveResourceResult.ValidationFailed>(await resources2.SaveAsync(
            new SaveResourceRequest
            {
                Definition = invalidResource,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        var draftItem = CreateItem("Objet brouillon PG");
        var draftItemSaved = Assert.IsType<SaveItemResult.Success>(
            await new PostgresItemRepository(db2).SaveAsync(new SaveItemRequest
            {
                Definition = draftItem,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        invalidResource.YieldItemId = draftItemSaved.ItemId;
        Assert.IsType<SaveResourceResult.ValidationFailed>(await resources2.SaveAsync(
            new SaveResourceRequest
            {
                Definition = invalidResource,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        var maps = new PostgresMapRepository(db2);
        var mapId = await SaveMapAsync(maps);
        var spawns = new PostgresResourceSpawnRepository(db2, maps, resources2);
        var spawnDefinition = CreateSpawn(mapId, draft.ResourceId, 2, 3);
        var spawnDraft = Assert.IsType<SaveResourceSpawnResult.Success>(
            await spawns.SaveAsync(new SaveResourceSpawnRequest
            {
                Definition = spawnDefinition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        spawnDefinition.TileX = 4;
        var spawnPublished = Assert.IsType<SaveResourceSpawnResult.Success>(
            await spawns.SaveAsync(new SaveResourceSpawnRequest
            {
                SpawnId = spawnDraft.SpawnId,
                Definition = spawnDefinition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.Equal(2, spawnPublished.PublishedRevision);

        spawnDefinition.TileX = 7;
        Assert.IsType<SaveResourceSpawnResult.Success>(await spawns.SaveAsync(
            new SaveResourceSpawnRequest
            {
                SpawnId = spawnDraft.SpawnId,
                Definition = spawnDefinition,
                ExpectedRevision = 2,
                Intent = SaveContentIntent.SaveDraft,
            }));

        await using var db3 = CreateDb();
        var spawns3 = new PostgresResourceSpawnRepository(db3);
        Assert.Equal(7, (await spawns3.LoadByIdAsync(spawnDraft.SpawnId))!.Definition.TileX);
        Assert.Equal(
            4,
            (await spawns3.LoadPublishedByIdAsync(spawnDraft.SpawnId))!.Definition.TileX);
        Assert.Single(await spawns3.ListPublishedAsync(mapId));

        Assert.IsType<SaveResourceSpawnResult.ValidationFailed>(await spawns3.SaveAsync(
            new SaveResourceSpawnRequest
            {
                Definition = CreateSpawn(Guid.NewGuid(), draft.ResourceId, 0, 0),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.IsType<SaveResourceSpawnResult.ValidationFailed>(await spawns3.SaveAsync(
            new SaveResourceSpawnRequest
            {
                Definition = CreateSpawn(mapId, Guid.NewGuid(), 0, 0),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        var itemDeleteRepository = new PostgresItemRepository(db3);
        Assert.IsType<DeleteItemResult.Referenced>(
            await itemDeleteRepository.DeleteAsync(toolItemId));
        var resourceDeleteRepository = new PostgresResourceRepository(db3);
        Assert.IsType<DeleteResourceResult.Referenced>(
            await resourceDeleteRepository.DeleteAsync(draft.ResourceId));

        var beforeResourceCount = (await resources2.ListSummariesAsync()).Count;
        await using var db4 = CreateDb();
        var failingResource = new PostgresResourceRepository(db4)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("resource-fail"),
        };
        var failedResource = await failingResource.SaveAsync(new SaveResourceRequest
        {
            Definition = CreateResource(yieldItemId, name: "Ressource rollback PG"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveResourceResult.PersistenceFailed>(failedResource);

        await using var db5 = CreateDb();
        var afterResourceRollback = new PostgresResourceRepository(db5);
        Assert.Empty(await afterResourceRollback.ListSummariesAsync("Ressource rollback PG"));
        Assert.Equal(beforeResourceCount, (await afterResourceRollback.ListSummariesAsync()).Count);

        var failingSpawn = new PostgresResourceSpawnRepository(db5)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("spawn-fail"),
        };
        var beforeSpawnCount = (await failingSpawn.ListSummariesAsync()).Count;
        var failedSpawn = await failingSpawn.SaveAsync(new SaveResourceSpawnRequest
        {
            Definition = CreateSpawn(mapId, draft.ResourceId, 8, 8),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveResourceSpawnResult.PersistenceFailed>(failedSpawn);

        await using var db6 = CreateDb();
        var afterSpawnRollback = new PostgresResourceSpawnRepository(db6);
        Assert.Equal(beforeSpawnCount, (await afterSpawnRollback.ListSummariesAsync()).Count);
        Assert.DoesNotContain(
            await afterSpawnRollback.ListPublishedAsync(mapId),
            spawn => spawn.TileX == 8 && spawn.TileY == 8);
    }

    private FrogDbContext CreateDb()
        => new(FrogDbContextOptions.Create(_fixture.ConnectionString));

    private static async Task<Guid> PublishItemAsync(
        PostgresItemRepository repository,
        string name)
    {
        var published = Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(
            new SaveItemRequest
            {
                Definition = CreateItem(name),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
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
        Guid? toolItemId = null,
        string name = "Arbre PG") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Description ressource PG",
        SpriteLogicalPath = $"sprites/resources/{Guid.NewGuid():N}.png",
        RespawnSeconds = 30,
        ToolItemId = toolItemId,
        YieldItemId = yieldItemId,
        YieldQuantity = 2,
    };

    private static ResourceSpawnDefinition CreateSpawn(
        Guid mapId,
        Guid resourceId,
        int x,
        int y) => new()
    {
        Id = Guid.NewGuid(),
        MapId = mapId,
        ResourceId = resourceId,
        TileX = x,
        TileY = y,
    };

    private static async Task<Guid> SaveMapAsync(PostgresMapRepository repository)
    {
        var map = new Map
        {
            Name = "Carte ressources PG",
            Width = 10,
            Height = 10,
        };
        map.Layers.Add(new Layer
        {
            LayerType = LayerType.Ground,
            DisplayName = "Sol",
        });
        var saved = Assert.IsType<SaveMapResult.Success>(await repository.SaveAsync(
            new SaveMapRequest
            {
                Map = map,
                ExpectedRevision = 0,
                Intent = SaveMapIntent.SaveDraft,
            }));
        return saved.MapId;
    }
}
