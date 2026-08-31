using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Frog.Tests;

public sealed class ShopDefinitionValidationTests
{
    [Fact]
    public void Validate_Accepts_EmptyListingsAndPriceStockBoundaries()
    {
        var empty = ShopWorkspaceSessionTests.CreateDefinition("Boutique vide");
        empty.Listings.Clear();
        Assert.True(empty.Validate(out var emptyError));
        Assert.Null(emptyError);

        var bounded = ShopWorkspaceSessionTests.CreateDefinition(
            "Boutique bornes",
            new ShopListing
            {
                ItemId = Guid.NewGuid(),
                Price = 0,
                Stock = 0,
            },
            new ShopListing
            {
                ItemId = Guid.NewGuid(),
                Price = int.MaxValue,
                Stock = int.MaxValue,
            });
        Assert.True(bounded.Validate(out var boundedError));
        Assert.Null(boundedError);
    }

    [Fact]
    public void Validate_Rejects_InvalidIdentityTextListingsPricesStocksAndDuplicates()
    {
        var definition = ShopWorkspaceSessionTests.CreateDefinition("Invalide");
        definition.Id = Guid.Empty;
        Assert.False(definition.Validate(out _));

        definition = ShopWorkspaceSessionTests.CreateDefinition(string.Empty);
        Assert.False(definition.Validate(out _));

        definition = ShopWorkspaceSessionTests.CreateDefinition("Description invalide");
        definition.Description = new string('x', ShopDefinition.MaxDescriptionLength + 1);
        Assert.False(definition.Validate(out _));

        definition = ShopWorkspaceSessionTests.CreateDefinition(
            "Objet invalide",
            new ShopListing { ItemId = Guid.Empty, Price = 0 });
        Assert.False(definition.Validate(out _));

        definition = ShopWorkspaceSessionTests.CreateDefinition(
            "Prix invalide",
            new ShopListing { ItemId = Guid.NewGuid(), Price = -1 });
        Assert.False(definition.Validate(out _));

        definition = ShopWorkspaceSessionTests.CreateDefinition(
            "Stock invalide",
            new ShopListing { ItemId = Guid.NewGuid(), Price = 1, Stock = -1 });
        Assert.False(definition.Validate(out _));

        var duplicateId = Guid.NewGuid();
        definition = ShopWorkspaceSessionTests.CreateDefinition(
            "Doublon",
            new ShopListing { ItemId = duplicateId, Price = 1 },
            new ShopListing { ItemId = duplicateId, Price = 2 });
        Assert.False(definition.Validate(out _));
    }
}

public sealed class ShopWorkspaceSessionTests
{
    [Fact]
    public async Task Create_SaveDraft_Publish_DraftDistinct_Conflict_SearchDuplicateAndDelete()
    {
        var items = new InMemoryItemRepository();
        var itemId = await PublishItemAsync(items, "Potion boutique");
        var repository = new InMemoryShopRepository(items);
        var session = new ShopWorkspaceSession(repository);
        session.AdoptNewDraft(CreateDefinition(
            "Apothicaire",
            new ShopListing { ItemId = itemId, Price = 75, Stock = null }));

        var saved = Assert.IsType<SaveShopResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, saved.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.Description = "Boutique publiée.";
        session.MarkDirty();
        var published = Assert.IsType<SaveShopResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        session.Current.Description = "Modification brouillon.";
        session.Current.Listings[0].Price = 90;
        session.MarkDirty();
        Assert.IsType<SaveShopResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(
            "Modification brouillon.",
            (await repository.LoadByIdAsync(saved.ShopId))!.Definition.Description);
        var snapshot = (await repository.LoadPublishedByIdAsync(saved.ShopId))!.Definition;
        Assert.Equal("Boutique publiée.", snapshot.Description);
        Assert.Equal(75, Assert.Single(snapshot.Listings).Price);

        Assert.IsType<SaveShopResult.Conflict>(await repository.SaveAsync(new SaveShopRequest
        {
            ShopId = saved.ShopId,
            Definition = CreateDefinition(
                "Conflit",
                new ShopListing { ItemId = itemId, Price = 1 }),
            ExpectedRevision = 1,
        }));

        session.SearchFilter = "Apoth";
        session.StatusFilter = ContentPublishStatus.Draft;
        await session.RefreshCatalogAsync();
        Assert.Equal(saved.ShopId, Assert.Single(session.Catalog).ShopId);

        session.DuplicateCurrent();
        Assert.True(session.IsDirty);
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);

