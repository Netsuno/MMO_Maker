using System;
using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class QuickTalkingNpcDraftTests
{
    [Fact]
    public void TryCreate_Action_UsesStartDialogueNotShowText()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var ok = QuickTalkingNpcDraft.TryCreate(
            new QuickTalkingNpcDraftRequest
            {
                Name = "  Gardien  ",
                Text = "Halte, voyageur.",
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                DialogueId = id,
            },
            out var draft,
            out var error);

        Assert.True(ok, error);
        Assert.NotNull(draft);
        Assert.Equal(id, draft!.DialogueId);
        Assert.Equal("Gardien", draft.DisplayName);
        Assert.Equal("pnj_gardien", draft.Slug);
        Assert.Equal(Phase8MapEventTriggerKinds.Action, draft.TriggerKind);
        Assert.Equal("Gardien", draft.Dialogue.Lines[0].Speaker);
        Assert.Equal("Halte, voyageur.", draft.Dialogue.Lines[0].Text);
        Assert.True(draft.Dialogue.Validate(out error), error);

        var command = Assert.Single(draft.Page.Commands);
        Assert.Equal(MapEventCommandDiscriminators.StartDialogue, command.Discriminator);
        Assert.NotEqual(MapEventCommandDiscriminators.ShowText, command.Discriminator);
        Assert.True(MapEventParameterSchemas.TryParseStartDialogue(command.ParameterJson, out var dialogueId, out error), error);
        Assert.Equal(id, dialogueId);
        Assert.Equal(Phase8MapEventTriggerKinds.Action, draft.Page.TriggerKind);
        Assert.True(draft.Page.Validate(out error), error);
    }

    [Fact]
    public void TryCreate_PlayerContact_AndAccentedName()
    {
        var ok = QuickTalkingNpcDraft.TryCreate(
            new QuickTalkingNpcDraftRequest
            {
                Name = "Élodie",
                Text = "Bonjour.",
                TriggerKind = Phase8MapEventTriggerKinds.PlayerContact,
            },
            out var draft,
            out var error);

        Assert.True(ok, error);
        Assert.NotNull(draft);
        Assert.Equal("pnj_elodie", draft!.Slug);
        Assert.Equal(Phase8MapEventTriggerKinds.PlayerContact, draft.Page.TriggerKind);
        Assert.Equal(MapEventCommandDiscriminators.StartDialogue, Assert.Single(draft.Page.Commands).Discriminator);
    }

    [Theory]
    [InlineData("", "Bonjour.", Phase8MapEventTriggerKinds.Action)]
    [InlineData("Gardien", "   ", Phase8MapEventTriggerKinds.Action)]
    [InlineData("Gardien", "Bonjour.", "autorun")]
    [InlineData("Gardien", "Bonjour.", "show_text")]
    public void TryCreate_RejectsIncompleteInput(string name, string text, string trigger)
    {
        var ok = QuickTalkingNpcDraft.TryCreate(
            new QuickTalkingNpcDraftRequest
            {
                Name = name,
                Text = text,
                TriggerKind = trigger,
            },
            out var draft,
            out var error);

        Assert.False(ok);
        Assert.Null(draft);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryCreate_RejectsTextLongerThan512()
    {
        var ok = QuickTalkingNpcDraft.TryCreate(
            new QuickTalkingNpcDraftRequest
            {
                Name = "Gardien",
                Text = new string('a', 513),
                TriggerKind = Phase8MapEventTriggerKinds.Action,
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("512", error, StringComparison.Ordinal);
    }

    [Fact]
    public void WithAttemptSuffix_StaysWithinSlugLimit()
    {
        var slug = new string('a', 64);
        var second = QuickTalkingNpcDraft.WithAttemptSuffix(slug, 2);
        Assert.EndsWith("_2", second, StringComparison.Ordinal);
        Assert.True(second.Length <= 64);
        Assert.Equal(slug, QuickTalkingNpcDraft.WithAttemptSuffix(slug, 1));
    }
}
