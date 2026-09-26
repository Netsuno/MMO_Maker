using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Forms;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class QuickEventPresetPublisherTests
{
    [Theory]
    [InlineData(QuickEventPresetKind.Chest, "Coffre", "coffre_coffre", "coffre_coffre_ouvert", "coffre_coffre_butin")]
    [InlineData(QuickEventPresetKind.Door, "Porte", "porte_porte", "porte_porte_ouverte", null)]
    [InlineData(QuickEventPresetKind.Inn, "Auberge", "auberge_auberge", "auberge_auberge_repose", "auberge_auberge_nuits")]
    public async Task Publish_PersistsBlockingPages(QuickEventPresetKind kind, string name, string slug, string switchKey, string? counterKey)
    {
        var repository = new InMemoryMapEventRepository();
        var result = await QuickEventPresetPublisher.PublishAsync(
            new MapEventsPostgreSqlService(repository),
            name,
            kind);

        Assert.True(result.Success, result.Error);
        Assert.Equal(slug, result.Slug);
        Assert.Equal(switchKey, result.SwitchKey);
        Assert.Equal(counterKey, result.CounterKey);
        Assert.Equal(Phase8MapEventTriggerKinds.Action, result.TriggerKind);

        var stored = await repository.LoadByIdAsync(result.EventId);
        Assert.NotNull(stored);
        Assert.Equal(ContentPublishStatus.Published, stored!.Status);
        Assert.Equal(slug, stored.Definition.CatalogSlug);
        Assert.Equal(2, stored.Definition.Pages.Count);
        Assert.All(stored.Definition.Pages, page => Assert.True(page.BlocksCollision));
        Assert.Equal(switchKey, QuickEventPresetDraft.StateKey(stored.Definition.CatalogSlug!, QuickEventPresetDraft.SwitchSuffix(kind)));
        if (kind == QuickEventPresetKind.Inn)
        {
            Assert.Equal(MapEventCommandDiscriminators.Branch, stored.Definition.Pages[1].Commands[0].Discriminator);
        }
    }

    [Fact]
    public async Task Publish_RetriesSlugAndRewritesStateKeys()
    {
        var repository = new InMemoryMapEventRepository();
        var mapEvents = new MapEventsPostgreSqlService(repository);
        Assert.True(mapEvents.TryInsertCatalog("coffre_coffre", "Autre", out _, out var insertError), insertError);

        var result = await QuickEventPresetPublisher.PublishAsync(mapEvents, "Coffre", QuickEventPresetKind.Chest);
        Assert.True(result.Success, result.Error);
        Assert.Equal("coffre_coffre_2", result.Slug);
        Assert.Equal("coffre_coffre_2_ouvert", result.SwitchKey);

        var stored = await repository.LoadByIdAsync(result.EventId);
        Assert.Equal("coffre_coffre_2", stored!.Definition.CatalogSlug);
        Assert.True(
            MapEventParameterSchemas.TryParseSetSwitch(
                stored.Definition.Pages[0].Commands[1].ParameterJson,
                out var switchId,
                out var value,
                out var error),
            error);
        Assert.Equal("coffre_coffre_2_ouvert", switchId);
        Assert.True(value);
    }

    [Fact]
    public async Task Publish_RollsBackCatalogWhenPagePublishFails()
    {
        var repository = new FailPagePublishRepository();
        var result = await QuickEventPresetPublisher.PublishAsync(
            new MapEventsPostgreSqlService(repository),
            "Porte",
            QuickEventPresetKind.Door);

        Assert.False(result.Success);
        Assert.Contains("pages", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await repository.ListSummariesAsync());
    }

    [Fact]
    public async Task Publish_DoesNotWriteWhenNameIsEmpty()
    {
        var repository = new InMemoryMapEventRepository();
        var result = await QuickEventPresetPublisher.PublishAsync(
            new MapEventsPostgreSqlService(repository),
            "  ",
            QuickEventPresetKind.Inn);

        Assert.False(result.Success);
        Assert.Empty(await repository.ListSummariesAsync());
    }

    [Fact]
    public void Dialog_FrenchLabels_DefaultNameMatchesKind()
    {
        StaTestRunner.Run(() =>
        {
            using var dlg = new QuickEventPresetDialog(
                new MapEventsPostgreSqlService(new InMemoryMapEventRepository()),
                QuickEventPresetKind.Chest);
            dlg.Show();
            try
            {
                Assert.Equal("Coffre rapide", dlg.Text);
                Assert.Equal("Créer et placer", dlg.CreateButtonForTest.Text);
                Assert.Equal("Coffre", dlg.NameBoxForTest.Text);
                Assert.Equal(QuickEventPresetKind.Chest, dlg.KindForTest);
                Assert.Contains("bloquante", QuickEventPresetDraft.Describe(QuickEventPresetKind.Chest), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                dlg.Close();
            }
        });
    }

    [Fact]
    public void BrowseDialog_OffersChestDoorAndInn()
    {
        StaTestRunner.Run(() =>
        {
            using var dlg = new MapEventsBrowseDialog(
                new MapEventsPostgreSqlService(new InMemoryMapEventRepository()),
                Guid.Empty);
            dlg.Show();
            try
            {
                Assert.NotNull(FindButton(dlg, "Coffre…"));
                Assert.NotNull(FindButton(dlg, "Porte…"));
                Assert.NotNull(FindButton(dlg, "Auberge…"));
                Assert.NotNull(FindButton(dlg, "PNJ rapide…"));
            }
            finally
            {
                dlg.Close();
            }
        });
    }

    private static System.Windows.Forms.Button? FindButton(System.Windows.Forms.Control root, string text)
    {
        if (root is System.Windows.Forms.Button button && button.Text == text)
        {
            return button;
        }

        foreach (System.Windows.Forms.Control child in root.Controls)
        {
            var found = FindButton(child, text);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class FailPagePublishRepository : InMemoryMapEventRepository
    {
        public override Task<SaveMapEventResult> SaveAsync(SaveMapEventRequest request, CancellationToken cancellationToken = default)
        {
            if (request.EventId is Guid id && id != Guid.Empty && request.Intent == SaveContentIntent.Publish)
            {
                return Task.FromResult<SaveMapEventResult>(new SaveMapEventResult.PersistenceFailed("pages refusées"));
            }

            return base.SaveAsync(request, cancellationToken);
        }
    }
}
