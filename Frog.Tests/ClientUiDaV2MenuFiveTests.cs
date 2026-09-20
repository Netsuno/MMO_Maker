using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>DA v2 step 4 — menu ring 5 round icons (Netsun). Linux source gates.</summary>
public sealed class ClientUiDaV2MenuFiveTests
{
    [Fact]
    public void Theme_KeepsContrastTokens_AndRoundMenuApply()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiTheme.cs"));
        Assert.Contains("BgSlot = Color.FromArgb(0x0C, 0x10, 0x18)", theme, StringComparison.Ordinal);
        Assert.Contains("AccentGold = Color.FromArgb(0xC9, 0xA2, 0x27)", theme, StringComparison.Ordinal);
        Assert.Contains("TextPrimary = Color.FromArgb(0xF2, 0xF4, 0xF8)", theme, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", theme, StringComparison.Ordinal);
        Assert.Contains("HudMenuRing.RoundButton", theme, StringComparison.Ordinal);
        Assert.Contains("button.FlatAppearance.BorderSize = 0", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void MenuRing_HasFiveRoundIconButtons_WithFullLabels()
    {
        var menu = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudMenuRing.cs"));

        Assert.Contains("IconDiameter = 40", menu, StringComparison.Ordinal);
        Assert.Contains("FillEllipse", menu, StringComparison.Ordinal);
        Assert.Contains("DrawEllipse", menu, StringComparison.Ordinal);
        Assert.Contains("(\"Perso\", HudMenuCommand.Character)", menu, StringComparison.Ordinal);
        Assert.Contains("(\"Inventaire\", HudMenuCommand.Inventory)", menu, StringComparison.Ordinal);
        Assert.Contains("(\"Quêtes\", HudMenuCommand.Quests)", menu, StringComparison.Ordinal);
        Assert.Contains("(\"Carte\", HudMenuCommand.Map)", menu, StringComparison.Ordinal);
        Assert.Contains("(\"Options\", HudMenuCommand.Options)", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("(\"Inv\",", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("CloneMenuPill()", menu, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", menu, StringComparison.Ordinal);
        Assert.Contains("UiTheme.BgSlot", menu, StringComparison.Ordinal);
        Assert.Contains("UiTheme.AccentGold", menu, StringComparison.Ordinal);
        Assert.Contains("UiTheme.TextPrimary", menu, StringComparison.Ordinal);
        Assert.Contains("CloneMenuIcon", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("FROG_MOVEMENT_MEASURE", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayerWorldAssets", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("immersif", menu, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shell_WiresCarteAndOptions()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        Assert.Contains("case HudMenuCommand.Map:", shell, StringComparison.Ordinal);
        Assert.Contains("case HudMenuCommand.Options:", shell, StringComparison.Ordinal);
        Assert.Contains("OpenOptions();", shell, StringComparison.Ordinal);
        Assert.Contains("InvokeHudMenuCommandForTest", shell, StringComparison.Ordinal);
        Assert.Contains("_hudMenu.Command += OnHudMenuCommand", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsMenuFiveStep_OwnerNetsun()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-menu-five.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("Perso", text, StringComparison.Ordinal);
        Assert.Contains("Inventaire", text, StringComparison.Ordinal);
        Assert.Contains("Quêtes", text, StringComparison.Ordinal);
        Assert.Contains("Carte", text, StringComparison.Ordinal);
        Assert.Contains("Options", text, StringComparison.Ordinal);
        Assert.Contains("ø**40**", text, StringComparison.Ordinal);
        Assert.Contains("#0C1018", text, StringComparison.Ordinal);
        Assert.Contains("#C9A227", text, StringComparison.Ordinal);
        Assert.Contains("#F2F4F8", text, StringComparison.Ordinal);
        Assert.Contains("bg.slot", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PriorStepStatus_Remain()
    {
        var contrast = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS.md"));
        var chrome = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-chrome.md"));
        var portrait = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-status-portrait.md"));
        Assert.Contains("HudHotbar", contrast, StringComparison.Ordinal);
        Assert.Contains("bg.slot", contrast, StringComparison.Ordinal);
        Assert.Contains("STATUS-da-v2-menu-five.md", contrast, StringComparison.Ordinal);
        Assert.Contains("titlebar", chrome, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("280×72", portrait, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
