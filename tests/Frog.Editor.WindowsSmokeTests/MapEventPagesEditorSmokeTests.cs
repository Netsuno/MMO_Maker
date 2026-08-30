using System.Windows.Forms;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Forms.Phase8;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapEventPagesEditorSmokeTests
{
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
            Assert.Equal(1, panel.ConditionsForTest.Rows.Count);
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
}
