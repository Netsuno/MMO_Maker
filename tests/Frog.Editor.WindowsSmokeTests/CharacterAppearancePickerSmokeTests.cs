using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Frog.Client.Controls;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Création : Corps / Cheveux / Tunique, aperçu vivant, look repris au choix du perso (Netsun).</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class CharacterAppearancePickerSmokeTests
{
    private static readonly Color Tunic = Color.FromArgb(255, 186, 122, 64);

    [Fact]
    public void Picker_ClickAndArrows_UpdatePreview_AndCreateLookSurvivesSelect()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-appearance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                form.ShowCharacterSelectForTest();
                var picker = form.AppearancePickerForTest;
                WaitUntilPickerReady(picker);
                Assert.Equal("Ocre", picker.ValueForTest(CharacterLookSlot.Tunic));
                Assert.Equal("Chevalier", picker.ValueForTest(CharacterLookSlot.Body));
                Assert.Equal("Naturel", picker.ValueForTest(CharacterLookSlot.Hair));

                using (var ochre = picker.RenderPreviewForTest())
                {
                    Assert.True(Contains(ochre, Tunic), "ocre tunic layer is in the live preview");
                    picker.ClickPreviousForTest(CharacterLookSlot.Tunic);
                    Assert.Equal("Aucune", picker.ValueForTest(CharacterLookSlot.Tunic));
                    using var bare = picker.RenderPreviewForTest();
                    Assert.False(Contains(bare, Tunic), "Aucune hides the tunic overlay");
                }

                picker.ClickNextForTest(CharacterLookSlot.Tunic);
                picker.ClickNextForTest(CharacterLookSlot.Tunic);
                Assert.Equal("Lin", picker.ValueForTest(CharacterLookSlot.Tunic));
                using (var linen = picker.RenderPreviewForTest())
                {
                    Assert.False(Contains(linen, Tunic), "lin recolors the same tunic sheet");
                }

                picker.ClickNextForTest(CharacterLookSlot.Hair);
                Assert.Equal("Brun", picker.ValueForTest(CharacterLookSlot.Hair));
                picker.ClickNextForTest(CharacterLookSlot.Body);
                Assert.Equal("Forêt", picker.ValueForTest(CharacterLookSlot.Body));

                var outside = form.CharRefreshButtonForTest;
                outside.Enabled = true;
                WaitUntilInteractive(outside);
                Assert.True(outside.Focus(), "Liste persos accepts focus beside the picker");
                Assert.False(picker.ContainsFocus);
                var beforeArrow = picker.ValueForTest(CharacterLookSlot.Body);
                form.PressAppearanceArrowForTest(Keys.Right);
                Assert.NotEqual(beforeArrow, picker.ValueForTest(CharacterLookSlot.Body));
                form.PressAppearanceArrowForTest(Keys.Left);
                Assert.Equal(beforeArrow, picker.ValueForTest(CharacterLookSlot.Body));
                WaitUntilInteractive(picker);
                Assert.True(picker.Focus(), "picker accepts focus for its own arrows");
                Assert.True(picker.HandleKeyForTest(Keys.Down));
                form.NewCharNameTextBoxForTest.Enabled = true;
                WaitUntilInteractive(form.NewCharNameTextBoxForTest);
                Assert.True(form.NewCharNameTextBoxForTest.Focus(), "name field accepts focus");
                var hairBefore = picker.ValueForTest(CharacterLookSlot.Hair);
                form.PressAppearanceArrowForTest(Keys.Right);
                Assert.Equal(hairBefore, picker.ValueForTest(CharacterLookSlot.Hair));

                form.NewCharNameTextBoxForTest.Text = "Ael";
                form.RememberAppearanceForTest();
                Assert.Contains("Ael", File.ReadAllText(path), StringComparison.Ordinal);
                var id = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
                form.BindAppearanceIdForTest("Ael", id);
                form.ApplySavedAppearanceForTest(id, "Ael");
                Assert.Equal("Forêt", form.ActiveLookForTest.Label(CharacterLookSlot.Body));
                Assert.Equal("Brun", form.ActiveLookForTest.Label(CharacterLookSlot.Hair));
                Assert.Equal("Lin", form.ActiveLookForTest.Label(CharacterLookSlot.Tunic));
                Assert.Equal("Retirer la tunique", form.EquipmentPanelForTest.TunicButtonTextForTest);

                form.ShowInventoryEquipmentForTest();
                StaTestRunner.PumpUntil(
                    () => form.EquipmentPanelForTest.TunicToggleInteractiveForTest,
                    TimeSpan.FromSeconds(5));
                form.EquipmentPanelForTest.ClickToggleTunicForTest();
                Assert.Equal("Porter la tunique", form.EquipmentPanelForTest.TunicButtonTextForTest);
                var saved = form.SettingsForTest;
                Assert.True(CharacterLookBook.TryGet(saved.CharacterLooks, id, "Ael", out var off));
                Assert.False(off.TunicWorn);
                Assert.Equal((byte)2, off.Tunic);

                StaTestRunner.PumpUntil(
                    () => form.EquipmentPanelForTest.TunicToggleInteractiveForTest,
                    TimeSpan.FromSeconds(5));
                form.EquipmentPanelForTest.ClickToggleTunicForTest();
                Assert.Equal("Retirer la tunique", form.EquipmentPanelForTest.TunicButtonTextForTest);
                Assert.Equal("Lin", form.ActiveLookForTest.Label(CharacterLookSlot.Tunic));
            }
            finally
            {
                if (form is not null)
                {
                    ClientSmokeTestAccess.CloseMainShell(form);
                }

                Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, previous);
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        });
    }

    private static void WaitUntilPickerReady(AppearancePickerPanel picker)
    {
        StaTestRunner.PumpUntil(
            () => PickerInteractive(picker),
            TimeSpan.FromSeconds(5));
    }

    private static bool PickerInteractive(AppearancePickerPanel picker)
    {
        if (!Interactive(picker))
        {
            return false;
        }

        foreach (CharacterLookSlot slot in Enum.GetValues<CharacterLookSlot>())
        {
            if (!Interactive(picker.NextButtonForTest(slot))
                || !Interactive(picker.PreviousButtonForTest(slot)))
            {
                return false;
            }
        }

        return true;
    }

    private static void WaitUntilInteractive(Control control)
    {
        StaTestRunner.PumpUntil(() => Interactive(control), TimeSpan.FromSeconds(5));
    }

    private static bool Interactive(Control control)
        => control.IsHandleCreated
           && control.Visible
           && control.Enabled
           && control.CanSelect
           && control.CanFocus;

    private static bool Contains(Bitmap bitmap, Color marker)
    {
        var argb = marker.ToArgb();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == argb)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
