using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Forms;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class QuickTalkingNpcPublisherTests
{
    [Fact]
    public async Task Publish_CreatesPublishedDialogueAndStartDialoguePage()
    {
        var dialogues = new InMemoryPhase8ContentEditorService();
        var repository = new InMemoryMapEventRepository();
        var mapEvents = new MapEventsPostgreSqlService(repository);

        var result = await QuickTalkingNpcPublisher.PublishAsync(
            dialogues,
            mapEvents,
            new QuickTalkingNpcRequest
            {
                Name = "Gardien",
                Text = "Halte !",
                TriggerKind = Phase8MapEventTriggerKinds.Action,
            });

        Assert.True(result.Success, result.Error);
        Assert.Equal("pnj_gardien", result.Slug);
        Assert.Equal(Phase8MapEventTriggerKinds.Action, result.TriggerKind);

        var dialogue = await dialogues.LoadDraftAsync(result.DialogueId);
        Assert.NotNull(dialogue);
        Assert.Equal(ContentPublishStatus.Published, dialogue!.Status);
        Assert.True(Phase8ContentPostgreSqlService.TryDeserialize(dialogue.PayloadJson, out DialogueDefinition def, out var error), error);
        Assert.Equal("Halte !", Assert.Single(def.Lines).Text);
        Assert.Equal(result.DialogueId, def.Id);

        var stored = await repository.LoadByIdAsync(result.EventId);
        Assert.NotNull(stored);
        Assert.Equal(ContentPublishStatus.Published, stored!.Status);
        Assert.Equal(2, stored.Revision);
        var page = Assert.Single(stored.Definition.Pages);
        Assert.Equal(Phase8MapEventTriggerKinds.Action, page.TriggerKind);
        var command = Assert.Single(page.Commands);
        Assert.Equal(MapEventCommandDiscriminators.StartDialogue, command.Discriminator);
        Assert.NotEqual(MapEventCommandDiscriminators.ShowText, command.Discriminator);
        Assert.True(MapEventParameterSchemas.TryParseStartDialogue(command.ParameterJson, out var dialogueId, out error), error);
        Assert.Equal(result.DialogueId, dialogueId);
    }

    [Fact]
    public async Task Publish_PlayerContact_UsesMatchingTrigger()
    {
        var repository = new InMemoryMapEventRepository();
        var result = await QuickTalkingNpcPublisher.PublishAsync(
            new InMemoryPhase8ContentEditorService(),
            new MapEventsPostgreSqlService(repository),
            new QuickTalkingNpcRequest
            {
                Name = "Chien",
                Text = "Ouaf.",
                TriggerKind = Phase8MapEventTriggerKinds.PlayerContact,
            });

        Assert.True(result.Success, result.Error);
        var stored = await repository.LoadByIdAsync(result.EventId);
        Assert.Equal(Phase8MapEventTriggerKinds.PlayerContact, Assert.Single(stored!.Definition.Pages).TriggerKind);
    }

    [Fact]
    public async Task Publish_RetriesSlugWhenCatalogNameCollides()
    {
        var repository = new InMemoryMapEventRepository();
        var mapEvents = new MapEventsPostgreSqlService(repository);
        Assert.True(mapEvents.TryInsertCatalog("pnj_gardien", "Autre", out _, out var insertError), insertError);

        var result = await QuickTalkingNpcPublisher.PublishAsync(
            new InMemoryPhase8ContentEditorService(),
            mapEvents,
            new QuickTalkingNpcRequest
            {
                Name = "Gardien",
                Text = "Encore.",
                TriggerKind = Phase8MapEventTriggerKinds.Action,
            });

        Assert.True(result.Success, result.Error);
        Assert.Equal("pnj_gardien_2", result.Slug);
        var stored = await repository.LoadByIdAsync(result.EventId);
        Assert.Equal("pnj_gardien_2", stored!.Definition.CatalogSlug);
    }

    [Fact]
    public async Task Publish_RollsBackDialogueWhenPagePublishFails()
    {
        var dialogues = new InMemoryPhase8ContentEditorService();
        var repository = new FailPagePublishRepository();
        var result = await QuickTalkingNpcPublisher.PublishAsync(
            dialogues,
            new MapEventsPostgreSqlService(repository),
            new QuickTalkingNpcRequest
            {
                Name = "Gardien",
                Text = "Halte.",
                TriggerKind = Phase8MapEventTriggerKinds.Action,
            });

        Assert.False(result.Success);
        Assert.Contains("pages", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await dialogues.ListAsync(Phase8ContentKind.Dialogue));
        Assert.Empty(await repository.ListSummariesAsync());
    }

    [Fact]
    public async Task Publish_DoesNotWriteWhenTextIsEmpty()
    {
        var dialogues = new InMemoryPhase8ContentEditorService();
        var repository = new InMemoryMapEventRepository();
        var result = await QuickTalkingNpcPublisher.PublishAsync(
            dialogues,
            new MapEventsPostgreSqlService(repository),
            new QuickTalkingNpcRequest
            {
                Name = "Gardien",
                Text = "  ",
                TriggerKind = Phase8MapEventTriggerKinds.Action,
            });

        Assert.False(result.Success);
        Assert.Empty(await dialogues.ListAsync(Phase8ContentKind.Dialogue));
        Assert.Empty(await repository.ListSummariesAsync());
    }

    [Fact]
    public void Dialog_FrenchLabels_DefaultTriggerIsAction()
    {
        StaTestRunner.Run(() =>
        {
            using var dlg = new QuickTalkingNpcDialog(
                new InMemoryPhase8ContentEditorService(),
                new MapEventsPostgreSqlService(new InMemoryMapEventRepository()));
            dlg.Show();
            try
            {
                Assert.Equal("PNJ rapide", dlg.Text);
                Assert.Equal("Créer et placer", dlg.CreateButtonForTest.Text);
                Assert.True(dlg.ActionForTest.Checked);
                Assert.Equal("Action (E)", dlg.ActionForTest.Text);
                Assert.Equal("Contact joueur", dlg.ContactForTest.Text);
                Assert.Equal(Phase8MapEventTriggerKinds.Action, dlg.SelectedTriggerForTest);
                dlg.ContactForTest.Checked = true;
                Assert.Equal(Phase8MapEventTriggerKinds.PlayerContact, dlg.SelectedTriggerForTest);
            }
            finally
            {
                dlg.Close();
            }
        });
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
