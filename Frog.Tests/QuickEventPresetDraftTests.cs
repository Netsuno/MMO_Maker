using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class QuickEventPresetDraftTests
{
    [Fact]
    public void Chest_Blocks_SetsSwitch_AndCountsLoot()
    {
        var ok = QuickEventPresetDraft.TryCreate("  Coffre du roi  ", QuickEventPresetKind.Chest, out var draft, out var error);
        Assert.True(ok, error);
        Assert.NotNull(draft);
        Assert.Equal("Coffre du roi", draft!.DisplayName);
        Assert.Equal("coffre_du_roi", draft.Slug);
        Assert.Equal(Phase8MapEventTriggerKinds.Action, draft.TriggerKind);
        Assert.Equal("coffre_du_roi_ouvert", draft.SwitchKey);
        Assert.Equal("coffre_du_roi_butin", draft.CounterKey);
        Assert.Equal(QuickEventPresetTexts.ChestOpen, "Vous ouvrez le coffre.");
        Assert.Equal(QuickEventPresetTexts.ChestEmpty, "Le coffre est vide.");

        Assert.Equal(2, draft.Pages.Count);
        Assert.All(draft.Pages, page =>
        {
            Assert.True(page.BlocksCollision);
            Assert.Equal(Phase8MapEventTriggerKinds.Action, page.TriggerKind);
            Assert.Equal(MapEventMovementKinds.Fixed, page.MovementKind);
            Assert.True(page.Validate(out var pageError), pageError);
        });

        var closed = draft.Pages[0];
        Assert.Equal(0, closed.Priority);
        Assert.Empty(closed.Conditions);
        Assert.Equal(
            [MapEventCommandDiscriminators.ShowText, MapEventCommandDiscriminators.SetSwitch, MapEventCommandDiscriminators.AddVariable],
            closed.Commands.Select(command => command.Discriminator).ToArray());
        Assert.Equal(QuickEventPresetTexts.ChestOpen, TextOf(closed.Commands[0]));
        Assert.True(MapEventParameterSchemas.TryParseSetSwitch(closed.Commands[1].ParameterJson, out var switchId, out var switchValue, out error), error);
        Assert.Equal(draft.SwitchKey, switchId);
        Assert.True(switchValue);
        Assert.True(MapEventParameterSchemas.TryParseAddVariable(closed.Commands[2].ParameterJson, out var variableId, out var delta, out error), error);
        Assert.Equal(draft.CounterKey, variableId);
        Assert.Equal(1, delta);

        var opened = draft.Pages[1];
        Assert.Equal(1, opened.Priority);
        Assert.True(opened.BlocksCollision);
        var condition = Assert.Single(opened.Conditions);
        Assert.Equal(MapEventConditionKinds.CharacterSwitch, condition.Kind);
        Assert.True(MapEventParameterSchemas.TryParseCharacterSwitchCondition(condition.ParameterJson, out switchId, out var expected, out error), error);
        Assert.Equal(draft.SwitchKey, switchId);
        Assert.True(expected);
        Assert.Equal(QuickEventPresetTexts.ChestEmpty, TextOf(Assert.Single(opened.Commands)));
    }

    [Fact]
    public void Door_StaysBlocking_OnTheOpenPage()
    {
        var ok = QuickEventPresetDraft.TryCreate("Porte nord", QuickEventPresetKind.Door, out var draft, out var error);
        Assert.True(ok, error);
        Assert.Equal("porte_nord", draft!.Slug);
        Assert.Equal("porte_nord_ouverte", draft.SwitchKey);
        Assert.Null(draft.CounterKey);
        Assert.All(draft.Pages, page => Assert.True(page.BlocksCollision));
        Assert.Equal(1, draft.Pages.Max(page => page.Priority));
        Assert.True(draft.Pages.Single(page => page.Priority == draft.Pages.Max(p => p.Priority)).BlocksCollision);

        Assert.Equal(QuickEventPresetTexts.DoorOpen, TextOf(draft.Pages[0].Commands[0]));
        Assert.Equal(QuickEventPresetTexts.DoorAlreadyOpen, "La porte est ouverte.");
        Assert.Equal(QuickEventPresetTexts.DoorAlreadyOpen, TextOf(Assert.Single(draft.Pages[1].Commands)));
        Assert.Equal(MapEventCommandDiscriminators.SetSwitch, draft.Pages[0].Commands[1].Discriminator);
    }

    [Fact]
    public void Inn_BranchesOnNightCount()
    {
        var ok = QuickEventPresetDraft.TryCreate("Auberge", QuickEventPresetKind.Inn, out var draft, out var error);
        Assert.True(ok, error);
        Assert.Equal("auberge_auberge", draft!.Slug);
        Assert.Equal("auberge_auberge_repose", draft.SwitchKey);
        Assert.Equal("auberge_auberge_nuits", draft.CounterKey);
        Assert.All(draft.Pages, page => Assert.True(page.BlocksCollision));
        Assert.Equal(QuickEventPresetTexts.InnWelcome, "Bienvenue à l'auberge. Vous vous reposez.");
        Assert.Equal(QuickEventPresetTexts.InnWelcome, TextOf(draft.Pages[0].Commands[0]));
        Assert.Equal(MapEventCommandDiscriminators.SetSwitch, draft.Pages[0].Commands[1].Discriminator);
        Assert.True(MapEventParameterSchemas.TryParseSetVariable(draft.Pages[0].Commands[2].ParameterJson, out var variableId, out var nights, out error), error);
        Assert.Equal(draft.CounterKey, variableId);
        Assert.Equal(1, nights);

        var branch = draft.Pages[1].Commands[0];
        Assert.Equal(MapEventCommandDiscriminators.Branch, branch.Discriminator);
        Assert.True(MapEventParameterSchemas.TryParseBranch(branch.ParameterJson, out var condition, out var thenCommands, out var elseCommands, out error), error);
        Assert.Equal(MapEventConditionKinds.CharacterVariableCompare, condition.Kind);
        Assert.True(MapEventParameterSchemas.TryParseCharacterVariableCompare(condition.ParameterJson, out variableId, out var op, out var threshold, out error), error);
        Assert.Equal(draft.CounterKey, variableId);
        Assert.Equal("gte", op);
        Assert.Equal(QuickEventPresetTexts.InnRegularNights, threshold);
        Assert.Equal(3, QuickEventPresetTexts.InnRegularNights);
        Assert.Equal(QuickEventPresetTexts.InnRegular, TextOf(Assert.Single(thenCommands)));
        Assert.Equal("Vous êtes un habitué. La chambre est prête.", QuickEventPresetTexts.InnRegular);
        Assert.Equal(QuickEventPresetTexts.InnReturn, TextOf(Assert.Single(elseCommands)));
        Assert.Equal("Bon retour. Une nuit de plus.", QuickEventPresetTexts.InnReturn);
        Assert.Equal(MapEventCommandDiscriminators.AddVariable, draft.Pages[1].Commands[1].Discriminator);
    }

    [Fact]
    public void AccentedName_FoldsIntoSlug()
    {
        var ok = QuickEventPresetDraft.TryCreate("Élodie", QuickEventPresetKind.Chest, out var draft, out var error);
        Assert.True(ok, error);
        Assert.Equal("coffre_elodie", draft!.Slug);
        Assert.Equal("coffre_elodie_ouvert", draft.SwitchKey);
    }

    [Theory]
    [InlineData("", QuickEventPresetKind.Chest)]
    [InlineData("   ", QuickEventPresetKind.Door)]
    [InlineData(null, QuickEventPresetKind.Inn)]
    public void TryCreate_RejectsEmptyName(string? name, QuickEventPresetKind kind)
    {
        var ok = QuickEventPresetDraft.TryCreate(name, kind, out var draft, out var error);
        Assert.False(ok);
        Assert.Null(draft);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryCreate_RejectsUnknownKindAndLongName()
    {
        Assert.False(QuickEventPresetDraft.TryCreate("Coffre", (QuickEventPresetKind)99, out _, out var error));
        Assert.Contains("inconnu", error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var ok = QuickEventPresetDraft.TryCreate(new string('a', 129), QuickEventPresetKind.Door, out _, out error);
        Assert.False(ok);
        Assert.Contains("128", error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Pages_RoundTripThroughCodec()
    {
        Assert.True(QuickEventPresetDraft.TryCreate("Coffre", QuickEventPresetKind.Chest, out var draft, out var error), error);
        var json = MapEventPagesCodec.SerializePages(draft!.Pages);
        Assert.True(MapEventPagesCodec.TryDeserializePages(json, out var pages, out error), error);
        var definition = new MapEventDefinition
        {
            Name = draft.DisplayName,
            CatalogSlug = draft.Slug,
            Pages = pages,
        };
        Assert.True(definition.Validate(out error), error);
        Assert.Equal(draft.Pages.Count, pages.Count);
        Assert.Equal(draft.Pages[0].Commands[0].Discriminator, pages[0].Commands[0].Discriminator);
        Assert.True(pages[1].BlocksCollision);
    }

    [Fact]
    public async Task PageSelector_UsesTheOpenPageOnlyWhenTheSwitchPasses()
    {
        Assert.True(QuickEventPresetDraft.TryCreate("Porte", QuickEventPresetKind.Door, out var draft, out var error), error);
        var closed = await MapEventPageSelector.SelectBestPageAsync(
            draft!.Pages,
            Phase8MapEventTriggerKinds.Action,
            _ => Task.FromResult(false));
        Assert.Equal(0, closed!.PageOrder);
        Assert.Equal(QuickEventPresetTexts.DoorOpen, TextOf(closed.Commands[0]));

        var opened = await MapEventPageSelector.SelectBestPageAsync(
            draft.Pages,
            Phase8MapEventTriggerKinds.Action,
            _ => Task.FromResult(true));
        Assert.Equal(1, opened!.PageOrder);
        Assert.Equal(QuickEventPresetTexts.DoorAlreadyOpen, TextOf(Assert.Single(opened.Commands)));

        var ignored = await MapEventPageSelector.SelectBestPageAsync(
            draft.Pages,
            Phase8MapEventTriggerKinds.Autorun,
            _ => Task.FromResult(true));
        Assert.Null(ignored);
    }

    [Fact]
    public void StateKey_AndSlugSuffix_StayWithinLimits()
    {
        var slug = new string('a', 64);
        var key = QuickEventPresetDraft.StateKey(slug, "ouverte");
        Assert.EndsWith("_ouverte", key, StringComparison.Ordinal);
        Assert.True(key.Length <= 64);
        Assert.DoesNotContain("__", key, StringComparison.Ordinal);

        var second = QuickEventPresetDraft.WithAttemptSuffix(slug, 2);
        Assert.EndsWith("_2", second, StringComparison.Ordinal);
        Assert.True(second.Length <= 64);
        Assert.Equal(slug, QuickEventPresetDraft.WithAttemptSuffix(slug, 1));
    }

    private static string TextOf(MapEventCommandDefinition command)
    {
        Assert.Equal(MapEventCommandDiscriminators.ShowText, command.Discriminator);
        Assert.True(MapEventParameterSchemas.TryParseShowText(command.ParameterJson, out var text, out var error), error);
        return text;
    }
}
