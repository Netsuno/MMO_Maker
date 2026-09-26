using System.Windows.Forms;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Forms.Phase8;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EventCommandPaletteSmokeTests
{
    [Fact]
    public void Palette_InsertsSwitchVariableBranchAndNestedText()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 980, Height = 720 };
            var panel = new MapEventPagesEditorPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();

            panel.LoadPages(
            [
                new MapEventPageDefinition
                {
                    TriggerKind = Phase8MapEventTriggerKinds.Action,
                },
            ]);

            var labels = Buttons(panel).Select(button => button.Text).ToList();
            foreach (var entry in MapEventCommandPalette.Entries)
            {
                Assert.Contains(entry.Label, labels);
            }

            Buttons(panel).Single(button => button.Text == "Interrupteur").PerformClick();
            Assert.Equal(1, panel.CommandsForTest.Items.Count);
            Assert.Contains("Interrupteur", panel.CommandsForTest.Items[0]?.ToString(), StringComparison.Ordinal);

            panel.InsertPaletteForTest(MapEventCommandPalette.BranchVariableId);
            Assert.NotNull(panel.CommandParamsForTest.BranchThenForTest);
            panel.CommandParamsForTest.BranchThenForTest!.InsertPaletteForTest(MapEventCommandPalette.ShowTextId);

            Assert.True(panel.TryBuildPages(out var pages, out var error), error);
            Assert.Equal(2, pages[0].Commands.Count);
            Assert.Equal(MapEventCommandDiscriminators.SetSwitch, pages[0].Commands[0].Discriminator);
            Assert.Equal(MapEventCommandDiscriminators.Branch, pages[0].Commands[1].Discriminator);
            Assert.True(
                MapEventParameterSchemas.TryParseBranch(
                    pages[0].Commands[1].ParameterJson,
                    out var condition,
                    out var thenCommands,
                    out var elseCommands,
                    out var parseErr),
                parseErr);
            Assert.Equal(MapEventConditionKinds.CharacterVariableCompare, condition.Kind);
            Assert.Equal(MapEventCommandDiscriminators.ShowText, Assert.Single(thenCommands).Discriminator);
            Assert.Empty(elseCommands);
            Assert.True(pages[0].Validate(out var pageErr), pageErr);

            host.Close();
        });
    }

    private static IEnumerable<Button> Buttons(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button)
            {
                yield return button;
            }

            foreach (var nested in Buttons(child))
            {
                yield return nested;
            }
        }
    }
}
