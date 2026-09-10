using System;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventDefinitionValidationTests
{
    [Fact]
    public void Valid_MinimalEvent_Passes()
    {
        var def = new MapEventDefinition
        {
            Name = "Gate",
            Pages =
            [
                new MapEventPageDefinition
                {
                    PageOrder = 0,
                    TriggerKind = Phase8MapEventTriggerKinds.Action,
                    Commands =
                    [
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"Hello"}""",
                        },
                    ],
                },
            ],
        };

        Assert.True(def.Validate(out var error), error);
    }

    [Fact]
    public void EmptyName_FailsValidation()
    {
        var def = new MapEventDefinition { Name = "" };
        Assert.False(def.Validate(out var error));
        Assert.Contains("nom", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicatePageOrder_Fails()
    {
        var def = new MapEventDefinition
        {
            Name = "Dup",
            Pages =
            [
                new MapEventPageDefinition { PageOrder = 0 },
                new MapEventPageDefinition { PageOrder = 0 },
            ],
        };

        Assert.False(def.Validate(out var error));
        Assert.Contains("dupliqué", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownCommand_Fails()
    {
        var def = new MapEventDefinition
        {
            Name = "BadCmd",
            Pages =
            [
                new MapEventPageDefinition
                {
                    PageOrder = 0,
                    Commands = [new MapEventCommandDefinition { Discriminator = "run_lua" }],
                },
            ],
        };

        Assert.False(def.Validate(out _));
    }

    [Fact]
    public void Phase8TriggerKinds_AreDistinctFromLegacyPage()
    {
        Assert.DoesNotContain("page", Phase8MapEventTriggerKinds.All);
        Assert.Equal("interact", Phase8MapEventTriggerKinds.ToWireTriggerKind(Phase8MapEventTriggerKinds.Action));
    }
}
