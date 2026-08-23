using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Frog.Tests;

public sealed class ResourceDefinitionValidationTests
{
    [Fact]
    public void Validate_Accepts_BoundariesAndOptionalTool()
    {
        var definition = ResourceContentTestData.CreateResource(
            Guid.NewGuid(),
            toolItemId: null);
        definition.RespawnSeconds = 0;
        definition.YieldQuantity = ResourceDefinition.MinYieldQuantity;

        Assert.True(definition.Validate(out var error));
        Assert.Null(error);

        definition.RespawnSeconds = int.MaxValue;
        definition.YieldQuantity = ResourceDefinition.MaxYieldQuantity;
        Assert.True(definition.Validate(out error));
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Rejects_InvalidIdentityTextPathRespawnItemsAndQuantity()
    {
        var definition = ResourceContentTestData.CreateResource(Guid.NewGuid());
        definition.Id = Guid.Empty;
        Assert.False(definition.Validate(out _));

        definition = ResourceContentTestData.CreateResource(Guid.NewGuid());
        definition.Name = string.Empty;
        Assert.False(definition.Validate(out _));

        definition = ResourceContentTestData.CreateResource(Guid.NewGuid());
        definition.Description = new string('x', ResourceDefinition.MaxDescriptionLength + 1);
        Assert.False(definition.Validate(out _));

        definition = ResourceContentTestData.CreateResource(Guid.NewGuid());
        definition.SpriteLogicalPath = "../tree.png";
        Assert.False(definition.Validate(out _));

        definition = ResourceContentTestData.CreateResource(Guid.NewGuid());
        definition.RespawnSeconds = -1;
        Assert.False(definition.Validate(out _));

        definition = ResourceContentTestData.CreateResource(Guid.NewGuid());
        definition.ToolItemId = Guid.Empty;
        Assert.False(definition.Validate(out _));

        definition = ResourceContentTestData.CreateResource(Guid.Empty);
        Assert.False(definition.Validate(out _));

        definition = ResourceContentTestData.CreateResource(Guid.NewGuid());
        definition.YieldQuantity = ResourceDefinition.MaxYieldQuantity + 1;
        Assert.False(definition.Validate(out _));
    }

    [Fact]
    public void SpawnValidate_Rejects_MissingReferencesAndNegativeCoordinates()
    {
        var definition = new ResourceSpawnDefinition
        {
            Id = Guid.NewGuid(),
            MapId = Guid.NewGuid(),
            ResourceId = Guid.NewGuid(),
            TileX = 0,
            TileY = int.MaxValue,
        };
        Assert.True(definition.Validate(out var error));
        Assert.Null(error);

        definition.MapId = Guid.Empty;
        Assert.False(definition.Validate(out _));
        definition.MapId = Guid.NewGuid();
        definition.ResourceId = Guid.Empty;
        Assert.False(definition.Validate(out _));
        definition.ResourceId = Guid.NewGuid();
        definition.TileX = -1;
        Assert.False(definition.Validate(out _));
    }
}

public sealed class ResourceWorkspaceSessionTests
{
    [Fact]
    public async Task Resource_DraftPublish_IsDistinct_RejectsInvalidItemRefs_AndProtectsItems()
    {
        var items = new InMemoryItemRepository();
        var yieldItemId = await ResourceContentTestData.PublishItemAsync(items, "Bois");
        var toolItemId = await ResourceContentTestData.PublishItemAsync(items, "Hache");
        var nextYieldItemId = await ResourceContentTestData.PublishItemAsync(items, "Bois dur");
        var repository = new InMemoryResourceRepository(items);
        var session = new ResourceWorkspaceSession(repository);
        session.AdoptNewDraft(ResourceContentTestData.CreateResource(
            yieldItemId,
            toolItemId,
            "Chêne"));

        var draft = Assert.IsType<SaveResourceResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, draft.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.Description = "Version publiée";
        session.MarkDirty();
        var published = Assert.IsType<SaveResourceResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);

