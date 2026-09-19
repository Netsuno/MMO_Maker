using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Frog.Client.Forms;
using Frog.Client.Services;
using Frog.Client.UI;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>E2–E8 : overlay HUD réel, TabControl 360 px, mouvement / focus inchangés.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class ClientHudOverlaySmokeTests
{
    [Fact]
    public void HudLayer_FullFrameMap_KeepsTabCrop_AndRealWires()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-e2-hud-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                form.LayoutGameHudForTest();
                Assert.False(form.WindowLayerVisibleForTest);
                Assert.False(form.GameToolbarOnWorldForTest);
                Assert.False(form.StatusHudForTest.TitleVisibleForTest);
                Assert.False(form.StatusHudForTest.XpBarVisibleForTest);
                Assert.True(form.MinimapForTest.TitleHeightForTest <= 20);
                Assert.Equal(DrawMode.OwnerDrawFixed, form.ChatDockForTest.HistoryDrawModeForTest);

                form.SetWindowLayerVisibleForTest(true);
                form.LayoutGameHudForTest();

                Assert.Equal(DockStyle.Fill, form.MapScrollForTest.Dock);
                Assert.Equal(form.WorldHostForTest, form.MapScrollForTest.Parent);
                Assert.Equal(16, form.SmoothTimerIntervalForTest);

                var tabs = form.GameplayTabsForTest;
                Assert.True(tabs.Visible);
                Assert.Equal(360, tabs.Width);
                Assert.True(
                    tabs.Width >= 300 && tabs.Width <= 400 && tabs.Height >= 250 && tabs.Height <= 700,
                    $"TabControl crop {tabs.Width}×{tabs.Height}");

                Assert.Equal(10, form.HotbarForTest.SlotCountForTest);
                Assert.Equal("1", form.HotbarForTest.SlotTextForTest(0));
                Assert.Contains("Mêlée", form.HotbarForTest.SlotToolTipForTest(0), StringComparison.Ordinal);
                Assert.True(form.HotbarForTest.SlotEnabledForTest(0));
                Assert.True(form.HotbarForTest.SlotEnabledForTest(1));
                Assert.True(form.HotbarForTest.SlotEnabledForTest(2));
                Assert.False(form.HotbarForTest.SlotEnabledForTest(3));
                Assert.False(form.HotbarForTest.SlotEnabledForTest(9));

                Assert.Equal(5, form.ChatDockForTest.VisibleChannelCountForTest);
                Assert.Equal(5, form.MenuRingForTest.PillCountForTest);
                Assert.Equal(
                    new[] { "Perso", "Inv", "Quêtes", "Carte", "Options" },
                    form.MenuRingForTest.PillTextsForTest.ToArray());

                form.StatusHudForTest.ApplyCombat(
                    new CombatStateWire
                    {
                        Level = 3,
                        Experience = 40,
                        Hp = 8,
                        MaxHp = 20,
                        Mp = 4,
                        MaxMp = 10,
                        Gold = 12,
                        IsDead = true,
                    },
                    "Netsun");
                Assert.Equal("Netsun", form.StatusHudForTest.NameTextForTest);
                Assert.Equal("Lv 3", form.StatusHudForTest.MetaTextForTest);
                Assert.DoesNotContain("HP", form.StatusHudForTest.MetaTextForTest, StringComparison.Ordinal);
                Assert.True(form.StatusHudForTest.IsDeadVisibleForTest);
                Assert.False(form.StatusHudForTest.XpBarVisibleForTest);

                form.QuestTrackerForTest.ApplySnapshot(new[]
                {
                    new QuestJournalEntryWire
                    {
                        Name = "Herbier",
                        Status = 1,
                        StageDescription = "Cueillir",
                        Objectives = new[]
                        {
                            new QuestObjectiveProgressWire { Description = "Fleurs", Current = 1, Required = 3 },
                        },
                    },
                });
                Assert.Equal("Herbier", form.QuestTrackerForTest.TitleTextForTest);
                Assert.Contains("Fleurs", form.QuestTrackerForTest.Objective1ForTest, StringComparison.Ordinal);

                form.ChatTextBoxForTest.Focus();
                Assert.True(InputService.IsTextInputFocus(form.ActiveControl));

                form.SetWindowLayerVisibleForTest(false);
                Assert.False(form.WindowLayerVisibleForTest);
                form.SelectPhase8TabForTest();
                Assert.True(form.WindowLayerVisibleForTest);
                Assert.True(form.IsPhase8TabSelectedForTest);
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
    public void Options_HasRealNavAndEndpointFields()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new UserSettings { LastHost = "192.0.2.20", LastPort = 6112 };
            using var options = new OptionsForm(settings);
            Assert.Equal(5, options.SectionNavForTest.Items.Count);
            Assert.Contains("Réseau", options.SectionNavForTest.Items.Cast<object>().Select(o => o.ToString()));
            Assert.Contains("Interface", options.SectionNavForTest.Items.Cast<object>().Select(o => o.ToString()));
            options.SectionNavForTest.SelectedItem = "Réseau";
            Assert.Equal("192.0.2.20", options.HostTextBoxForTest.Text);
            Assert.Equal(6112, (int)options.PortNumericForTest.Value);
            Assert.True(options.HostTextBoxForTest.Visible);
        });
    }

    [Fact]
    public void Tokens_StillMatchDaHex()
    {
        Assert.Equal(Color.FromArgb(0x0E, 0x12, 0x18), UiTheme.BgApp);
        Assert.Equal(Color.FromArgb(0x16, 0x1C, 0x28), UiTheme.BgPanel);
        Assert.Equal(Color.FromArgb(0xC6, 0x28, 0x28), UiTheme.BarHp);
    }
}
