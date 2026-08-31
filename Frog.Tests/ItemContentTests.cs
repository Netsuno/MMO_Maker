using System;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Frog.Tests;

public sealed class ItemDefinitionValidationTests
{
    [Fact]
    public void Validate_Accepts_AllAuthoredItemTypesAndBoundaries()
    {
        foreach (var type in Enum.GetValues<ItemType>())
        {
            if (type == ItemType.Unknown)
            {
                continue;
            }

            var item = Valid();
            item.Kind = type;
            item.MaxStack = ItemDefinition.MaxStackSize;
            item.BuyPrice = 0;
            item.SellPrice = int.MaxValue;
            Assert.True(item.Validate(out var error));
            Assert.Null(error);
        }
    }

    [Fact]
    public void Validate_Rejects_InvalidTypePathStackPricesAndDescription()
    {
        var item = Valid();
        item.Kind = ItemType.Unknown;
        Assert.False(item.Validate(out _));

        item = Valid();
        item.IconLogicalPath = "../outside.png";
        Assert.False(item.Validate(out _));

        item = Valid();
        item.MaxStack = 1000;
        Assert.False(item.Validate(out _));

        item = Valid();
        item.BuyPrice = -1;
        Assert.False(item.Validate(out _));

        item = Valid();
        item.Description = new string('x', ItemDefinition.MaxDescriptionLength + 1);
        Assert.False(item.Validate(out _));
    }

    private static ItemDefinition Valid() => ItemWorkspaceSessionTests.CreateDefinition(
        "Potion",
        ItemType.Consumable,
        "icons/items/potion.png",
        maxStack: 20,
        buyPrice: 50,
        sellPrice: 15);
}

public sealed class ItemWorkspaceSessionTests
{
    [Fact]
    public async Task Create_SaveDraft_Publish_Duplicate_SearchAndFilter_RoundTrip()
    {
        var repository = new InMemoryItemRepository();
        var session = new ItemWorkspaceSession(repository);
        session.AdoptNewDraft(CreateDefinition(
            "Potion majeure",
            ItemType.Consumable,
            "icons/items/major-potion.png",
            maxStack: 25,
            buyPrice: 100,
            sellPrice: 30));

        var saved = Assert.IsType<SaveItemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, saved.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.Description = "Restaure beaucoup de vie.";
        session.MarkDirty();
        var published = Assert.IsType<SaveItemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        var snapshot = await repository.LoadPublishedByIdAsync(saved.ItemId);
        Assert.Equal("Restaure beaucoup de vie.", snapshot!.Definition.Description);
        Assert.Equal(100, snapshot.Definition.BuyPrice);

        session.DuplicateCurrent();
        Assert.True(session.IsDirty);
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);

        session.SearchFilter = "Potion";
        session.StatusFilter = ContentPublishStatus.Published;
        await session.RefreshCatalogAsync();
        var entry = Assert.Single(session.Catalog);
        Assert.Equal(saved.ItemId, entry.ItemId);
    }

    [Fact]
    public async Task DraftDistinctFromPublished_AndStaleRevisionConflicts()
    {
        var repository = new InMemoryItemRepository();
        var definition = CreateDefinition(
            "Épée v1",
            ItemType.Weapon,
            "icons/items/sword.png",
            maxStack: 1,
            buyPrice: 500,
            sellPrice: 100);
        var published = Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(
            new SaveItemRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        definition.Name = "Épée v2 brouillon";
        Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(new SaveItemRequest
        {
            ItemId = published.ItemId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.Equal(
            "Épée v2 brouillon",
            (await repository.LoadByIdAsync(published.ItemId))!.Definition.Name);
        Assert.Equal(
            "Épée v1",
            (await repository.LoadPublishedByIdAsync(published.ItemId))!.Definition.Name);

        Assert.IsType<SaveItemResult.Conflict>(await repository.SaveAsync(new SaveItemRequest
        {
            ItemId = published.ItemId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));
    }

    [Fact]
    public async Task Invalid_CannotPublish_AndSavedItemCanDelete()
    {
        var repository = new InMemoryItemRepository();
        var session = new ItemWorkspaceSession(repository);
        var invalid = CreateDefinition(
            "Pile invalide",
            ItemType.Quest,
            "icons/items/quest.png",
            maxStack: 1000,
            buyPrice: 0,
            sellPrice: 0);
        session.AdoptNewDraft(invalid);
        Assert.IsType<SaveItemResult.ValidationFailed>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));

        session.AdoptNewDraft(CreateDefinition(
            "Clé",
            ItemType.Key,
            "icons/items/key.png",
            maxStack: 1,
            buyPrice: 0,
            sellPrice: 0));
        Assert.IsType<SaveItemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.IsType<DeleteItemResult.Success>(await session.DeleteCurrentAsync());
        Assert.Empty(await repository.ListSummariesAsync());
    }

    internal static ItemDefinition CreateDefinition(
        string name,
        ItemType kind,
        string iconPath,
        int maxStack,
        int buyPrice,
        int sellPrice) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        IconLogicalPath = iconPath,
        MaxStack = maxStack,
        BuyPrice = buyPrice,
        SellPrice = sellPrice,
        Description = "Description de test",
    };
}

public sealed class PublishedItemConsumerTests
{
    [Fact]
    public async Task Consumer_Loads_OnlyPublishedDefinitions()
    {
        var repository = new InMemoryItemRepository();
        await repository.SaveAsync(new SaveItemRequest
        {
            Definition = ItemWorkspaceSessionTests.CreateDefinition(
                "Brouillon",
                ItemType.Armor,
                "icons/items/draft-armor.png",
                1,
                200,
                40),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        await repository.SaveAsync(new SaveItemRequest
        {
            Definition = ItemWorkspaceSessionTests.CreateDefinition(
                "Potion publiée",
                ItemType.Consumable,
                "icons/items/published-potion.png",
                10,
                20,
                5),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var consumer = new Frog.Server.Services.PublishedItemConsumer(
            repository,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedItemConsumer>());
        var loaded = await consumer.LoadPublishedAsync();

        var definition = Assert.Single(loaded);
        Assert.Equal("Potion publiée", definition.Name);
        Assert.Equal(ItemType.Consumable, definition.Kind);
    }
}
