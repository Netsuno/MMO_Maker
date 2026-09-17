using System.Text.Json;
using System.Windows.Forms;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Forms.Phase8;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapEventPagesEditorSmokeTests
{
    private static readonly Guid SampleGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void MapEventPagesEditor_StructuredEdit_ValidateSaveAndReopen()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 800, Height = 600 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            var page = new MapEventPageDefinition
            {
                PageOrder = 0,
                Priority = 3,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Route,
                RouteWaypoints =
                [
                    new MapEventRouteWaypoint { TileX = 1, TileY = 0, WaitMs = 250 },
                    new MapEventRouteWaypoint { TileX = 1, TileY = 1, WaitMs = 500 },
                ],
                AppearanceGraphicId = 2,
                AppearanceDirection = 4,
                BlocksCollision = true,
                Conditions =
                [
                    new MapEventConditionDefinition
                    {
                        Kind = MapEventConditionKinds.CharacterSwitch,
                        ParameterJson = """{"switchId":"gate_open","value":true}""",
                    },
                ],
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Structured smoke"}""",
                    },
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.SetSwitch,
                        ParameterJson = """{"switchId":"gate_open","value":true}""",
                    },
                ],
            };

            panel.LoadPages([page]);
            StaTestRunner.PumpUntil(() => panel.PagesForTest.Items.Count == 1, TimeSpan.FromSeconds(5));

            Assert.Equal(3, (int)panel.PriorityForTest.Value);
            Assert.Equal(MapEventMovementKinds.Route, panel.MovementForTest.SelectedItem);
            Assert.Equal(2, panel.WaypointsForTest.Rows.Count);
            Assert.Equal(1, panel.ConditionsForTest.Items.Count);
            Assert.Equal(2, panel.CommandsForTest.Items.Count);

            panel.PriorityForTest.Value = 5;
            Assert.True(panel.TryBuildPages(out var built, out var error), error);
            Assert.Single(built);
            Assert.Equal(5, built[0].Priority);
            Assert.Equal(2, built[0].RouteWaypoints.Count);
            Assert.True(built[0].Validate(out _));

            panel.LoadPages(built);
            Assert.True(panel.TryBuildPages(out var rebuilt, out var rebuildErr), rebuildErr);
            Assert.Equal(5, rebuilt[0].Priority);

            host.Close();
        });
    }

    [Theory]
    [InlineData(MapEventConditionKinds.CharacterSwitch, """{"switchId":"gate_open","value":true}""")]
    [InlineData(MapEventConditionKinds.CharacterVariableCompare, """{"variableId":"score","op":"gte","value":10}""")]
    [InlineData(MapEventConditionKinds.QuestStatus, """{"questId":"11111111-2222-3333-4444-555555555555","status":"active"}""")]
    [InlineData(MapEventConditionKinds.ItemQuantity, """{"itemId":"11111111-2222-3333-4444-555555555555","quantity":2}""")]
    [InlineData(MapEventConditionKinds.CharacterLevel, """{"minLevel":5}""")]
    [InlineData(MapEventConditionKinds.ProfessionLevel, """{"professionId":"11111111-2222-3333-4444-555555555555","minLevel":3}""")]
    [InlineData(MapEventConditionKinds.MapOrRegion, """{"mapId":4}""")]
    public void MapEventPagesEditor_AllConditionKinds_TypedRoundtrip(string kind, string parameterJson)
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 800, Height = 600 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            panel.LoadPages(
            [
                PageWith(
                    conditions:
                    [
                        new MapEventConditionDefinition { Kind = kind, ParameterJson = parameterJson },
                    ],
                    commands:
                    [
                        ShowText("typed condition"),
                    ]),
            ]);

            Assert.False(panel.ConditionParamsForTest.ShowAdvancedForTest.Checked);
            Assert.False(panel.ConditionParamsForTest.AdvancedJsonForTest.Visible);
            Assert.Equal(kind, panel.ConditionParamsForTest.KindForTest.SelectedItem);

            Assert.True(panel.TryBuildPages(out var built, out var error), error);
            Assert.Equal(kind, built[0].Conditions[0].Kind);
            Assert.True(built[0].Conditions[0].Validate(out var condErr), condErr);

            panel.LoadPages(built);
            Assert.False(panel.ConditionParamsForTest.ShowAdvancedForTest.Checked);
            Assert.True(panel.TryBuildPages(out var rebuilt, out var rebuildErr), rebuildErr);
            Assert.Equal(kind, rebuilt[0].Conditions[0].Kind);
            Assert.True(rebuilt[0].Conditions[0].Validate(out var rebuiltErr), rebuiltErr);

            host.Close();
        });
    }

    [Fact]
    public void MapEventPagesEditor_TypedCommandEdit_PersistsWithoutRawJson()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 800, Height = 600 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            panel.LoadPages([PageWith(commands: [ShowText("before")])]);
            Assert.False(panel.CommandParamsForTest.ShowAdvancedForTest.Checked);
            Assert.False(panel.CommandParamsForTest.AdvancedJsonForTest.Visible);

            var textBox = Assert.IsType<TextBox>(panel.CommandParamsForTest.FieldForTest("text"));
            textBox.Text = "Edited structured text";
            textBox.Text = "Edited structured text fully";

            Assert.True(panel.TryBuildPages(out var built, out var error), error);
            Assert.Contains("Edited structured text fully", built[0].Commands[0].ParameterJson, StringComparison.Ordinal);
            Assert.DoesNotContain("before", built[0].Commands[0].ParameterJson, StringComparison.Ordinal);

            host.Close();
        });
    }

    [Theory]
    [InlineData(MapEventCommandDiscriminators.ShowText)]
    [InlineData(MapEventCommandDiscriminators.SetSwitch)]
    [InlineData(MapEventCommandDiscriminators.SetVariable)]
    [InlineData(MapEventCommandDiscriminators.AddVariable)]
    [InlineData(MapEventCommandDiscriminators.SubVariable)]
    [InlineData(MapEventCommandDiscriminators.GiveItem)]
    [InlineData(MapEventCommandDiscriminators.TakeItem)]
    [InlineData(MapEventCommandDiscriminators.GiveGold)]
    [InlineData(MapEventCommandDiscriminators.TakeGold)]
    [InlineData(MapEventCommandDiscriminators.StartDialogue)]
    [InlineData(MapEventCommandDiscriminators.StartQuest)]
    [InlineData(MapEventCommandDiscriminators.TurnInQuest)]
    [InlineData(MapEventCommandDiscriminators.AdvanceQuest)]
    [InlineData(MapEventCommandDiscriminators.Teleport)]
    [InlineData(MapEventCommandDiscriminators.Wait)]
    [InlineData(MapEventCommandDiscriminators.CallCommonEvent)]
    [InlineData(MapEventCommandDiscriminators.LearnProfession)]
    [InlineData(MapEventCommandDiscriminators.Branch)]
    public void MapEventPagesEditor_AllCommandFamilies_TypedFieldMutation(string discriminator)
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 900, Height = 700 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            panel.LoadPages([PageWith(commands: [SeedCommand(discriminator)])]);
            Assert.False(panel.CommandParamsForTest.ShowAdvancedForTest.Checked);
            Assert.Equal(discriminator, panel.CommandParamsForTest.DiscriminatorForTest.SelectedItem);

            MutateCommandFamily(panel, discriminator, out var expectedFragment);
            Assert.True(panel.TryBuildPages(out var built, out var error), error);
            Assert.Equal(discriminator, built[0].Commands[0].Discriminator);
            Assert.Contains(expectedFragment, built[0].Commands[0].ParameterJson, StringComparison.Ordinal);
            if (discriminator == MapEventCommandDiscriminators.Wait)
            {
                Assert.Contains("\"milliseconds\"", built[0].Commands[0].ParameterJson, StringComparison.Ordinal);
                Assert.DoesNotContain("waitMs", built[0].Commands[0].ParameterJson, StringComparison.Ordinal);
            }

            panel.LoadPages(built);
            Assert.True(panel.TryBuildPages(out var rebuilt, out var rebuildErr), rebuildErr);
            Assert.Contains(expectedFragment, rebuilt[0].Commands[0].ParameterJson, StringComparison.Ordinal);
            Assert.True(MapEventCommandParameterValidator.ValidateParameters(rebuilt[0].Commands[0], out var validErr), validErr);

            host.Close();
        });
    }

    [Theory]
    [InlineData(MapEventConditionKinds.CharacterSwitch, "switchId", "mutated_switch")]
    [InlineData(MapEventConditionKinds.CharacterVariableCompare, "value", "77")]
    [InlineData(MapEventConditionKinds.QuestStatus, "status", "completed")]
    [InlineData(MapEventConditionKinds.ItemQuantity, "quantity", "9")]
    [InlineData(MapEventConditionKinds.CharacterLevel, "minLevel", "12")]
    [InlineData(MapEventConditionKinds.ProfessionLevel, "minLevel", "8")]
    [InlineData(MapEventConditionKinds.MapOrRegion, "mapId", "15")]
    public void MapEventPagesEditor_AllConditionKinds_TypedFieldMutation(string kind, string field, string newValue)
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 800, Height = 600 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            panel.LoadPages(
            [
                PageWith(
                    conditions: [SeedCondition(kind)],
                    commands: [ShowText("typed condition mutation")]),
            ]);

            Assert.Equal(kind, panel.ConditionParamsForTest.KindForTest.SelectedItem);
            ApplyTypedField(panel.ConditionParamsForTest.FieldForTest(field), newValue);
            Assert.True(panel.TryBuildPages(out var built, out var error), error);
            Assert.Contains(newValue, built[0].Conditions[0].ParameterJson, StringComparison.Ordinal);

            panel.LoadPages(built);
            Assert.True(panel.TryBuildPages(out var rebuilt, out var rebuildErr), rebuildErr);
            Assert.Contains(newValue, rebuilt[0].Conditions[0].ParameterJson, StringComparison.Ordinal);
            Assert.True(rebuilt[0].Conditions[0].Validate(out var condErr), condErr);

            host.Close();
        });
    }

    [Fact]
    public void MapEventPagesEditor_WaitMilliseconds_BoundsAndRejectsOutOfRange()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 800, Height = 600 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            panel.LoadPages([PageWith(commands: [SeedCommand(MapEventCommandDiscriminators.Wait)])]);
            var ms = Assert.IsType<NumericUpDown>(panel.CommandParamsForTest.FieldForTest("milliseconds"));
            Assert.Equal(0, ms.Minimum);
            Assert.Equal(MapEventRuntimeLimits.MaxWaitMs, ms.Maximum);

            ms.Value = 0;
            Assert.True(panel.TryBuildPages(out var zero, out var zeroErr), zeroErr);
            Assert.Contains("\"milliseconds\":0", zero[0].Commands[0].ParameterJson, StringComparison.Ordinal);

            ms.Value = MapEventRuntimeLimits.MaxWaitMs;
            Assert.True(panel.TryBuildPages(out var max, out var maxErr), maxErr);
            Assert.Contains($"\"milliseconds\":{MapEventRuntimeLimits.MaxWaitMs}", max[0].Commands[0].ParameterJson, StringComparison.Ordinal);

            panel.CommandParamsForTest.ShowAdvancedForTest.Checked = true;
            panel.CommandParamsForTest.AdvancedJsonForTest.Text = """{"milliseconds":60001}""";
            Assert.False(panel.TryBuildPages(out _, out var overErr));
            Assert.False(string.IsNullOrWhiteSpace(overErr));
            Assert.Contains("milliseconds", overErr, StringComparison.OrdinalIgnoreCase);

            panel.CommandParamsForTest.AdvancedJsonForTest.Text = """{"waitMs":500}""";
            Assert.False(panel.TryBuildPages(out _, out var legacyErr));
            Assert.False(string.IsNullOrWhiteSpace(legacyErr));

            host.Close();
        });
    }

    [Fact]
    public void MapEventPagesEditor_InvalidWaypoints_AreRejectedWithoutCoerce()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 800, Height = 600 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            var page = new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Route,
                RouteWaypoints =
                [
                    new MapEventRouteWaypoint { TileX = 2, TileY = 3, WaitMs = 250 },
                    new MapEventRouteWaypoint { TileX = 4, TileY = 5, WaitMs = 500 },
                ],
                Commands = [ShowText("wp")],
            };
            panel.LoadPages([page]);

            panel.WaypointsForTest.Rows[0].Cells[0].Value = "nope";
            Assert.False(panel.TryBuildPages(out _, out var nonIntErr));
            Assert.Contains("TileX", nonIntErr, StringComparison.OrdinalIgnoreCase);

            panel.WaypointsForTest.Rows[0].Cells[0].Value = -1;
            Assert.False(panel.TryBuildPages(out _, out var negErr));
            Assert.Contains("TileX", negErr, StringComparison.OrdinalIgnoreCase);

            panel.WaypointsForTest.Rows[0].Cells[0].Value = 2;
            panel.WaypointsForTest.Rows[0].Cells[2].Value = 60001;
            Assert.False(panel.TryBuildPages(out _, out var waitErr));
            Assert.Contains("WaitMs", waitErr, StringComparison.OrdinalIgnoreCase);

            panel.WaypointsForTest.Rows[0].Cells[2].Value = string.Empty;
            Assert.False(panel.TryBuildPages(out _, out var emptyErr));
            Assert.Contains("WaitMs", emptyErr, StringComparison.OrdinalIgnoreCase);

            panel.WaypointsForTest.Rows[0].Cells[0].Value = 2;
            panel.WaypointsForTest.Rows[0].Cells[1].Value = 3;
            panel.WaypointsForTest.Rows[0].Cells[2].Value = 250;
            Assert.True(panel.TryBuildPages(out var built, out var okErr), okErr);
            Assert.Equal(2, built[0].RouteWaypoints[0].TileX);
            Assert.Equal(3, built[0].RouteWaypoints[0].TileY);
            Assert.Equal(250, built[0].RouteWaypoints[0].WaitMs);

            host.Close();
        });
    }

    [Fact]
    public void MapEventPagesEditor_BranchThenElseNested_ValidateEditAndReopen()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 900, Height = 700 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            var nested = BranchCommand(
                new MapEventConditionDefinition
                {
                    Kind = MapEventConditionKinds.CharacterLevel,
                    ParameterJson = """{"minLevel":2}""",
                },
                [ShowText("nested-then")],
                [ShowText("nested-else")]);
            var root = BranchCommand(
                new MapEventConditionDefinition
                {
                    Kind = MapEventConditionKinds.CharacterSwitch,
                    ParameterJson = """{"switchId":"gate_open","value":true}""",
                },
                [nested, ShowText("outer-then")],
                [ShowText("outer-else")]);

            panel.LoadPages([PageWith(commands: [root])]);
            Assert.Equal(MapEventCommandDiscriminators.Branch, panel.CommandParamsForTest.DiscriminatorForTest.SelectedItem);
            Assert.False(panel.CommandParamsForTest.ShowAdvancedForTest.Checked);
            Assert.NotNull(panel.CommandParamsForTest.BranchThenForTest);
            Assert.NotNull(panel.CommandParamsForTest.BranchElseForTest);
            Assert.Equal(2, panel.CommandParamsForTest.BranchThenForTest!.CommandsForTest.Items.Count);
            Assert.Equal(1, panel.CommandParamsForTest.BranchElseForTest!.CommandsForTest.Items.Count);

            var nestedThen = panel.CommandParamsForTest.BranchThenForTest.ParamsForTest.BranchThenForTest;
            Assert.NotNull(nestedThen);
            var thenText = Assert.IsType<TextBox>(nestedThen.ParamsForTest.FieldForTest("text"));
            thenText.Text = "nested-then-edited";

            Assert.True(panel.TryBuildPages(out var built, out var error), error);
            Assert.Equal(MapEventCommandDiscriminators.Branch, built[0].Commands[0].Discriminator);
            Assert.True(
                MapEventParameterSchemas.TryParseBranch(
                    built[0].Commands[0].ParameterJson,
                    out _,
                    out var thenCommands,
                    out var elseCommands,
                    out var parseErr),
                parseErr);
            Assert.Equal(2, thenCommands.Count);
            Assert.Equal(MapEventCommandDiscriminators.Branch, thenCommands[0].Discriminator);
            Assert.Contains("nested-then-edited", thenCommands[0].ParameterJson, StringComparison.Ordinal);
            Assert.Contains("outer-else", elseCommands[0].ParameterJson, StringComparison.Ordinal);

            panel.LoadPages(built);
            Assert.True(panel.TryBuildPages(out var rebuilt, out var rebuildErr), rebuildErr);
            Assert.True(
                MapEventParameterSchemas.TryParseBranch(
                    rebuilt[0].Commands[0].ParameterJson,
                    out _,
                    out var thenAgain,
                    out _,
                    out var parseAgainErr),
                parseAgainErr);
            Assert.Contains("nested-then-edited", thenAgain[0].ParameterJson, StringComparison.Ordinal);

            host.Close();
        });
    }

    [Fact]
    public void MapEventPagesEditor_BranchBeyondMaxDepth_FailsValidation()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 800, Height = 600 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            panel.LoadPages([PageWith(commands: [NestBranches(MapEventRuntimeLimits.MaxBranchDepth + 1)])]);
            Assert.False(panel.TryBuildPages(out _, out var error));
            Assert.False(string.IsNullOrWhiteSpace(error));

            host.Close();
        });
    }

    [Fact]
    public void Phase8CommonEventEditor_StructuredSaveCloseReopen_NoRawJson()
    {
        StaTestRunner.Run(() =>
        {
            var id = Guid.NewGuid();
            var original = new CommonEventDefinition
            {
                Id = id,
                Name = "Structured CE",
                Pages =
                [
                    PageWith(
                        conditions:
                        [
                            new MapEventConditionDefinition
                            {
                                Kind = MapEventConditionKinds.CharacterSwitch,
                                ParameterJson = """{"switchId":"gate_open","value":true}""",
                            },
                        ],
                        commands:
                        [
                            BranchCommand(
                                new MapEventConditionDefinition
                                {
                                    Kind = MapEventConditionKinds.ItemQuantity,
                                    ParameterJson = $$"""{"itemId":"{{SampleGuid:D}}","quantity":1}""",
                                },
                                [ShowText("ce-then")],
                                [ShowText("ce-else")]),
                        ]),
                ],
            };

            using var first = new Phase8CommonEventEditorPanel { Dock = DockStyle.Fill };
            first.CatalogName = "Structured CE";
            first.LoadPayload(Phase8ContentPostgreSqlService.Serialize(original));
            Assert.False(first.PagesPanelForTest.CommandParamsForTest.ShowAdvancedForTest.Checked);
            Assert.False(first.PagesPanelForTest.CommandParamsForTest.AdvancedJsonForTest.Visible);
            Assert.Equal(MapEventCommandDiscriminators.Branch, first.PagesPanelForTest.CommandParamsForTest.DiscriminatorForTest.SelectedItem);

            Assert.True(first.TryBuildPayload(out var savedJson, out var saveErr), saveErr);

            using var reopened = new Phase8CommonEventEditorPanel { Dock = DockStyle.Fill };
            reopened.CatalogName = "Structured CE";
            reopened.LoadPayload(savedJson);
            Assert.False(reopened.PagesPanelForTest.CommandParamsForTest.ShowAdvancedForTest.Checked);
            Assert.True(reopened.TryBuildPayload(out var reopenedJson, out var reopenErr), reopenErr);
            Assert.True(
                Phase8ContentPostgreSqlService.TryDeserialize(reopenedJson, out CommonEventDefinition restored, out var deserErr),
                deserErr);
            Assert.Equal("Structured CE", restored.Name);
            Assert.Single(restored.Pages);
            Assert.Equal(MapEventCommandDiscriminators.Branch, restored.Pages[0].Commands[0].Discriminator);
            Assert.Equal(MapEventConditionKinds.CharacterSwitch, restored.Pages[0].Conditions[0].Kind);
            Assert.True(restored.Validate(out var validErr), validErr);
        });
    }

    private static MapEventPageDefinition PageWith(
        IReadOnlyList<MapEventConditionDefinition>? conditions = null,
        IReadOnlyList<MapEventCommandDefinition>? commands = null) =>
        new()
        {
            PageOrder = 0,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            Conditions = conditions ?? Array.Empty<MapEventConditionDefinition>(),
            Commands = commands ?? Array.Empty<MapEventCommandDefinition>(),
        };

    private static MapEventCommandDefinition SeedCommand(string discriminator) =>
        discriminator switch
        {
            MapEventCommandDiscriminators.ShowText => ShowText("before"),
            MapEventCommandDiscriminators.SetSwitch => Command(discriminator, """{"switchId":"gate_open","value":true}"""),
            MapEventCommandDiscriminators.SetVariable => Command(discriminator, """{"variableId":"var1","value":0}"""),
            MapEventCommandDiscriminators.AddVariable => Command(discriminator, """{"variableId":"var1","delta":1}"""),
            MapEventCommandDiscriminators.SubVariable => Command(discriminator, """{"variableId":"var1","delta":1}"""),
            MapEventCommandDiscriminators.GiveItem => Command(discriminator, $$"""{"itemId":"{{SampleGuid:D}}","quantity":1}"""),
            MapEventCommandDiscriminators.TakeItem => Command(discriminator, $$"""{"itemId":"{{SampleGuid:D}}","quantity":1}"""),
            MapEventCommandDiscriminators.GiveGold => Command(discriminator, """{"amount":10}"""),
            MapEventCommandDiscriminators.TakeGold => Command(discriminator, """{"amount":10}"""),
            MapEventCommandDiscriminators.StartDialogue => Command(discriminator, $$"""{"dialogueId":"{{SampleGuid:D}}"}"""),
            MapEventCommandDiscriminators.StartQuest or MapEventCommandDiscriminators.TurnInQuest =>
                Command(discriminator, $$"""{"questId":"{{SampleGuid:D}}"}"""),
            MapEventCommandDiscriminators.AdvanceQuest => Command(discriminator, $$"""{"questId":"{{SampleGuid:D}}","stageIndex":0}"""),
            MapEventCommandDiscriminators.Teleport => Command(discriminator, """{"mapId":1,"tileX":0,"tileY":0}"""),
            MapEventCommandDiscriminators.Wait => Command(discriminator, """{"milliseconds":500}"""),
            MapEventCommandDiscriminators.CallCommonEvent => Command(discriminator, $$"""{"commonEventId":"{{SampleGuid:D}}"}"""),
            MapEventCommandDiscriminators.LearnProfession => Command(discriminator, $$"""{"professionId":"{{SampleGuid:D}}"}"""),
            MapEventCommandDiscriminators.Branch => BranchCommand(
                new MapEventConditionDefinition
                {
                    Kind = MapEventConditionKinds.CharacterSwitch,
                    ParameterJson = """{"switchId":"gate_open","value":true}""",
                },
                [ShowText("branch-then")],
                [ShowText("branch-else")]),
            _ => throw new ArgumentOutOfRangeException(nameof(discriminator), discriminator, "Unknown command family."),
        };

    private static MapEventConditionDefinition SeedCondition(string kind) => kind switch
    {
        MapEventConditionKinds.CharacterSwitch => new()
        {
            Kind = kind,
            ParameterJson = """{"switchId":"gate_open","value":true}""",
        },
        MapEventConditionKinds.CharacterVariableCompare => new()
        {
            Kind = kind,
            ParameterJson = """{"variableId":"score","op":"gte","value":10}""",
        },
        MapEventConditionKinds.QuestStatus => new()
        {
            Kind = kind,
            ParameterJson = $$"""{"questId":"{{SampleGuid:D}}","status":"active"}""",
        },
        MapEventConditionKinds.ItemQuantity => new()
        {
            Kind = kind,
            ParameterJson = $$"""{"itemId":"{{SampleGuid:D}}","quantity":2}""",
        },
        MapEventConditionKinds.CharacterLevel => new()
        {
            Kind = kind,
            ParameterJson = """{"minLevel":5}""",
        },
        MapEventConditionKinds.ProfessionLevel => new()
        {
            Kind = kind,
            ParameterJson = $$"""{"professionId":"{{SampleGuid:D}}","minLevel":3}""",
        },
        MapEventConditionKinds.MapOrRegion => new()
        {
            Kind = kind,
            ParameterJson = """{"mapId":4}""",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown condition family."),
    };

    private static MapEventCommandDefinition Command(string discriminator, string parameterJson) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = parameterJson,
        };

    private static void MutateCommandFamily(MapEventPagesEditorPanel panel, string discriminator, out string expectedFragment)
    {
        switch (discriminator)
        {
            case MapEventCommandDiscriminators.ShowText:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("text"), "mutated-text");
                expectedFragment = "mutated-text";
                break;
            case MapEventCommandDiscriminators.SetSwitch:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("switchId"), "gate_closed");
                expectedFragment = "gate_closed";
                break;
            case MapEventCommandDiscriminators.SetVariable:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("value"), "42");
                expectedFragment = "\"value\":42";
                break;
            case MapEventCommandDiscriminators.AddVariable:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("delta"), "7");
                expectedFragment = "\"delta\":7";
                break;
            case MapEventCommandDiscriminators.SubVariable:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("delta"), "3");
                expectedFragment = "\"delta\":3";
                break;
            case MapEventCommandDiscriminators.GiveItem:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("quantity"), "4");
                expectedFragment = "\"quantity\":4";
                break;
            case MapEventCommandDiscriminators.TakeItem:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("quantity"), "2");
                expectedFragment = "\"quantity\":2";
                break;
            case MapEventCommandDiscriminators.GiveGold:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("amount"), "99");
                expectedFragment = "\"amount\":99";
                break;
            case MapEventCommandDiscriminators.TakeGold:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("amount"), "5");
                expectedFragment = "\"amount\":5";
                break;
            case MapEventCommandDiscriminators.StartDialogue:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("dialogueId"), OtherGuid.ToString("D"));
                expectedFragment = OtherGuid.ToString("D");
                break;
            case MapEventCommandDiscriminators.StartQuest:
            case MapEventCommandDiscriminators.TurnInQuest:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("questId"), OtherGuid.ToString("D"));
                expectedFragment = OtherGuid.ToString("D");
                break;
            case MapEventCommandDiscriminators.AdvanceQuest:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("stageIndex"), "2");
                expectedFragment = "\"stageIndex\":2";
                break;
            case MapEventCommandDiscriminators.Teleport:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("tileX"), "8");
                expectedFragment = "\"tileX\":8";
                break;
            case MapEventCommandDiscriminators.Wait:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("milliseconds"), "1500");
                expectedFragment = "\"milliseconds\":1500";
                break;
            case MapEventCommandDiscriminators.CallCommonEvent:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("commonEventId"), OtherGuid.ToString("D"));
                expectedFragment = OtherGuid.ToString("D");
                break;
            case MapEventCommandDiscriminators.LearnProfession:
                ApplyTypedField(panel.CommandParamsForTest.FieldForTest("professionId"), OtherGuid.ToString("D"));
                expectedFragment = OtherGuid.ToString("D");
                break;
            case MapEventCommandDiscriminators.Branch:
                var thenText = Assert.IsType<TextBox>(
                    panel.CommandParamsForTest.BranchThenForTest!.ParamsForTest.FieldForTest("text"));
                thenText.Text = "branch-mutated";
                expectedFragment = "branch-mutated";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(discriminator), discriminator, "Unknown command family.");
        }
    }

    private static void ApplyTypedField(Control? field, string newValue)
    {
        Assert.NotNull(field);
        switch (field)
        {
            case TextBox text:
                text.Text = newValue;
                break;
            case NumericUpDown numeric:
                numeric.Value = decimal.Parse(newValue, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case ComboBox combo:
                var idx = combo.Items.IndexOf(newValue);
                Assert.True(idx >= 0, $"Combo value '{newValue}' not found.");
                combo.SelectedIndex = idx;
                break;
            case CheckBox check:
                check.Checked = bool.Parse(newValue);
                break;
            default:
                throw new InvalidOperationException($"Unsupported field type {field.GetType().Name}.");
        }
    }

    private static MapEventCommandDefinition ShowText(string text) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            SchemaVersion = 1,
            ParameterJson = JsonSerializer.Serialize(new { text }),
        };

    private static MapEventCommandDefinition BranchCommand(
        MapEventConditionDefinition condition,
        IReadOnlyList<MapEventCommandDefinition> thenCommands,
        IReadOnlyList<MapEventCommandDefinition> elseCommands) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.Branch,
            SchemaVersion = 1,
            ParameterJson = JsonSerializer.Serialize(new
            {
                conditionKind = condition.Kind,
                conditionParameterJson = condition.ParameterJson,
                thenCommands = thenCommands.Select(c => new
                {
                    discriminator = c.Discriminator,
                    parameterJson = c.ParameterJson,
                }),
                elseCommands = elseCommands.Select(c => new
                {
                    discriminator = c.Discriminator,
                    parameterJson = c.ParameterJson,
                }),
            }),
        };

    private static MapEventCommandDefinition NestBranches(int depth)
    {
        if (depth <= 0)
        {
            return ShowText("leaf");
        }

        return BranchCommand(
            new MapEventConditionDefinition
            {
                Kind = MapEventConditionKinds.CharacterSwitch,
                ParameterJson = """{"switchId":"gate_open","value":true}""",
            },
            [NestBranches(depth - 1)],
            Array.Empty<MapEventCommandDefinition>());
    }
}