        Assert.True(await session.OpenAsync(saved.ShopId));
        Assert.IsType<DeleteShopResult.Success>(await session.DeleteCurrentAsync());
        Assert.Null(await repository.LoadByIdAsync(saved.ShopId));
    }

    [Fact]
    public async Task Listings_MustResolveToPublishedItems_OnDraftAndPublish()
    {
        var items = new InMemoryItemRepository();
        var repository = new InMemoryShopRepository(items);
        var missingId = Guid.NewGuid();
        var definition = CreateDefinition(
            "Référence absente",
            new ShopListing { ItemId = missingId, Price = 10 });

        Assert.IsType<SaveShopResult.ValidationFailed>(await repository.SaveAsync(
            new SaveShopRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.IsType<SaveShopResult.ValidationFailed>(await repository.SaveAsync(
            new SaveShopRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        var draftItem = CreateItem("Objet brouillon");
        var itemSaved = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(
            new SaveItemRequest
            {
                Definition = draftItem,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        definition.Listings[0].ItemId = itemSaved.ItemId;
        Assert.IsType<SaveShopResult.ValidationFailed>(await repository.SaveAsync(
            new SaveShopRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            ItemId = itemSaved.ItemId,
            Definition = draftItem,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.IsType<SaveShopResult.Success>(await repository.SaveAsync(new SaveShopRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));
    }

    [Fact]
    public async Task ItemDelete_IsBlockedByDraftAndEveryPublishedSnapshot()
    {
        var items = new InMemoryItemRepository();
        var draftItemId = await PublishItemAsync(items, "Objet brouillon protégé");
        var snapshotItemId = await PublishItemAsync(items, "Objet snapshot protégé");
        var shops = new InMemoryShopRepository(items);

        var draftShop = Assert.IsType<SaveShopResult.Success>(await shops.SaveAsync(
            new SaveShopRequest
            {
                Definition = CreateDefinition(
                    "Brouillon protecteur",
                    new ShopListing { ItemId = draftItemId, Price = 5 }),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(draftItemId));
        Assert.IsType<DeleteShopResult.Success>(await shops.DeleteAsync(draftShop.ShopId));
        Assert.IsType<DeleteItemResult.Success>(await items.DeleteAsync(draftItemId));

        var definition = CreateDefinition(
            "Snapshot protecteur",
            new ShopListing { ItemId = snapshotItemId, Price = 10 });
        var publishedShop = Assert.IsType<SaveShopResult.Success>(await shops.SaveAsync(
            new SaveShopRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        definition.Listings.Clear();
        Assert.IsType<SaveShopResult.Success>(await shops.SaveAsync(new SaveShopRequest
        {
            ShopId = publishedShop.ShopId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(snapshotItemId));
        Assert.IsType<DeleteShopResult.Success>(await shops.DeleteAsync(publishedShop.ShopId));
        Assert.IsType<DeleteItemResult.Success>(await items.DeleteAsync(snapshotItemId));
    }

    internal static ShopDefinition CreateDefinition(
        string name,
        params ShopListing[] listings) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Description de boutique",
        Listings = listings.ToList(),
    };

    internal static async Task<Guid> PublishItemAsync(InMemoryItemRepository repository, string name)
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

    private static ItemDefinition CreateItem(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = ItemType.Consumable,
        IconLogicalPath = $"icons/items/{Guid.NewGuid():N}.png",
        MaxStack = 20,
        BuyPrice = 50,
        SellPrice = 15,
    };
}

public sealed class PublishedShopConsumerTests
{
    [Fact]
    public async Task Consumer_Loads_OnlyPublishedDefinitions()
    {
        var items = new InMemoryItemRepository();
        var itemId = await ShopWorkspaceSessionTests.PublishItemAsync(items, "Objet publié");
        var repository = new InMemoryShopRepository(items);
        await repository.SaveAsync(new SaveShopRequest
        {
            Definition = ShopWorkspaceSessionTests.CreateDefinition(
                "Boutique brouillon",
                new ShopListing { ItemId = itemId, Price = 1 }),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        await repository.SaveAsync(new SaveShopRequest
        {
            Definition = ShopWorkspaceSessionTests.CreateDefinition(
                "Boutique publiée",
                new ShopListing { ItemId = itemId, Price = 2, Stock = 3 }),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var consumer = new Frog.Server.Services.PublishedShopConsumer(
            repository,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedShopConsumer>());
        var loaded = await consumer.LoadPublishedAsync();

        var definition = Assert.Single(loaded);
        Assert.Equal("Boutique publiée", definition.Name);
        Assert.Equal(itemId, Assert.Single(definition.Listings).ItemId);
    }
}
