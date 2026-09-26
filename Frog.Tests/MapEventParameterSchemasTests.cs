using System;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventParameterSchemasTests
{
    [Fact]
    public void TryParseShowText_ValidJson()
    {
        Assert.True(MapEventParameterSchemas.TryParseShowText("""{"text":"Bonjour"}""", out var text, out var err));
        Assert.Equal("Bonjour", text);
        Assert.Null(err);
    }

    [Fact]
    public void TryParseSetSwitch_ValidJson()
    {
        Assert.True(MapEventParameterSchemas.TryParseSetSwitch(
            """{"switchId":"door_open","value":true}""",
            out var id,
            out var value,
            out var err));
        Assert.Equal("door_open", id);
        Assert.True(value);
        Assert.Null(err);
    }

    [Fact]
    public void TryParseCharacterSwitchCondition_ValidJson()
    {
        Assert.True(MapEventParameterSchemas.TryParseCharacterSwitchCondition(
            """{"switchId":"quest_a","value":false}""",
            out var id,
            out var expected,
            out var err));
        Assert.Equal("quest_a", id);
        Assert.False(expected);
        Assert.Null(err);
    }

    [Fact]
    public void TryParseAddVariable_ValidJson()
    {
        Assert.True(MapEventParameterSchemas.TryParseAddVariable(
            """{"variableId":"score","delta":10}""",
            out var id,
            out var delta,
            out var err));
        Assert.Equal("score", id);
        Assert.Equal(10, delta);
        Assert.Null(err);
    }

    [Fact]
    public void TryParseShowChoices_AcceptsTwoToFourAndRejectsCancelPastEnd()
    {
        var json = MapEventParameterSchemas.SerializeShowChoices(
            ["Oui", "Non", "Peut-être"],
            MapEventShowChoices.CancelChoice2,
            [
                [new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ShowText,
                    ParameterJson = """{"text":"branche"}""",
                }],
                [],
                [],
            ],
            []);
        Assert.True(
            MapEventParameterSchemas.TryParseShowChoices(
                json,
                out var labels,
                out var cancel,
                out var branches,
                out var cancelCommands,
                out var error),
            error);
        Assert.Equal(["Oui", "Non", "Peut-être"], labels);
        Assert.Equal(MapEventShowChoices.CancelChoice2, cancel);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, Assert.Single(branches[0]).Discriminator);
        Assert.Empty(cancelCommands);
        Assert.True(new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowChoices,
            ParameterJson = json,
        }.Validate(out var shapeErr), shapeErr);

        var page = new MapEventPageDefinition
        {
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            Commands =
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ShowChoices,
                    ParameterJson = json,
                },
            ],
        };
        Assert.True(page.Validate(out var pageErr), pageErr);

        Assert.False(MapEventParameterSchemas.TryParseShowChoices(
            """{"choices":["Seul"],"cancel":"disallow","branches":[[]],"cancelCommands":[]}""",
            out _,
            out _,
            out _,
            out _,
            out var shortErr));
        Assert.Contains("2 et 4", shortErr, StringComparison.Ordinal);

        Assert.False(MapEventParameterSchemas.TryParseShowChoices(
            """{"choices":["A","B"],"cancel":"choice_4","branches":[[],[]],"cancelCommands":[]}""",
            out _,
            out _,
            out _,
            out _,
            out var cancelErr));
        Assert.Contains("absent", cancelErr, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Fact]
    public void TryParsePlayAudio_UsesMapTrackRulesWithoutPitch()
    {
        Assert.True(MapEventParameterSchemas.TryParsePlayAudio(
            """{"asset":"Assets/Audio/music-loop.wav","volume":80,"fadeMs":250}""",
            MapEventCommandDiscriminators.PlayBgm,
            out var track,
            out var error), error);
        Assert.Equal("Assets/Audio/music-loop.wav", track.Asset);
        Assert.Equal(80, track.Volume);
        Assert.Equal(250, track.FadeMs);
        Assert.False(MapEventParameterSchemas.TryParsePlayAudio(
            """{"asset":"/tmp/secret.wav","volume":80,"fadeMs":0}""",
            MapEventCommandDiscriminators.PlaySe,
            out _,
            out var absErr));
        Assert.False(string.IsNullOrWhiteSpace(absErr));
        Assert.False(MapEventParameterSchemas.TryParsePlayAudio(
            """{"asset":"cue","volume":140,"fadeMs":0}""",
            MapEventCommandDiscriminators.PlayBgm,
            out _,
            out var volumeErr));
        Assert.Contains("volume", volumeErr, StringComparison.OrdinalIgnoreCase);
        Assert.False(MapEventParameterSchemas.TryParsePlayAudio(
            """{"asset":"cue","volume":80,"fadeMs":0,"pitch":100}""",
            MapEventCommandDiscriminators.PlayBgm,
            out _,
            out var pitchErr));
        Assert.Contains("inconnue", pitchErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateVariableCompare_Operators()
    {
        Assert.True(MapEventParameterSchemas.EvaluateVariableCompare(5, "eq", 5));
        Assert.True(MapEventParameterSchemas.EvaluateVariableCompare(3, "lt", 5));
        Assert.False(MapEventParameterSchemas.EvaluateVariableCompare(7, "lte", 5));
    }
}
