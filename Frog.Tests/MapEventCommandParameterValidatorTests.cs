using System;
using System.Text.Json;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventCommandParameterValidatorTests
{
    [Fact]
    public void ValidateParameters_RejectsUnsupportedSchemaVersion()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            SchemaVersion = 2,
            ParameterJson = """{"text":"Hi"}""",
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.Contains("SchemaVersion", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_RejectsUnknownJsonProperty()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            ParameterJson = """{"text":"Hi","extra":true}""",
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.Contains("inconnue", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_AcceptsGiveItemWithOnceKey()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.GiveItem,
            ParameterJson = $$"""{"itemId":"{{Guid.NewGuid():D}}","quantity":1,"onceKey":"chest-a"}""",
        };
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out _));
    }

    [Fact]
    public void ValidateParameters_RejectsUnknownDiscriminator()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = "not_a_real_command",
            ParameterJson = "{}",
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out _));
    }

    [Fact]
    public void ValidateParameters_AcceptsNestedBranchAtMaxDepth()
    {
        var command = NestBranches(MapEventRuntimeLimits.MaxBranchDepth);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out var error), error);
    }

    [Fact]
    public void ValidateParameters_RejectsNestedBranchBeyondMaxDepth()
    {
        var command = NestBranches(MapEventRuntimeLimits.MaxBranchDepth + 1);
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.Contains("branche", error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MapEventConditionKinds.CharacterSwitch, """{"switchId":"gate_open","value":true}""")]
    [InlineData(MapEventConditionKinds.CharacterVariableCompare, """{"variableId":"score","op":"gte","value":10}""")]
    [InlineData(MapEventConditionKinds.QuestStatus, """{"questId":"11111111-2222-3333-4444-555555555555","status":"ready"}""")]
    [InlineData(MapEventConditionKinds.ItemQuantity, """{"itemId":"11111111-2222-3333-4444-555555555555","quantity":2}""")]
    [InlineData(MapEventConditionKinds.CharacterLevel, """{"minLevel":5}""")]
    [InlineData(MapEventConditionKinds.ProfessionLevel, """{"professionId":"11111111-2222-3333-4444-555555555555","minLevel":3}""")]
    [InlineData(MapEventConditionKinds.MapOrRegion, """{"mapId":4}""")]
    public void PageValidate_AcceptsEveryConditionKind(string kind, string parameterJson)
    {
        var page = new MapEventPageDefinition
        {
            PageOrder = 0,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            Conditions =
            [
                new MapEventConditionDefinition { Kind = kind, ParameterJson = parameterJson },
            ],
            Commands =
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ShowText,
                    ParameterJson = """{"text":"ok"}""",
                },
            ],
        };

        Assert.True(page.Validate(out var error), error);
    }

    private static MapEventCommandDefinition NestBranches(int depth)
    {
        if (depth <= 0)
        {
            return new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.ShowText,
                ParameterJson = """{"text":"leaf"}""",
            };
        }

        var inner = NestBranches(depth - 1);
        return new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.Branch,
            ParameterJson = JsonSerializer.Serialize(new
            {
                conditionKind = MapEventConditionKinds.CharacterSwitch,
                conditionParameterJson = """{"switchId":"gate_open","value":true}""",
                thenCommands = new[]
                {
                    new { discriminator = inner.Discriminator, parameterJson = inner.ParameterJson },
                },
                elseCommands = Array.Empty<object>(),
            }),
        };
    }
}
