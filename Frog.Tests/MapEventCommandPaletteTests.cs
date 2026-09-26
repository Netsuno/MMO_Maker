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
                MapEventCommandPalette.SetSwitchId,
                MapEventCommandPalette.SetVariableId,
                MapEventCommandPalette.AddVariableId,
                MapEventCommandPalette.SubVariableId,
                MapEventCommandPalette.BranchSwitchId,
                MapEventCommandPalette.BranchVariableId,
            ],
            ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Texte");
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Label == "Si variable");
    }

    [Theory]
    [MemberData(nameof(PaletteIds))]
    public void TryCreate_ValidatesAndKeepsStoredDiscriminator(string id)
    {
        Assert.True(MapEventCommandPalette.TryCreate(id, out var command));
        Assert.True(command.Validate(out var error), error);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out error), error);
        Assert.Equal(1, command.SchemaVersion);
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
