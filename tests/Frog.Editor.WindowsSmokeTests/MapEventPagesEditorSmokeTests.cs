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
            Assert.Contains("branche", error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

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
