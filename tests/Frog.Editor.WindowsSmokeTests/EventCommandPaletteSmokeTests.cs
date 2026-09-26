using System.IO;
using System.Windows.Forms;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Forms.Phase8;
using Frog.Editor.Services;
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

    [Fact]
    public void Palette_InsertsChoicesAndAudio_RoundTripsEdits()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Width = 980, Height = 860 };
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

            panel.InsertPaletteForTest(MapEventCommandPalette.ShowChoicesId);
            var choices = panel.CommandParamsForTest.ShowChoicesForTest;
            Assert.NotNull(choices);
            Assert.Equal(MapEventShowChoices.CancelDisallow, choices!.CancelForTest.SelectedItem);
            choices.ChoiceTextForTest(1).Text = "Jamais";
            choices.BranchForTest(0).InsertPaletteForTest(MapEventCommandPalette.ShowTextId);
            choices.CancelForTest.SelectedItem = MapEventShowChoices.CancelBranch;
            choices.CancelCommandsForTest.InsertPaletteForTest(MapEventCommandPalette.SetSwitchId);

            panel.InsertPaletteForTest(MapEventCommandPalette.PlayBgmId);
            Assert.Equal("Parcourir…", panel.CommandParamsForTest.AudioBrowseForTest?.Text);
            var picked = Path.Combine(Path.GetTempPath(), "Assets", "Audio", $"frog-bgm-{Guid.NewGuid():N}.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(picked)!);
            File.WriteAllBytes(picked, [0x52, 0x49, 0x46, 0x46]);
            EditorTestHooks.OverrideMapAudioPickPath = picked;
            try
            {
                panel.CommandParamsForTest.ClickAudioBrowseForTest();
            }
            finally
            {
                EditorTestHooks.OverrideMapAudioPickPath = null;
            }

            var volume = Assert.IsType<NumericUpDown>(panel.CommandParamsForTest.FieldForTest("volume"));
            volume.Value = 65;
            var fade = Assert.IsType<NumericUpDown>(panel.CommandParamsForTest.FieldForTest("fadeMs"));
            fade.Value = 300;

            panel.InsertPaletteForTest(MapEventCommandPalette.PlaySeId);

            Assert.True(panel.TryBuildPages(out var pages, out var error), error);
            Assert.Equal(3, pages[0].Commands.Count);
            Assert.Equal(MapEventCommandDiscriminators.ShowChoices, pages[0].Commands[0].Discriminator);
            Assert.True(
                MapEventParameterSchemas.TryParseShowChoices(
                    pages[0].Commands[0].ParameterJson,
                    out var labels,
                    out var cancel,
                    out var branches,
                    out var cancelCommands,
                    out var parseErr),
                parseErr);
            Assert.Equal(["Oui", "Jamais"], labels);
            Assert.Equal(MapEventShowChoices.CancelBranch, cancel);
            Assert.Equal(MapEventCommandDiscriminators.ShowText, Assert.Single(branches[0]).Discriminator);
            Assert.Empty(branches[1]);
            Assert.Equal(MapEventCommandDiscriminators.SetSwitch, Assert.Single(cancelCommands).Discriminator);

            Assert.Equal(MapEventCommandDiscriminators.PlayBgm, pages[0].Commands[1].Discriminator);
            Assert.True(
                MapEventParameterSchemas.TryParsePlayAudio(
                    pages[0].Commands[1].ParameterJson,
                    MapEventCommandDiscriminators.PlayBgm,
                    out var bgm,
                    out var bgmErr),
                bgmErr);
            Assert.EndsWith(".wav", bgm.Asset, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(":", bgm.Asset, StringComparison.Ordinal);
            Assert.Equal(65, bgm.Volume);
            Assert.Equal(300, bgm.FadeMs);

            Assert.Equal(MapEventCommandDiscriminators.PlaySe, pages[0].Commands[2].Discriminator);
            Assert.True(pages[0].Validate(out var pageErr), pageErr);

            panel.LoadPages(pages);
            Assert.True(panel.TryBuildPages(out var rebuilt, out var rebuildErr), rebuildErr);
            Assert.Equal(pages[0].Commands[0].ParameterJson, rebuilt[0].Commands[0].ParameterJson);
            Assert.Equal(pages[0].Commands[1].ParameterJson, rebuilt[0].Commands[1].ParameterJson);
            Assert.Equal(pages[0].Commands[2].ParameterJson, rebuilt[0].Commands[2].ParameterJson);

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
