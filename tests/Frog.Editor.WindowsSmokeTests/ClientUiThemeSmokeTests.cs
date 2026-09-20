using System;
using System.Drawing;
using System.IO;
using Frog.Client;
using Frog.Client.Config;
using Frog.Client.Forms;
using Frog.Client.UI;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>E0 : jetons DA appliqués une fois ; surfaces SHA Phase 8 non recolorées ; hôte/port toujours réels.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class ClientUiThemeSmokeTests
{
    [Fact]
    public void Tokens_MatchDaHex()
    {
        Assert.Equal(Color.FromArgb(0x0E, 0x12, 0x18), UiTheme.BgApp);
        Assert.Equal(Color.FromArgb(0x16, 0x1C, 0x28), UiTheme.BgPanel);
        Assert.Equal(Color.FromArgb(0x0C, 0x10, 0x18), UiTheme.BgSlot);
        Assert.Equal(Color.FromArgb(0xC9, 0xA2, 0x27), UiTheme.AccentGold);
        Assert.Equal(Color.FromArgb(0xF2, 0xF4, 0xF8), UiTheme.TextPrimary);
        Assert.Equal(Color.FromArgb(0xB7, 0x1C, 0x1C), UiTheme.StateError);
        Assert.Equal(Color.FromArgb(0xC6, 0x28, 0x28), UiTheme.BarHp);
        Assert.Equal(Color.FromArgb(0x15, 0x65, 0xC0), UiTheme.BarMp);
        Assert.Equal(Color.FromArgb(0x2E, 0x7D, 0x32), UiTheme.BarXp);
    }

    [Fact]
    public void Theme_TintAttributes_ReuseExistingDaTokens()
    {
        using var panel = UiTheme.CreatePanelTintAttributes();
        using var gold = UiTheme.CreateGoldTintAttributes();
        using var cream = UiTheme.CreatePrimaryTintAttributes();
        Assert.NotNull(panel);
        Assert.NotNull(gold);
        Assert.NotNull(cream);
        Assert.Equal(Color.FromArgb(0x16, 0x1C, 0x28), UiTheme.BgPanel);
        Assert.Equal(Color.FromArgb(0x0C, 0x10, 0x18), UiTheme.BgSlot);
        Assert.Equal(Color.FromArgb(0xC9, 0xA2, 0x27), UiTheme.AccentGold);
        Assert.Equal(Color.FromArgb(0xF2, 0xF4, 0xF8), UiTheme.TextPrimary);
        Assert.True(UiPackAssets.HasFramePanel, "Kenney frame resolves from output or repo");
        Assert.True(UiPackAssets.HasSlot);
        Assert.True(UiPackAssets.HasMenuPill);
        Assert.NotNull(UiPackAssets.CloneHotbarIcon(0));
        Assert.NotNull(UiPackAssets.CloneMenuIcon(HudMenuCommand.Inventory));
        Assert.Null(UiPackAssets.CloneHotbarIcon(3));
    }

    [Fact]
    public void MainShell_AppliesTheme_KeepsMapAndPhase8Panels_KeepsEndpointFields()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-e0-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                Assert.Equal(UiTheme.BgApp, form.BackColor);
                Assert.True(form.LoginShellForTest.Visible);
                Assert.Equal(400, form.LoginShellForTest.CardWidthForTest);
                Assert.Equal(12, form.LoginShellForTest.CardPaddingForTest);
                Assert.True(form.LoginShellForTest.HasGoldRingEmblemForTest);
                Assert.Equal("FRoG", form.LoginShellForTest.LogoWordmarkForTest.Text);
                Assert.True(form.LoginButtonForTest.Visible);
                Assert.Equal("Connexion", form.LoginButtonForTest.Text);
                Assert.Equal(UiTheme.BgSlot, form.LoginButtonForTest.BackColor);
                Assert.Equal(UiTheme.AccentGold, form.LoginButtonForTest.FlatAppearance.BorderColor);
                Assert.Equal(UiTheme.TextPrimary, form.LoginButtonForTest.ForeColor);
                Assert.True(form.RememberAccountCheckBoxForTest.Visible);
                Assert.Equal("Souvenir", form.RememberAccountCheckBoxForTest.Text);
                Assert.False(form.HostTextBoxForTest.Visible, "host leaves the player card");
                Assert.False(form.PortNumericForTest.Visible, "port leaves the player card");
                form.ToggleLoginOpsForTest();
                Assert.True(form.HostTextBoxForTest.Visible, "F9 ops strip reveals host");
                Assert.True(form.PortNumericForTest.Visible, "F9 ops strip reveals port");
                Assert.Equal("Options", form.OptionsButtonForTest.Text);

                Assert.False(UiTheme.IsPhase8ExactShaSurface(form.LoginButtonForTest));
                Assert.True(UiTheme.IsPhase8ExactShaSurface(form.DialoguePanelForTest));
                Assert.True(UiTheme.IsPhase8ExactShaSurface(form.QuestJournalPanelForTest));
                Assert.True(UiTheme.IsPhase8ExactShaSurface(form.EnvironmentPanelForTest));

                var dialogue = form.DialoguePanelForTest;
                Assert.NotEqual(UiTheme.BgPanel, dialogue.BackColor);
                Assert.Equal(SystemColors.Control, dialogue.BackColor);
                Assert.Equal(SystemColors.ControlText, dialogue.ForeColor);
                Assert.Equal(SystemColors.ControlText, form.QuestJournalPanelForTest.ForeColor);
                Assert.Equal(SystemColors.ControlText, form.EnvironmentPanelForTest.ForeColor);

                form.HostTextBoxForTest.Text = "10.0.0.8";
                form.PortNumericForTest.Value = 6123;
                var endpoint = form.SettingsForTest;
                endpoint.LastHost = form.HostTextBoxForTest.Text.Trim();
                endpoint.LastPort = (int)form.PortNumericForTest.Value;
                using var options = new OptionsForm(endpoint);
                Assert.Equal("10.0.0.8", options.HostTextBoxForTest.Text);
                Assert.Equal(6123, (int)options.PortNumericForTest.Value);
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

    [Fact]
    public void SettingsStore_PersistsLastHostPort()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-e0-ep-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var store = new ClientSettingsStore(path);
            var settings = store.Load();
            settings.LastHost = "192.0.2.10";
            settings.LastPort = 7010;
            store.Save(settings);
            var json = File.ReadAllText(path);
            Assert.Contains("192.0.2.10", json, StringComparison.Ordinal);
            Assert.Contains("7010", json, StringComparison.Ordinal);
            var reloaded = store.Load();
            Assert.Equal("192.0.2.10", reloaded.LastHost);
            Assert.Equal(7010, reloaded.LastPort);
            Directory.Delete(dir, recursive: true);
        });
    }
}
