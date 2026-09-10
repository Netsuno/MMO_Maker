using System;
using System.Collections.Generic;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class CommonEventCycleDetectorTests
{
    [Fact]
    public void DetectCycles_ReturnsNull_WhenNoCalls()
    {
        var events = new List<CommonEventDefinition>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "A",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.ShowText,
                                ParameterJson = """{"text":"hi"}""",
                            },
                        ],
                    },
                ],
            },
        };

        Assert.Null(CommonEventCycleDetector.DetectCycles(events));
    }

    [Fact]
    public void DetectCycles_ReturnsNull_WhenAcyclicNestedCalls()
    {
        var parentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
        var childId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02");
        var events = new List<CommonEventDefinition>
        {
            new()
            {
                Id = parentId,
                Name = "Parent",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                                ParameterJson = $$$"""{"commonEventId":"{{{childId}}}"}""",
                            },
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.ShowText,
                                ParameterJson = """{"text":"from-parent"}""",
                            },
                        ],
                    },
                ],
            },
            new()
            {
                Id = childId,
                Name = "Child",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.SetSwitch,
                                ParameterJson = """{"switchId":"nested_flag","value":true}""",
                            },
                        ],
                    },
                ],
            },
        };

        Assert.Null(CommonEventCycleDetector.DetectCycles(events));
    }

    [Fact]
    public void DetectCycles_ReportsCycle_WhenMutualCalls()
    {
        var idA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var idB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var events = new List<CommonEventDefinition>
        {
            new()
            {
                Id = idA,
                Name = "Alpha",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                                ParameterJson = $$$"""{"commonEventId":"{{{idB}}}"}""",
                            },
                        ],
                    },
                ],
            },
            new()
            {
                Id = idB,
                Name = "Beta",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                                ParameterJson = $$$"""{"commonEventId":"{{{idA}}}"}""",
                            },
                        ],
                    },
                ],
            },
        };

        var error = CommonEventCycleDetector.DetectCycles(events);
        Assert.NotNull(error);
        Assert.Contains("Cycle call_common_event", error, StringComparison.Ordinal);
        Assert.Contains("Alpha", error, StringComparison.Ordinal);
        Assert.Contains("Beta", error, StringComparison.Ordinal);
    }
}
