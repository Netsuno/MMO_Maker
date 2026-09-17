using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresItemRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresItemRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_Reload_DraftDistinct_Conflict_InvalidPublish_Rollback()
    {
        using var gate = CreateGate();
        var repository = new PostgresItemRepository(gate);
        var definition = CreateDefinition(
            "Potion majeure PG",
            ItemType.Consumable,
            "icons/items/pg-major-potion.png",
            maxStack: 25,
            buyPrice: 100,
            sellPrice: 30);

        var created = Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(
            new SaveItemRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.Equal(1, created.NewRevision);

        definition.Name = "Potion majeure publiée PG";
        definition.Description = "Restaure beaucoup de vie.";
        var published = Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(
            new SaveItemRequest
            {
                ItemId = created.ItemId,
                Definition = definition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.Equal(2, published.NewRevision);
        Assert.Equal(2, published.PublishedRevision);

        using var gate2 = CreateGate();
        var repository2 = new PostgresItemRepository(gate2);
        var draft = await repository2.LoadByIdAsync(created.ItemId);
        var snapshot = await repository2.LoadPublishedByIdAsync(created.ItemId);
        Assert.NotNull(draft);
        Assert.NotNull(snapshot);
        AssertDefinitionEqual(definition, draft!.Definition);
        AssertDefinitionEqual(definition, snapshot!.Definition);

        draft.Definition.Name = "Potion brouillon PG";
        draft.Definition.BuyPrice = 125;
        Assert.IsType<SaveItemResult.Success>(await repository2.SaveAsync(new SaveItemRequest
        {
            ItemId = created.ItemId,
            Definition = draft.Definition,
            ExpectedRevision = draft.Revision,
            Intent = SaveContentIntent.SaveDraft,
        }));

        using var gate3 = CreateGate();
        var repository3 = new PostgresItemRepository(gate3);
        Assert.Equal(
            "Potion brouillon PG",
            (await repository3.LoadByIdAsync(created.ItemId))!.Definition.Name);
        Assert.Equal(
            "Potion majeure publiée PG",
            (await repository3.LoadPublishedByIdAsync(created.ItemId))!.Definition.Name);

        Assert.IsType<SaveItemResult.Conflict>(await repository3.SaveAsync(new SaveItemRequest
        {
            ItemId = created.ItemId,
            Definition = draft.Definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var invalid = CreateDefinition(
            "Pile invalide PG",
            ItemType.Quest,
            "icons/items/pg-invalid.png",
            maxStack: 1000,
            buyPrice: 0,
            sellPrice: 0);
        Assert.IsType<SaveItemResult.ValidationFailed>(await repository3.SaveAsync(
            new SaveItemRequest
            {
                Definition = invalid,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        using var gate4 = CreateGate();
        var failing = new PostgresItemRepository(gate4)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected-fail"),
        };
        var before = await failing.ListSummariesAsync();
        var failed = await failing.SaveAsync(new SaveItemRequest
        {
            Definition = CreateDefinition(
                "Objet rollback PG",
                ItemType.Key,
                "icons/items/pg-rollback.png",
                maxStack: 1,
                buyPrice: 0,
                sellPrice: 0),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveItemResult.PersistenceFailed>(failed);

        using var gate5 = CreateGate();
        var afterRepository = new PostgresItemRepository(gate5);
        Assert.Empty(await afterRepository.ListSummariesAsync(search: "Objet rollback PG"));
        Assert.Equal(before.Count, (await afterRepository.ListSummariesAsync()).Count);
        Assert.Contains(
            await afterRepository.ListPublishedAsync(),
            item => item.Id == created.ItemId && item.Name == "Potion majeure publiée PG");
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Search_StatusFilter_AndDelete()
    {
        using var gate = CreateGate();
        var repository = new PostgresItemRepository(gate);
        var published = Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(
            new SaveItemRequest
            {
                Definition = CreateDefinition(
                    "Anneau filtrable PG",
                    ItemType.Armor,
                    "icons/items/pg-ring.png",
                    maxStack: 1,
                    buyPrice: 400,
                    sellPrice: 80),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        var draft = Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(
            new SaveItemRequest
            {
                Definition = CreateDefinition(
                    "Élixir filtrable PG",
                    ItemType.Consumable,
                    "icons/items/pg-elixir.png",
                    maxStack: 10,
                    buyPrice: 75,
                    sellPrice: 20),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));

        var byPath = await repository.ListSummariesAsync(search: "pg-ring");
        Assert.Contains(byPath, entry => entry.ItemId == published.ItemId);
        var publishedOnly = await repository.ListSummariesAsync(
            search: "filtrable PG",
            statusFilter: ContentPublishStatus.Published);
        var publishedEntry = Assert.Single(
            publishedOnly,
            entry => entry.ItemId == published.ItemId);
        Assert.Equal(ItemType.Armor, publishedEntry.Kind);
        Assert.DoesNotContain(publishedOnly, entry => entry.ItemId == draft.ItemId);

        Assert.IsType<DeleteItemResult.Success>(await repository.DeleteAsync(draft.ItemId));
        Assert.Null(await repository.LoadByIdAsync(draft.ItemId));
        Assert.IsType<DeleteItemResult.NotFound>(await repository.DeleteAsync(draft.ItemId));
    }

    private FrogDbContextGate CreateGate()
        => new(new(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static ItemDefinition CreateDefinition(
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
        Description = "Description PG",
    };

    private static void AssertDefinitionEqual(ItemDefinition expected, ItemDefinition actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.IconLogicalPath, actual.IconLogicalPath);
        Assert.Equal(expected.MaxStack, actual.MaxStack);
        Assert.Equal(expected.BuyPrice, actual.BuyPrice);
        Assert.Equal(expected.SellPrice, actual.SellPrice);
        Assert.Equal(expected.Description, actual.Description);
    }
}
