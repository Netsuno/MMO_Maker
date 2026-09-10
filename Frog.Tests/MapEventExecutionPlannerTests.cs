using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;
using ServerPlanner = Frog.Server.Gameplay.MapEventExecutionPlanner;

namespace Frog.Tests;

public sealed class MapEventExecutionPlannerTests
{
    private static readonly Guid CharacterId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid FixedRequestId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Plan_ResolvesBranchThen()
    {
        var commands = new[]
        {
            Branch(
                MapEventConditionKinds.CharacterSwitch,
                """{"switchId":"gate_open","value":true}""",
                thenCommands: [ShowText("then")],
                elseCommands: [ShowText("else")]),
        };

        var plan = MapEventExecutionPlanner.Plan(
            commands,
            InMemoryCommonEventSource.Empty,
            _ => true,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Equal(FixedRequestId, plan.Identity.RequestId);
        Assert.Equal((CharacterId, FixedRequestId), plan.Identity.LedgerKey);
        Assert.Single(plan.Effects);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, plan.Effects[0].Discriminator);
        Assert.Contains("then", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ResolvesBranchElse()
    {
        var commands = new[]
        {
            Branch(
                MapEventConditionKinds.CharacterSwitch,
                """{"switchId":"gate_open","value":true}""",
                thenCommands: [ShowText("then")],
                elseCommands: [ShowText("else")]),
        };

        var plan = MapEventExecutionPlanner.Plan(
            commands,
            InMemoryCommonEventSource.Empty,
            _ => false,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Single(plan.Effects);
        Assert.Contains("else", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ResolvesNestedBranch()
    {
        var inner = Branch(
            MapEventConditionKinds.CharacterVariableCompare,
            """{"variableId":"score","op":"gte","value":10}""",
            thenCommands: [ShowText("inner-then")],
            elseCommands: [ShowText("inner-else")]);
        var outer = Branch(
            MapEventConditionKinds.CharacterSwitch,
            """{"switchId":"gate_open","value":true}""",
            thenCommands: [inner],
            elseCommands: [ShowText("outer-else")]);

        var plan = MapEventExecutionPlanner.Plan(
            [outer],
            InMemoryCommonEventSource.Empty,
            condition => condition.Kind == MapEventConditionKinds.CharacterSwitch,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Single(plan.Effects);
        Assert.Contains("inner-else", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ExpandsCommonEvent()
    {
        var commonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var source = new InMemoryCommonEventSource(
        [
            new CommonEventDefinition
            {
                Id = commonId,
                Name = "Shared",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Priority = 1,
                        Commands =
                        [
                            ShowText("from-common"),
                            SetSwitch("shared_flag", true),
                        ],
                    },
                ],
            },
        ]);

        var plan = MapEventExecutionPlanner.Plan(
            [CallCommon(commonId)],
            source,
            _ => true,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Equal(2, plan.Effects.Count);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, plan.Effects[0].Discriminator);
        Assert.Equal(MapEventCommandDiscriminators.SetSwitch, plan.Effects[1].Discriminator);
        Assert.DoesNotContain(
            plan.Effects,
            c => c.Discriminator == MapEventCommandDiscriminators.CallCommonEvent);
    }

    [Fact]
    public void Plan_ExpandsNestedCommonEvent()
    {
        var parentId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccd1");
        var childId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccd2");
        var source = new InMemoryCommonEventSource(
        [
            new CommonEventDefinition
            {
                Id = parentId,
                Name = "Parent",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands =
                        [
                            CallCommon(childId),
                            ShowText("from-parent"),
                        ],
                    },
                ],
            },
            new CommonEventDefinition
            {
                Id = childId,
                Name = "Child",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands = [SetSwitch("nested_flag", true)],
                    },
                ],
            },
        ]);

        var plan = MapEventExecutionPlanner.Plan(
            [CallCommon(parentId)],
            source,
            _ => true,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Equal(2, plan.Effects.Count);
        Assert.Equal(MapEventCommandDiscriminators.SetSwitch, plan.Effects[0].Discriminator);
        Assert.Contains("nested_flag", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, plan.Effects[1].Discriminator);
        Assert.Contains("from-parent", plan.Effects[1].ParameterJson, StringComparison.Ordinal);
        Assert.DoesNotContain(
            plan.Effects,
            c => c.Discriminator == MapEventCommandDiscriminators.CallCommonEvent);
    }

    [Fact]
    public void Plan_ExpandsCommonEventBranch()
    {
        var commonId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var source = new InMemoryCommonEventSource(
        [
            new CommonEventDefinition
            {
                Id = commonId,
                Name = "Branched",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands =
                        [
                            Branch(
                                MapEventConditionKinds.CharacterSwitch,
                                """{"switchId":"ready","value":true}""",
                                thenCommands: [ShowText("ce-then")],
                                elseCommands: [ShowText("ce-else")]),
                        ],
                    },
                ],
            },
        ]);

        var plan = MapEventExecutionPlanner.Plan(
            [CallCommon(commonId)],
            source,
            _ => false,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Single(plan.Effects);
        Assert.Contains("ce-else", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RejectsCommonEventCycle()
    {
        var idA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var idB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var source = new InMemoryCommonEventSource(
        [
            CommonEventCalling("Alpha", idA, idB),
            CommonEventCalling("Beta", idB, idA),
        ]);

        var plan = MapEventExecutionPlanner.Plan(
            [CallCommon(idA)],
            source,
            _ => true,
            Identity());

        Assert.False(plan.IsSuccess);
        Assert.Empty(plan.Effects);
        Assert.Contains("Cycle common-event", plan.Error, StringComparison.Ordinal);
        Assert.Equal(FixedRequestId, plan.Identity.RequestId);
    }

    [Fact]
    public void Plan_RejectsMissingCommonEventReference()
    {
        var missing = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var plan = MapEventExecutionPlanner.Plan(
            [CallCommon(missing)],
            InMemoryCommonEventSource.Empty,
            _ => true,
            Identity());

        Assert.False(plan.IsSuccess);
        Assert.Empty(plan.Effects);
        Assert.Contains("introuvable", plan.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_RejectsMissingCommonEventAlias()
    {
        var plan = MapEventExecutionPlanner.Plan(
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                    ParameterJson = """{"editorAliasId":42}""",
                },
            ],
            InMemoryCommonEventSource.Empty,
            _ => true,
            Identity());

        Assert.False(plan.IsSuccess);
        Assert.Contains("introuvable", plan.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_RejectsBranchDeeperThanLimit()
    {
        var command = NestBranches(MapEventRuntimeLimits.MaxBranchDepth + 1);
        var plan = MapEventExecutionPlanner.Plan(
            [command],
            InMemoryCommonEventSource.Empty,
            _ => true,
            Identity());

        Assert.False(plan.IsSuccess);
        Assert.Contains("branche", plan.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_AcceptsBranchAtMaxDepth()
    {
        var command = NestBranches(MapEventRuntimeLimits.MaxBranchDepth);
        var plan = MapEventExecutionPlanner.Plan(
            [command],
            InMemoryCommonEventSource.Empty,
            _ => true,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Single(plan.Effects);
        Assert.Contains("leaf", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RejectsStepBudgetExceeded()
    {
        var commands = Enumerable.Range(0, MapEventRuntimeLimits.MaxExecutionSteps + 1)
            .Select(i => ShowText($"step-{i}"))
            .ToArray();

        var plan = MapEventExecutionPlanner.Plan(
            commands,
            InMemoryCommonEventSource.Empty,
            _ => true,
            Identity());

        Assert.False(plan.IsSuccess);
        Assert.Contains("Limite d'exécution", plan.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RejectsCommonEventRecursionDeeperThanLimit()
    {
        var ids = Enumerable.Range(0, MapEventRuntimeLimits.MaxCommonEventRecursionDepth + 1)
            .Select(i => Guid.Parse($"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee{i:x}"))
            .ToArray();
        var events = new List<CommonEventDefinition>();
        for (var i = 0; i < ids.Length; i++)
        {
            var next = i + 1 < ids.Length ? CallCommon(ids[i + 1]) : ShowText("tail");
            events.Add(new CommonEventDefinition
            {
                Id = ids[i],
                Name = $"CE{i}",
                Pages = [new MapEventPageDefinition { Commands = [next] }],
            });
        }

        var plan = MapEventExecutionPlanner.Plan(
            [CallCommon(ids[0])],
            new InMemoryCommonEventSource(events),
            _ => true,
            Identity());

        Assert.False(plan.IsSuccess);
        Assert.Contains("call_common_event", plan.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_AcceptsCommonEventRecursionAtMaxDepth()
    {
        var ids = Enumerable.Range(0, MapEventRuntimeLimits.MaxCommonEventRecursionDepth)
            .Select(i => Guid.Parse($"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeea{i:x}"))
            .ToArray();
        var events = new List<CommonEventDefinition>();
        for (var i = 0; i < ids.Length; i++)
        {
            var next = i + 1 < ids.Length ? CallCommon(ids[i + 1]) : SetSwitch("depth_ok", true);
            events.Add(new CommonEventDefinition
            {
                Id = ids[i],
                Name = $"CE{i}",
                Pages = [new MapEventPageDefinition { Commands = [next] }],
            });
        }

        var plan = MapEventExecutionPlanner.Plan(
            [CallCommon(ids[0])],
            new InMemoryCommonEventSource(events),
            _ => true,
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Single(plan.Effects);
        Assert.Equal(MapEventCommandDiscriminators.SetSwitch, plan.Effects[0].Discriminator);
        Assert.DoesNotContain(
            plan.Effects,
            c => c.Discriminator == MapEventCommandDiscriminators.CallCommonEvent);
    }

    [Fact]
    public void Plan_RejectsInvalidExecutionIdentity()
    {
        var plan = MapEventExecutionPlanner.Plan(
            [ShowText("hi")],
            InMemoryCommonEventSource.Empty,
            _ => true,
            new MapEventExecutionIdentity(Guid.Empty, Guid.Empty, 1, 1));

        Assert.False(plan.IsSuccess);
        Assert.Contains("Identité", plan.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecutionIdentity_Create_ReusesProvidedRequestId()
    {
        var identity = MapEventExecutionIdentity.Create(CharacterId, 9, 3, FixedRequestId);
        Assert.Equal(FixedRequestId, identity.RequestId);
        Assert.Equal(FixedRequestId, identity.ActivationId);
        Assert.Equal(0, identity.WaitOrdinal);
        Assert.True(identity.IsValid);
        Assert.Equal((CharacterId, FixedRequestId), identity.LedgerKey);
    }

    [Fact]
    public void ExecutionIdentity_Create_GeneratesRequestIdWhenOmitted()
    {
        var identity = MapEventExecutionIdentity.Create(CharacterId, 9, 3);
        Assert.NotEqual(Guid.Empty, identity.RequestId);
        Assert.True(identity.IsValid);
    }

    [Fact]
    public async Task ExpandCommonEventsAsync_LeavesBranchCommandsIntact()
    {
        var branch = Branch(
            MapEventConditionKinds.CharacterSwitch,
            """{"switchId":"gate_open","value":true}""",
            thenCommands: [ShowText("then")],
            elseCommands: [ShowText("else")]);

        var resolved = await MapEventExecutionPlanner.ExpandCommonEventsAsync(
            [branch],
            InMemoryCommonEventSource.Empty);

        Assert.True(resolved.IsSuccess, resolved.Error);
        Assert.Single(resolved.Effects);
        Assert.Equal(MapEventCommandDiscriminators.Branch, resolved.Effects[0].Discriminator);
    }

    [Fact]
    public void ServerPlanner_CanExecuteTransactionally_AllowsQuestAndProfession()
    {
        Assert.True(ServerPlanner.CanExecuteTransactionally(
        [
            ShowText("ok"),
            Cmd(MapEventCommandDiscriminators.StartQuest, """{"questId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}"""),
            Cmd(MapEventCommandDiscriminators.AdvanceQuest, """{"questId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","stageIndex":1}"""),
            Cmd(MapEventCommandDiscriminators.TurnInQuest, """{"questId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}"""),
            Cmd(MapEventCommandDiscriminators.LearnProfession, """{"professionId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"}"""),
        ]));
    }

    [Fact]
    public void ServerPlanner_CanExecuteTransactionally_RejectsTeleportAndDialogue()
    {
        Assert.False(ServerPlanner.CanExecuteTransactionally(
        [
            ShowText("ok"),
            Cmd(MapEventCommandDiscriminators.Teleport, """{"mapId":1,"tileX":0,"tileY":0}"""),
        ]));
        Assert.False(ServerPlanner.CanExecuteTransactionally(
        [
            Cmd(MapEventCommandDiscriminators.StartDialogue, """{"dialogueId":"cccccccc-cccc-cccc-cccc-cccccccccccc"}"""),
        ]));
    }

    [Fact]
    public async Task ServerPlanner_PlanAsync_ResolvesBranchViaCore()
    {
        var commands = new[]
        {
            Branch(
                MapEventConditionKinds.CharacterSwitch,
                """{"switchId":"gate_open","value":true}""",
                thenCommands: [ShowText("then")],
                elseCommands: [ShowText("else")]),
            Cmd(MapEventCommandDiscriminators.StartQuest, """{"questId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}"""),
        };

        var plan = await ServerPlanner.PlanAsync(
            commands,
            new FakePublishedCommonEventCatalog([]),
            _ => Task.FromResult(true),
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Equal(2, plan.Effects.Count);
        Assert.DoesNotContain(plan.Effects, c => c.Discriminator == MapEventCommandDiscriminators.Branch);
        Assert.True(ServerPlanner.AreEffectsTransactional(plan.Effects));
        Assert.False(ServerPlanner.ContainsUnresolvedControlFlow(plan.Effects));
        Assert.Contains("then", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
        Assert.Equal(MapEventCommandDiscriminators.StartQuest, plan.Effects[1].Discriminator);
    }

    [Fact]
    public async Task ServerPlanner_ResolveCommandsAsync_ExpandsCommonEventViaCore()
    {
        var commonId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var catalog = new FakePublishedCommonEventCatalog(
        [
            new CommonEventDefinition
            {
                Id = commonId,
                Name = "ServerCE",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands = [ShowText("via-server")],
                    },
                ],
            },
        ]);

        var resolved = await ServerPlanner.ResolveCommandsAsync(
            catalog,
            [CallCommon(commonId)],
            default);

        Assert.Null(resolved.Error);
        Assert.Single(resolved.Commands);
        Assert.Contains("via-server", resolved.Commands[0].ParameterJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerPlanner_PlanAsync_ExpandsNestedCommonEventViaCore()
    {
        var parentId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffff01");
        var childId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffff02");
        var catalog = new FakePublishedCommonEventCatalog(
        [
            new CommonEventDefinition
            {
                Id = parentId,
                Name = "ServerParent",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands = [CallCommon(childId), ShowText("via-parent")],
                    },
                ],
            },
            new CommonEventDefinition
            {
                Id = childId,
                Name = "ServerChild",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        Commands = [SetSwitch("nested_public", true)],
                    },
                ],
            },
        ]);

        var plan = await ServerPlanner.PlanAsync(
            [CallCommon(parentId)],
            catalog,
            _ => Task.FromResult(true),
            Identity());

        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Equal(2, plan.Effects.Count);
        Assert.Equal(MapEventCommandDiscriminators.SetSwitch, plan.Effects[0].Discriminator);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, plan.Effects[1].Discriminator);
        Assert.True(ServerPlanner.AreEffectsTransactional(plan.Effects));
        Assert.False(ServerPlanner.ContainsUnresolvedControlFlow(plan.Effects));
    }

    [Fact]
    public async Task PublishedCommonEventSource_ForwardsCatalogLookup()
    {
        var commonId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var definition = new CommonEventDefinition { Id = commonId, Name = "Fwd", EditorAliasId = 7 };
        var source = new PublishedCommonEventSource(new FakePublishedCommonEventCatalog([definition]));

        var byId = await source.TryGetByIdAsync(commonId);
        var byAlias = await source.TryGetByAliasAsync(7);
        Assert.Same(definition, byId);
        Assert.Same(definition, byAlias);
    }

    private static MapEventExecutionIdentity Identity() =>
        MapEventExecutionIdentity.Create(CharacterId, placementId: 101, catalogAliasId: 3, FixedRequestId);

    private static MapEventCommandDefinition Cmd(string discriminator, string json) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = json,
        };

    private static MapEventCommandDefinition ShowText(string text) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            ParameterJson = JsonSerializer.Serialize(new { text }),
        };

    private static MapEventCommandDefinition SetSwitch(string switchId, bool value) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.SetSwitch,
            ParameterJson = JsonSerializer.Serialize(new { switchId, value }),
        };

    private static MapEventCommandDefinition CallCommon(Guid commonEventId) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
            ParameterJson = JsonSerializer.Serialize(new { commonEventId }),
        };

    private static MapEventCommandDefinition Branch(
        string conditionKind,
        string conditionParameterJson,
        IReadOnlyList<MapEventCommandDefinition> thenCommands,
        IReadOnlyList<MapEventCommandDefinition> elseCommands) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.Branch,
            ParameterJson = JsonSerializer.Serialize(new
            {
                conditionKind,
                conditionParameterJson,
                thenCommands = thenCommands.Select(AsAnon).ToArray(),
                elseCommands = elseCommands.Select(AsAnon).ToArray(),
            }),
        };

    private static object AsAnon(MapEventCommandDefinition command) =>
        new { discriminator = command.Discriminator, parameterJson = command.ParameterJson };

    private static MapEventCommandDefinition NestBranches(int depth)
    {
        if (depth <= 0)
        {
            return ShowText("leaf");
        }

        var inner = NestBranches(depth - 1);
        return Branch(
            MapEventConditionKinds.CharacterSwitch,
            """{"switchId":"gate_open","value":true}""",
            thenCommands: [inner],
            elseCommands: []);
    }

    private static CommonEventDefinition CommonEventCalling(string name, Guid id, Guid targetId) =>
        new()
        {
            Id = id,
            Name = name,
            Pages =
            [
                new MapEventPageDefinition
                {
                    Commands = [CallCommon(targetId)],
                },
            ],
        };

    private sealed class FakePublishedCommonEventCatalog : IPublishedCommonEventCatalog
    {
        private readonly Dictionary<Guid, CommonEventDefinition> _byId = new();
        private readonly Dictionary<int, CommonEventDefinition> _byAlias = new();

        public FakePublishedCommonEventCatalog(IEnumerable<CommonEventDefinition> events)
        {
            foreach (var ev in events)
            {
                _byId[ev.Id] = ev;
                if (ev.EditorAliasId is int alias and > 0)
                {
                    _byAlias[alias] = ev;
                }
            }
        }

        public Task<IReadOnlyList<CommonEventDefinition>> ListPublishedAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CommonEventDefinition>>(_byId.Values.ToList());

        public Task<CommonEventDefinition?> TryGetPublishedByIdAsync(
            Guid eventId,
            CancellationToken cancellationToken = default)
        {
            _byId.TryGetValue(eventId, out var ev);
            return Task.FromResult<CommonEventDefinition?>(ev);
        }

        public Task<CommonEventDefinition?> TryGetPublishedByAliasAsync(
            int editorAliasId,
            CancellationToken cancellationToken = default)
        {
            _byAlias.TryGetValue(editorAliasId, out var ev);
            return Task.FromResult<CommonEventDefinition?>(ev);
        }
    }
}
