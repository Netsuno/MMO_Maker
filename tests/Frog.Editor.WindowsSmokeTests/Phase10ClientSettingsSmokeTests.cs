using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Frog.Client.Forms;
using Frog.Client.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>P10-3a : settings JSON atomique, presets clavier, aide, version, diagnostics expurgés, status [ui].</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class Phase10ClientSettingsSmokeTests
{
    [Fact]
    public void SettingsStore_AzertyDefault_AtomicJson_QwertyAndRebindPersist()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-p10-3a-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            try
            {
                var store = new ClientSettingsStore(path);
                var settings = store.Load();
                Assert.Equal(KeyboardLayoutPreset.Azerty, settings.KeyboardPreset);
                Assert.Equal("Z", settings.Bindings.MoveUp);
                Assert.Equal("Q", settings.Bindings.MoveLeft);
                Assert.Equal("S", settings.Bindings.MoveDown);
                Assert.Equal("D", settings.Bindings.MoveRight);
                Assert.Equal("E", settings.Bindings.Interact);

                var input = new InputService();
                input.Apply(settings);
                Assert.True(input.IsMoveUp(Keys.Z));
                Assert.True(input.IsMoveUp(Keys.Up));
                Assert.True(input.IsMoveLeft(Keys.Q));
                Assert.True(input.IsAttack(Keys.Space));
                Assert.True(input.IsMoveLeft(Keys.Left));
                Assert.False(input.IsMoveUp(Keys.W));

                settings.ApplyPreset(KeyboardLayoutPreset.Qwerty);
                settings.VolumePercent = 35;
                store.Save(settings);
                Assert.True(File.Exists(path));
                Assert.False(File.Exists(path + ".tmp"));
                var json = File.ReadAllText(path);
                Assert.Contains("qwerty", json, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"moveUp\": \"W\"", json, StringComparison.Ordinal);
                Assert.Contains("\"volumePercent\": 35", json, StringComparison.Ordinal);

                var reloaded = store.Load();
                Assert.Equal(KeyboardLayoutPreset.Qwerty, reloaded.KeyboardPreset);
                input.Apply(reloaded);
                Assert.True(input.IsMoveUp(Keys.W));
                Assert.True(input.IsMoveLeft(Keys.A));
                Assert.Equal(35, new SoundService { }.ApplyVolume(reloaded));
                Assert.Equal("Space", reloaded.Bindings.Attack);
                Assert.Equal(100, reloaded.UiScalePercent);

                reloaded.Bindings.MoveUp = "I";
                store.Save(reloaded);
                var rebound = store.Load();
                Assert.Equal("I", rebound.Bindings.MoveUp);
                input.Apply(rebound);
                Assert.True(input.IsMoveUp(Keys.I));
                Assert.True(input.IsMoveUp(Keys.Up));
                Assert.False(File.Exists(path + ".tmp"));
            }
            finally
            {
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

    [Fact]
    public void MainShell_HelpF1_VersionBadge_DiagnosticsRedacted_UiStatus()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-p10-3a-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                Assert.Contains("10.3.0", form.VersionBadgeTextForTest, StringComparison.Ordinal);
                Assert.Contains("ZQSD", form.MoveHintTextForTest, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("Aide", form.HelpButtonForTest.Text);
                Assert.Equal("Options", form.OptionsButtonForTest.Text);

                form.PassTextBoxForTest.Text = "secret-pass-p10-3a-never-copy";
                var diagnostics = form.CopyDiagnosticsForTest();
                Assert.Contains("10.3.0", diagnostics, StringComparison.Ordinal);
                Assert.Contains("diagnostics", diagnostics, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("secret-pass-p10-3a-never-copy", diagnostics, StringComparison.Ordinal);
                Assert.DoesNotContain("password", diagnostics, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("jeton", diagnostics, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(form.PassTextBoxForTest.Text, diagnostics, StringComparison.Ordinal);

                form.ShowPlayerStatusForTest("Serveur indisponible. Vérifiez l'adresse et que le serveur est lancé.");
                Assert.Contains("[ui]", form.LogTextForTest, StringComparison.Ordinal);
                Assert.Contains("Serveur indisponible", form.PlayerStatusTextForTest, StringComparison.Ordinal);
                Assert.Contains("[ui] Serveur indisponible", form.LogTextForTest, StringComparison.Ordinal);

                form.OpenHelpForTest();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.HelpFormForTest is { Visible: true },
                    TimeSpan.FromSeconds(10));
                var help = form.HelpFormForTest;
                Assert.NotNull(help);
                Assert.Contains("AZERTY", help!.HelpTextForTest, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Fabriquer", help.HelpTextForTest, StringComparison.Ordinal);
                help.Close();
                ClientSmokeTestAccess.PumpUntil(() => form.HelpFormForTest is null, TimeSpan.FromSeconds(10));

                form.ProcessF1ForTest();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.HelpFormForTest is { Visible: true },
                    TimeSpan.FromSeconds(10));
                form.HelpFormForTest!.Close();

                form.ApplyKeyboardPresetForTest(KeyboardLayoutPreset.Qwerty);
                Assert.Contains("WASD", form.MoveHintTextForTest, StringComparison.OrdinalIgnoreCase);
                var persisted = new ClientSettingsStore(path).Load();
                Assert.Equal(KeyboardLayoutPreset.Qwerty, persisted.KeyboardPreset);

                using var options = new OptionsForm(new UserSettings());
                options.Show();
                try
                {
                    options.LayoutComboForTest.SelectedIndex = 1;
                    options.VolumeTrackForTest.Value = 12;
                    options.MuteCheckBoxForTest.Checked = true;
                    options.MusicCheckBoxForTest.Checked = true;
                    options.SaveButtonForTest.PerformClick();
                    if (options.DialogResult != DialogResult.OK)
                    {
                        options.CommitSave();
                    }

                    Assert.Equal(DialogResult.OK, options.DialogResult);
                    Assert.Equal(KeyboardLayoutPreset.Qwerty, options.Settings.KeyboardPreset);
                    Assert.Equal(12, options.Settings.VolumePercent);
                    Assert.True(options.Settings.AudioMuted);
                    Assert.True(options.Settings.MusicEnabled);
                    Assert.Equal("W", options.Settings.Bindings.MoveUp);
                    Assert.Equal("Space", options.Settings.Bindings.Attack);
                    Assert.False(options.Settings.Window.FullScreen);
                    Assert.Equal(100, options.Settings.UiScalePercent);
                    options.FullScreenCheckBoxForTest.Checked = true;
                    options.UiScaleComboForTest.SelectedIndex = ClientUiScale.StepIndex(150);
                    options.AssignBindingForTest("MoveLeft", "H");
                    options.AssignBindingForTest("Attack", "F");
                    options.CommitSave();
                    Assert.True(options.Settings.Window.FullScreen);
                    Assert.Equal(150, options.Settings.UiScalePercent);
                    Assert.Equal("H", options.Settings.Bindings.MoveLeft);
                    Assert.Equal("F", options.Settings.Bindings.Attack);
                    Assert.Equal("150 %", options.UiScaleComboForTest.SelectedItem?.ToString());

                    var sound = form.SoundServiceForTest;
                    Assert.True(sound.PlayUiClick());
                    sound.SetMuted(true);
                    Assert.False(sound.PlayUiClick());
                    sound.SetMuted(false);
                }
                finally
                {
                    options.Close();
                }
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
}

internal static class SoundServiceTestExtensions
{
    public static int ApplyVolume(this SoundService sound, UserSettings settings)
    {
        sound.Apply(settings);
        return sound.VolumePercent;
    }
}
