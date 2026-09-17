using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventPageSelectorTests
{
    [Fact]
    public async Task SelectBestPageAsync_PicksHighestPriorityMatchingConditions()
    {
        var pages = new List<MapEventPageDefinition>
        {
            new()
            {
                PageOrder = 0,
                Priority = 1,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Low"}""",
                    },
                ],
            },
            new()
            {
                PageOrder = 1,
                Priority = 10,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                Conditions =
                [
                    new MapEventConditionDefinition
                    {
                        Kind = MapEventConditionKinds.CharacterSwitch,
                        ParameterJson = """{"switchId":"door","value":true}""",
                    },
                ],
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"High"}""",
                    },
                ],
            },
        };

        var selected = await MapEventPageSelector.SelectBestPageAsync(
            pages,
            Phase8MapEventTriggerKinds.Action,
            condition => Task.FromResult(
                condition.Kind == MapEventConditionKinds.CharacterSwitch));

        Assert.NotNull(selected);
        Assert.Equal(10, selected!.Priority);
        Assert.Contains("High", selected.Commands[0].ParameterJson, StringComparison.Ordinal);
    }
}
