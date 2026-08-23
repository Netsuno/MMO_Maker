using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresShopRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresShopRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_Reload_DraftDistinct_Conflict_InvalidReference_Rollback()
    {
        await using var db = CreateDb();
        var items = new PostgresItemRepository(db);
        var itemId = await PublishItemAsync(items, "Potion boutique PG");
        var repository = new PostgresShopRepository(db, items);
        var definition = CreateDefinition(
            "Apothicaire PG",
            new ShopListing { ItemId = itemId, Price = 75, Stock = null });

        var created = Assert.IsType<SaveShopResult.Success>(await repository.SaveAsync(
            new SaveShopRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.Equal(1, created.NewRevision);

        definition.Name = "Apothicaire publié PG";
        definition.Description = "Boutique publiée PG";
        var published = Assert.IsType<SaveShopResult.Success>(await repository.SaveAsync(
            new SaveShopRequest
            {
                ShopId = created.ShopId,
                Definition = definition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.Equal(2, published.NewRevision);
        Assert.Equal(2, published.PublishedRevision);

        await using var db2 = CreateDb();
        var repository2 = new PostgresShopRepository(db2);
        var draft = await repository2.LoadByIdAsync(created.ShopId);
        var snapshot = await repository2.LoadPublishedByIdAsync(created.ShopId);
        Assert.NotNull(draft);
        Assert.NotNull(snapshot);
        AssertDefinitionEqual(definition, draft!.Definition);
        AssertDefinitionEqual(definition, snapshot!.Definition);

        draft.Definition.Name = "Apothicaire brouillon PG";
        draft.Definition.Listings[0].Price = 90;
        Assert.IsType<SaveShopResult.Success>(await repository2.SaveAsync(new SaveShopRequest
        {
            ShopId = created.ShopId,
            Definition = draft.Definition,
            ExpectedRevision = draft.Revision,
            Intent = SaveContentIntent.SaveDraft,
        }));

        await using var db3 = CreateDb();
        var repository3 = new PostgresShopRepository(db3);
        Assert.Equal(
            "Apothicaire brouillon PG",
            (await repository3.LoadByIdAsync(created.ShopId))!.Definition.Name);
        var publishedSnapshot = (await repository3.LoadPublishedByIdAsync(created.ShopId))!.Definition;
        Assert.Equal("Apothicaire publié PG", publishedSnapshot.Name);
        Assert.Equal(75, Assert.Single(publishedSnapshot.Listings).Price);
        Assert.IsType<SaveShopResult.Conflict>(await repository3.SaveAsync(new SaveShopRequest
        {
            ShopId = created.ShopId,
            Definition = draft.Definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var invalidReference = CreateDefinition(
            "Boutique référence invalide PG",
            new ShopListing { ItemId = Guid.NewGuid(), Price = 1 });
        Assert.IsType<SaveShopResult.ValidationFailed>(await repository3.SaveAsync(
            new SaveShopRequest
            {
                Definition = invalidReference,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.IsType<SaveShopResult.ValidationFailed>(await repository3.SaveAsync(
            new SaveShopRequest
            {
                Definition = invalidReference,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        await using var db4 = CreateDb();
        var draftItems = new PostgresItemRepository(db4);
        var draftItem = CreateItem("Objet non publié PG");
        var draftItemSaved = Assert.IsType<SaveItemResult.Success>(await draftItems.SaveAsync(
            new SaveItemRequest
            {
                Definition = draftItem,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        var invalidDraftReference = CreateDefinition(
            "Boutique objet brouillon PG",
            new ShopListing { ItemId = draftItemSaved.ItemId, Price = 1 });
        var draftReferenceRepository = new PostgresShopRepository(db4, draftItems);
        Assert.IsType<SaveShopResult.ValidationFailed>(await draftReferenceRepository.SaveAsync(
            new SaveShopRequest
            {
                Definition = invalidDraftReference,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        await using var db5 = CreateDb();
        var failing = new PostgresShopRepository(db5)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected-fail"),
        };
        var before = await failing.ListSummariesAsync();
        var failed = await failing.SaveAsync(new SaveShopRequest
        {
            Definition = CreateDefinition("Boutique rollback PG"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveShopResult.PersistenceFailed>(failed);

        await using var db6 = CreateDb();
        var afterRepository = new PostgresShopRepository(db6);
        Assert.Empty(await afterRepository.ListSummariesAsync(search: "Boutique rollback PG"));
        Assert.Equal(before.Count, (await afterRepository.ListSummariesAsync()).Count);
        Assert.Contains(
            await afterRepository.ListPublishedAsync(),
            shop => shop.Id == created.ShopId && shop.Name == "Apothicaire publié PG");
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Search_StatusFilter_Delete_AndPreventItemDeleteFromDraftOrSnapshot()
    {
        await using var db = CreateDb();
        var items = new PostgresItemRepository(db);
        var draftItemId = await PublishItemAsync(items, "Objet draft protégé PG");
        var snapshotItemId = await PublishItemAsync(items, "Objet snapshot protégé PG");
        var repository = new PostgresShopRepository(db, items);

        var draft = Assert.IsType<SaveShopResult.Success>(await repository.SaveAsync(
            new SaveShopRequest
            {
                Definition = CreateDefinition(
                    "Boutique brouillon filtrable PG",
                    new ShopListing { ItemId = draftItemId, Price = 5 }),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(draftItemId));

        var publishedDefinition = CreateDefinition(
            "Boutique publiée filtrable PG",
            new ShopListing { ItemId = snapshotItemId, Price = 10, Stock = 2 });
        var published = Assert.IsType<SaveShopResult.Success>(await repository.SaveAsync(
            new SaveShopRequest
            {
                Definition = publishedDefinition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        publishedDefinition.Listings.Clear();
        Assert.IsType<SaveShopResult.Success>(await repository.SaveAsync(new SaveShopRequest
        {
            ShopId = published.ShopId,
            Definition = publishedDefinition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var drafts = await repository.ListSummariesAsync(
            search: "filtrable PG",
            statusFilter: ContentPublishStatus.Draft);
        Assert.Contains(drafts, entry => entry.ShopId == draft.ShopId && entry.ListingCount == 1);
        Assert.Contains(drafts, entry => entry.ShopId == published.ShopId && entry.ListingCount == 0);
        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(snapshotItemId));

        Assert.IsType<DeleteShopResult.Success>(await repository.DeleteAsync(draft.ShopId));
        Assert.IsType<DeleteItemResult.Success>(await items.DeleteAsync(draftItemId));
        Assert.IsType<DeleteShopResult.Success>(await repository.DeleteAsync(published.ShopId));
        Assert.IsType<DeleteItemResult.Success>(await items.DeleteAsync(snapshotItemId));
        Assert.IsType<DeleteShopResult.NotFound>(await repository.DeleteAsync(published.ShopId));
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
        Kind = ItemType.Consumable,
        IconLogicalPath = $"icons/items/{Guid.NewGuid():N}.png",
        MaxStack = 20,
        BuyPrice = 50,
        SellPrice = 15,
    };

    private static ShopDefinition CreateDefinition(
        string name,
        params ShopListing[] listings) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Description boutique PG",
        Listings = listings.ToList(),
    };

    private static void AssertDefinitionEqual(ShopDefinition expected, ShopDefinition actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Listings.Count, actual.Listings.Count);
        for (var index = 0; index < expected.Listings.Count; index++)
        {
            Assert.Equal(expected.Listings[index].ItemId, actual.Listings[index].ItemId);
            Assert.Equal(expected.Listings[index].Price, actual.Listings[index].Price);
            Assert.Equal(expected.Listings[index].Stock, actual.Listings[index].Stock);
        }
    }
}
