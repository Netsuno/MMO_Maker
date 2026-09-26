using System;
using System.Linq;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventCommandPaletteTests
{
    [Fact]
    public void Entries_CoverTheMvpPackInFrench()
    {
        var ids = MapEventCommandPalette.Entries.Select(entry => entry.Id).ToArray();
        Assert.Equal(
            [
                MapEventCommandPalette.ShowTextId,
                MapEventCommandPalette.ShowChoicesId,
                MapEventCommandPalette.PlayBgmId,
                MapEventCommandPalette.PlaySeId,
                MapEventCommandPalette.OpenShopId,
                MapEventCommandPalette.SetSwitchId,
                MapEventCommandPalette.SetVariableId,
                MapEventCommandPalette.AddVariableId,
                MapEventCommandPalette.SubVariableId,
                MapEventCommandPalette.BranchSwitchId,
                MapEventCommandPalette.BranchVariableId,
                MapEventCommandPalette.SetWeatherId,
            ],
            ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Texte");
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Afficher choix");
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Jouer BGM");
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Jouer SE");
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Ouvrir boutique");
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Si variable");
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Changer météo");
    }

    [Theory]
    [MemberData(nameof(PaletteIds))]
    public void TryCreate_ValidatesAndKeepsStoredDiscriminator(string id)
    {
        Assert.True(MapEventCommandPalette.TryCreate(id, out var command));
        Assert.True(command.Validate(out var error), error);
        Assert.Equal(1, command.SchemaVersion);
        if (id == MapEventCommandPalette.OpenShopId)
        {
            // Placeholder Guid.Empty until the author picks a shop. See TryCreate_OpenShop_UsesEmptyGuidPlaceholder.
            Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out error));
            Assert.Contains("shopId", error, StringComparison.OrdinalIgnoreCase);
            return;
        }

        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out error), error);
    }

    [Fact]
    public void TryCreate_ShowTextSwitchAndVariable_RoundTrip()
    {
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ShowTextId, out var text));
        Assert.True(MapEventParameterSchemas.TryParseShowText(text.ParameterJson, out var shown, out var showErr), showErr);
        Assert.Equal("Bonjour.", shown);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.SetSwitchId, out var sw));
        Assert.True(
            MapEventParameterSchemas.TryParseSetSwitch(sw.ParameterJson, out var switchId, out var on, out var swErr),
            swErr);
        Assert.Equal(MapEventCommandPalette.DefaultSwitchId, switchId);
        Assert.True(on);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.SetVariableId, out var set));
        Assert.True(
            MapEventParameterSchemas.TryParseSetVariable(set.ParameterJson, out var setId, out var setValue, out var setErr),
            setErr);
        Assert.Equal(MapEventCommandPalette.DefaultVariableId, setId);
        Assert.Equal(0, setValue);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.AddVariableId, out var add));
        Assert.True(
            MapEventParameterSchemas.TryParseAddVariable(add.ParameterJson, out var addId, out var delta, out var addErr),
            addErr);
        Assert.Equal(MapEventCommandPalette.DefaultVariableId, addId);
        Assert.Equal(1, delta);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.SubVariableId, out var sub));
        Assert.Equal(MapEventCommandDiscriminators.SubVariable, sub.Discriminator);
    }

    [Fact]
    public void TryCreate_ShowChoicesAndAudio_RoundTrip()
    {
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ShowChoicesId, out var choices));
        Assert.Equal(MapEventCommandDiscriminators.ShowChoices, choices.Discriminator);
        Assert.True(
            MapEventParameterSchemas.TryParseShowChoices(
                choices.ParameterJson,
                out var labels,
                out var cancel,
                out var branches,
                out var cancelCommands,
                out var choicesErr),
            choicesErr);
        Assert.Equal(["Oui", "Non"], labels);
        Assert.Equal(MapEventShowChoices.CancelDisallow, cancel);
        Assert.Equal(2, branches.Count);
        Assert.All(branches, branch => Assert.Empty(branch));
        Assert.Empty(cancelCommands);
        Assert.Contains("Oui", MapEventEditorLabels.CommandSummary(choices.Discriminator, choices.ParameterJson), StringComparison.Ordinal);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.PlayBgmId, out var bgm));
        Assert.True(
            MapEventParameterSchemas.TryParsePlayAudio(
                bgm.ParameterJson,
                bgm.Discriminator,
                out var bgmTrack,
                out var bgmErr),
            bgmErr);
        Assert.Equal("Assets/Audio/music-loop.wav", bgmTrack.Asset);
        Assert.Equal(MapAudioTrack.DefaultVolume, bgmTrack.Volume);
        Assert.Equal(0, bgmTrack.FadeMs);
        Assert.Contains("Jouer BGM", MapEventEditorLabels.CommandSummary(bgm.Discriminator, bgm.ParameterJson), StringComparison.Ordinal);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.PlaySeId, out var se));
        Assert.Equal(MapEventCommandDiscriminators.PlaySe, se.Discriminator);
        Assert.Contains("ui-click.wav", se.ParameterJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_Branches_UseSwitchAndVariableComparisons()
    {
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.BranchSwitchId, out var sw));
        Assert.True(
            MapEventParameterSchemas.TryParseBranch(
                sw.ParameterJson,
                out var swCond,
                out var swThen,
                out var swElse,
                out var swErr),
            swErr);
        Assert.Equal(MapEventConditionKinds.CharacterSwitch, swCond.Kind);
        Assert.True(swCond.Validate(out var condErr), condErr);
        Assert.Empty(swThen);
        Assert.Empty(swElse);
        Assert.Contains("Interrupteur", MapEventEditorLabels.CommandSummary(sw.Discriminator, sw.ParameterJson), StringComparison.Ordinal);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.BranchVariableId, out var variable));
        Assert.True(
            MapEventParameterSchemas.TryParseBranch(
                variable.ParameterJson,
                out var varCond,
                out _,
                out _,
                out var varErr),
            varErr);
        Assert.Equal(MapEventConditionKinds.CharacterVariableCompare, varCond.Kind);
        Assert.True(
            MapEventParameterSchemas.TryParseCharacterVariableCompare(
                varCond.ParameterJson,
                out var variableId,
                out var op,
                out var expected,
                out var cmpErr),
            cmpErr);
        Assert.Equal(MapEventCommandPalette.DefaultVariableId, variableId);
        Assert.Equal("gte", op);
        Assert.Equal(1, expected);
        Assert.True(MapEventParameterSchemas.EvaluateVariableCompare(1, op, expected));
        Assert.False(MapEventParameterSchemas.EvaluateVariableCompare(0, op, expected));
        Assert.Contains("Variable", MapEventEditorLabels.CommandSummary(variable.Discriminator, variable.ParameterJson), StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_OpenShop_UsesEmptyGuidPlaceholder()
    {
        Assert.Equal(Guid.Empty, MapEventCommandPalette.OpenShopPlaceholderId);
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.OpenShopId, out var command));
        Assert.Equal(MapEventCommandDiscriminators.OpenShop, command.Discriminator);
        Assert.Contains(Guid.Empty.ToString("D"), command.ParameterJson, StringComparison.OrdinalIgnoreCase);
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.Contains("shopId", error, StringComparison.OrdinalIgnoreCase);

        var picked = Guid.Parse("aaaaaaaa-0005-4000-8000-000000000001");
        var ready = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.OpenShop,
            ParameterJson = $$"""{"shopId":"{{picked:D}}","shopName":"Échoppe"}""",
        };
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(ready, out var readyError), readyError);
        Assert.True(
            MapEventParameterSchemas.TryParseOpenShop(ready.ParameterJson, out var parsed, out var parsedName, out readyError),
            readyError);
        Assert.Equal(picked, parsed);
        Assert.Equal("Échoppe", parsedName);
        Assert.Equal(
            "Ouvrir boutique : Échoppe",
            MapEventEditorLabels.CommandSummary(ready.Discriminator, ready.ParameterJson));
        Assert.Equal(
            "Ouvrir boutique " + picked.ToString("D")[..8] + "…",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.OpenShop,
                $$"""{"shopId":"{{picked:D}}"}"""));
        Assert.Equal("Ouvrir boutique", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.OpenShop));
        Assert.Equal("Boutique", MapEventEditorLabels.Field("shopPick"));
    }

    [Fact]
    public void TryCreate_UnknownId_Fails()
    {
        Assert.False(MapEventCommandPalette.TryCreate("teleport", out _));
        Assert.False(MapEventCommandPalette.TryCreate(null, out _));
    }

    public static TheoryData<string> PaletteIds()
    {
        var data = new TheoryData<string>();
        foreach (var entry in MapEventCommandPalette.Entries)
        {
            data.Add(entry.Id);
        }

        return data;
    }
}
