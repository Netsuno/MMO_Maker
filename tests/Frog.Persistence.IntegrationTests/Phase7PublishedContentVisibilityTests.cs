using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Gameplay;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase7PublishedContentVisibilityTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase7PublishedContentVisibilityTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task DraftInvisible_PublishedVisible_RepublishUpdates()
    {
        using var gate = CreateGate();
        var items = new PostgresItemRepository(gate);
        var draftDef = Phase7ContentSeed.CreateDefaultConsumable();
        draftDef.Id = Guid.NewGuid();
        draftDef.Name = "DraftPotion";

        var created = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            Definition = draftDef,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.Null(await items.LoadPublishedByIdAsync(created.ItemId));
        Assert.NotNull(await items.LoadByIdAsync(created.ItemId));

        draftDef.Name = "PublishedPotion";
        var published = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            ItemId = created.ItemId,
            Definition = draftDef,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.NotNull(published.PublishedRevision);

        var loaded = await items.LoadPublishedByIdAsync(created.ItemId);
        Assert.NotNull(loaded);
        Assert.Equal("PublishedPotion", loaded!.Definition.Name);

        draftDef.Name = "DraftPotionV2";
        var draftAgain = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            ItemId = created.ItemId,
            Definition = draftDef,
            ExpectedRevision = 2,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal("DraftPotionV2", (await items.LoadByIdAsync(created.ItemId))!.Definition.Name);
        Assert.Equal("PublishedPotion", (await items.LoadPublishedByIdAsync(created.ItemId))!.Definition.Name);

        draftDef.Name = "PublishedPotionV2";
        Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            ItemId = created.ItemId,
            Definition = draftDef,
            ExpectedRevision = draftAgain.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal("PublishedPotionV2", (await items.LoadPublishedByIdAsync(created.ItemId))!.Definition.Name);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Phase7Seed_PublishesAllCatalogEntries()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var classes = new PostgresClassRepository(gate, new PostgresSpellRepository(gate));
        var items = new PostgresItemRepository(gate);
        var npcs = new PostgresNpcRepository(gate);
        var shops = new PostgresShopRepository(gate, items);

        Assert.NotNull(await classes.LoadPublishedByIdAsync(seed.ClassId));
        Assert.NotNull(await items.LoadPublishedByIdAsync(seed.WeaponId));
        Assert.NotNull(await items.LoadPublishedByIdAsync(seed.ConsumableId));
        Assert.NotNull(await npcs.LoadPublishedByIdAsync(seed.MonsterId));
        Assert.NotNull(await shops.LoadPublishedByIdAsync(seed.ShopId));
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
}
