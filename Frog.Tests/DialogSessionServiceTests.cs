using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;
using Frog.Server.Gameplay;
using Frog.Server.Persistence;
using Xunit;

namespace Frog.Tests;

public sealed class DialogSessionServiceTests
{
    [Fact]
    public async Task TryChooseAsync_RejectsWhenPublishedRevisionChanges()
    {
        var dialogueId = Guid.NewGuid();
        var questId = Guid.NewGuid();
        var phase8 = new Phase8InMemoryPublishedContent();
        phase8.RegisterDialogue(new DialogueDefinition
        {
            Id = dialogueId,
            Name = "Guide",
            Lines = [new DialogueLineDefinition { Speaker = "Guide", Text = "Original" }],
            Choices =
            [
                new DialogueChoiceDefinition { ChoiceId = "accept", Label = "Accept", StartQuestId = questId },
            ],
        });
        var catalog = new RevisionBindingDialogueCatalog(phase8, dialogueId, publishedRevision: 1);
        var sessions = new DialogSessionService(catalog, CreateQuestService(phase8));
        var characterId = Guid.NewGuid();

        var started = await sessions.TryStartSessionAsync(characterId, dialogueId);
        Assert.NotNull(started);
        Assert.Equal(1, started!.PublishedRevision);

        catalog.PublishedRevision = 2;
        var result = await sessions.TryChooseAsync(characterId, started.SessionToken, "accept");
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("republié", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryChooseAsync_ConcurrentRequests_OnlyOneSucceeds()
    {
        var dialogueId = Guid.NewGuid();
        var phase8 = new Phase8InMemoryPublishedContent();
        phase8.RegisterDialogue(new DialogueDefinition
        {
            Id = dialogueId,
            Name = "Guide",
            Lines = [new DialogueLineDefinition { Speaker = "Guide", Text = "Pick one" }],
            Choices = [new DialogueChoiceDefinition { ChoiceId = "accept", Label = "Accept" }],
        });
        var sessions = new DialogSessionService(new RevisionBindingDialogueCatalog(phase8, dialogueId, 1), CreateQuestService(phase8));
        var characterId = Guid.NewGuid();
        var started = await sessions.TryStartSessionAsync(characterId, dialogueId);
        Assert.NotNull(started);

        var results = await Task.WhenAll(
            sessions.TryChooseAsync(characterId, started!.SessionToken, "accept"),
            sessions.TryChooseAsync(characterId, started.SessionToken, "accept"));

        Assert.Equal(1, results.Count(r => r is { Success: true }));
        Assert.Equal(1, results.Count(r => r is { Success: false }));
    }

    [Fact]
    public async Task TryStartSessionAsync_BindsRevisionAndChoices()
    {
        var dialogueId = Guid.NewGuid();
        var phase8 = new Phase8InMemoryPublishedContent();
        phase8.RegisterDialogue(new DialogueDefinition
        {
            Id = dialogueId,
            Name = "Guide",
            Lines = [new DialogueLineDefinition { Speaker = "Guide", Text = "Bound text" }],
            Choices = [new DialogueChoiceDefinition { ChoiceId = "accept", Label = "Accept quest" }],
        });
        var sessions = new DialogSessionService(new RevisionBindingDialogueCatalog(phase8, dialogueId, 7), CreateQuestService(phase8));
        var started = await sessions.TryStartSessionAsync(Guid.NewGuid(), dialogueId);

        Assert.NotNull(started);
        Assert.Equal(7, started!.PublishedRevision);
        Assert.Equal("Bound text", started.Text);
        Assert.Contains(started.Choices, c => c.ChoiceId == "accept" && c.Label == "Accept quest");
    }

    private static QuestGameplayService CreateQuestService(Phase8InMemoryPublishedContent phase8)
    {
        var questRepo = new InMemoryCharacterQuestRepository();
        var characters = new InMemoryCharacterRepository();
        var items = new Phase7PublishedContent();
        var inventoryRepo = new InMemoryInventoryRepository();
        var inventory = new InventoryGameplayService(
            inventoryRepo,
            new InMemoryInventoryTransferRepository(inventoryRepo, new InMemoryEquipmentRepository(), new InMemoryGroundItemRepository(), items),
            new InMemoryGroundItemRepository(),
            items,
            new InMemoryEquipmentRepository());
        return new QuestGameplayService(
            phase8,
            questRepo,
            new InMemoryQuestMutationRepository(questRepo, characters, inventory, phase8));
    }

    private sealed class RevisionBindingDialogueCatalog(Phase8InMemoryPublishedContent inner, Guid dialogueId, long publishedRevision)
        : IPublishedDialogueCatalog
    {
        public long PublishedRevision { get; set; } = publishedRevision;

        public Task<IReadOnlyList<DialogueDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
            ((IPublishedDialogueCatalog)inner).ListPublishedAsync(cancellationToken);

        public Task<DialogueDefinition?> TryGetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            ((IPublishedDialogueCatalog)inner).TryGetPublishedByIdAsync(id, cancellationToken);

        public Task<DialogueDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default) =>
            ((IPublishedDialogueCatalog)inner).TryGetPublishedByAliasAsync(editorAliasId, cancellationToken);

        public Task<long?> TryGetPublishedRevisionByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(id == dialogueId ? PublishedRevision : null);
    }
}