        session.Current.Description = "Nouveau brouillon";
        session.Current.ToolItemId = null;
        session.Current.YieldItemId = nextYieldItemId;
        session.MarkDirty();
        Assert.IsType<SaveResourceResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(
            "Nouveau brouillon",
            (await repository.LoadByIdAsync(draft.ResourceId))!.Definition.Description);
        var snapshot = (await repository.LoadPublishedByIdAsync(draft.ResourceId))!.Definition;
        Assert.Equal("Version publiée", snapshot.Description);
        Assert.Equal(toolItemId, snapshot.ToolItemId);
        Assert.Equal(yieldItemId, snapshot.YieldItemId);

        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(toolItemId));
        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(nextYieldItemId));

        var missingReference = ResourceContentTestData.CreateResource(Guid.NewGuid());
        Assert.IsType<SaveResourceResult.ValidationFailed>(await repository.SaveAsync(
            new SaveResourceRequest
            {
                Definition = missingReference,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        var draftItem = ResourceContentTestData.CreateItem("Objet brouillon");
        var draftItemSaved = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(
            new SaveItemRequest
            {
                Definition = draftItem,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        missingReference.YieldItemId = draftItemSaved.ItemId;
        Assert.IsType<SaveResourceResult.ValidationFailed>(await repository.SaveAsync(
            new SaveResourceRequest
            {
                Definition = missingReference,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        Assert.IsType<SaveResourceResult.Conflict>(await repository.SaveAsync(
            new SaveResourceRequest
            {
                ResourceId = draft.ResourceId,
                Definition = session.Current,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.SaveDraft,
            }));
    }

    [Fact]
    public async Task Spawn_RequiresExistingMapAndPublishedResource_ThenDraftPublishIsDistinct()
    {
        var items = new InMemoryItemRepository();
        var yieldItemId = await ResourceContentTestData.PublishItemAsync(items, "Minerai");
        var resources = new InMemoryResourceRepository(items);
        var resource = Assert.IsType<SaveResourceResult.Success>(await resources.SaveAsync(
            new SaveResourceRequest
            {
                Definition = ResourceContentTestData.CreateResource(yieldItemId, name: "Rocher"),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        var maps = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var mapId = await ResourceContentTestData.SaveMapAsync(maps);
        var spawns = new InMemoryResourceSpawnRepository(maps, resources);
        var definition = ResourceContentTestData.CreateSpawn(mapId, resource.ResourceId, 2, 3);

        var draft = Assert.IsType<SaveResourceSpawnResult.Success>(await spawns.SaveAsync(
            new SaveResourceSpawnRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        definition.TileX = 4;
        var published = Assert.IsType<SaveResourceSpawnResult.Success>(await spawns.SaveAsync(
            new SaveResourceSpawnRequest
            {
                SpawnId = draft.SpawnId,
                Definition = definition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.Equal(2, published.PublishedRevision);

        definition.TileX = 7;
        Assert.IsType<SaveResourceSpawnResult.Success>(await spawns.SaveAsync(
            new SaveResourceSpawnRequest
            {
                SpawnId = draft.SpawnId,
                Definition = definition,
                ExpectedRevision = 2,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.Equal(7, (await spawns.LoadByIdAsync(draft.SpawnId))!.Definition.TileX);
        Assert.Equal(4, (await spawns.LoadPublishedByIdAsync(draft.SpawnId))!.Definition.TileX);

        var missingMap = ResourceContentTestData.CreateSpawn(
            Guid.NewGuid(),
            resource.ResourceId,
            0,
            0);
        Assert.IsType<SaveResourceSpawnResult.ValidationFailed>(await spawns.SaveAsync(
            new SaveResourceSpawnRequest
            {
                Definition = missingMap,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        var draftResource = Assert.IsType<SaveResourceResult.Success>(await resources.SaveAsync(
            new SaveResourceRequest
            {
                Definition = ResourceContentTestData.CreateResource(
                    yieldItemId,
                    name: "Ressource brouillon"),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        var unpublishedResource = ResourceContentTestData.CreateSpawn(
            mapId,
            draftResource.ResourceId,
            0,
            0);
        Assert.IsType<SaveResourceSpawnResult.ValidationFailed>(await spawns.SaveAsync(
            new SaveResourceSpawnRequest
            {
                Definition = unpublishedResource,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        Assert.IsType<DeleteResourceResult.Referenced>(
            await resources.DeleteAsync(resource.ResourceId));
        Assert.IsType<DeleteResourceSpawnResult.Success>(await spawns.DeleteAsync(draft.SpawnId));
        Assert.IsType<DeleteResourceResult.Success>(
            await resources.DeleteAsync(resource.ResourceId));
    }
}

public sealed class PublishedResourceConsumerTests
{
    [Fact]
    public async Task Consumers_LoadOnlyPublishedResourcesAndSpawns()
    {
        var items = new InMemoryItemRepository();
        var yieldItemId = await ResourceContentTestData.PublishItemAsync(items, "Fibre");
        var resources = new InMemoryResourceRepository(items);
        await resources.SaveAsync(new SaveResourceRequest
        {
            Definition = ResourceContentTestData.CreateResource(
                yieldItemId,
                name: "Herbe brouillon"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        var publishedResource = Assert.IsType<SaveResourceResult.Success>(
            await resources.SaveAsync(new SaveResourceRequest
            {
                Definition = ResourceContentTestData.CreateResource(
                    yieldItemId,
                    name: "Herbe publiée"),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        var maps = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var mapId = await ResourceContentTestData.SaveMapAsync(maps);
        var otherMapId = await ResourceContentTestData.SaveMapAsync(maps, "Autre carte");
        var spawns = new InMemoryResourceSpawnRepository(maps, resources);
        await spawns.SaveAsync(new SaveResourceSpawnRequest
        {
            Definition = ResourceContentTestData.CreateSpawn(
                mapId,
                publishedResource.ResourceId,
                1,
                1),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        await spawns.SaveAsync(new SaveResourceSpawnRequest
        {
            Definition = ResourceContentTestData.CreateSpawn(
                mapId,
                publishedResource.ResourceId,
                2,
                2),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var resourceConsumer = new Frog.Server.Services.PublishedResourceConsumer(
            resources,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedResourceConsumer>());
        var spawnConsumer = new Frog.Server.Services.PublishedResourceSpawnConsumer(
            spawns,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedResourceSpawnConsumer>());

        Assert.Equal("Herbe publiée", Assert.Single(await resourceConsumer.LoadPublishedAsync()).Name);
        Assert.Equal(2, Assert.Single(await spawnConsumer.LoadPublishedAsync(mapId)).TileX);
        Assert.Empty(await spawnConsumer.LoadPublishedAsync(otherMapId));
    }
}

internal static class ResourceContentTestData
{
    public static ResourceDefinition CreateResource(
        Guid yieldItemId,
        Guid? toolItemId = null,
        string name = "Arbre") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Description ressource",
        SpriteLogicalPath = $"sprites/resources/{Guid.NewGuid():N}.png",
        RespawnSeconds = 30,
        ToolItemId = toolItemId,
        YieldItemId = yieldItemId,
        YieldQuantity = 2,
    };

    public static ResourceSpawnDefinition CreateSpawn(
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

    public static async Task<Guid> PublishItemAsync(InMemoryItemRepository repository, string name)
    {
        var result = Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(
            new SaveItemRequest
            {
                Definition = CreateItem(name),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        return result.ItemId;
    }

    public static ItemDefinition CreateItem(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = ItemType.Quest,
        IconLogicalPath = $"icons/items/{Guid.NewGuid():N}.png",
        MaxStack = 99,
        BuyPrice = 0,
        SellPrice = 1,
    };

    public static async Task<Guid> SaveMapAsync(
        InMemoryMapRepository maps,
        string name = "Carte ressources")
    {
        var map = new Map
        {
            Name = name,
            Width = 10,
            Height = 10,
        };
        map.Layers.Add(new Layer
        {
            LayerType = LayerType.Ground,
            DisplayName = "Sol",
        });
        var saved = Assert.IsType<SaveMapResult.Success>(await maps.SaveAsync(new SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        }));
        return saved.MapId;
    }
}
