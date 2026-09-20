using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>DA v2 step 3 — status HG portrait placeholder (Netsun). Linux source gates.</summary>
public sealed class ClientUiDaV2StatusPortraitTests
{
    [Fact]
    public void Theme_StillDeclaresStatusTokens()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiTheme.cs"));
        Assert.Contains("BgPanel = Color.FromArgb(0x16, 0x1C, 0x28)", theme, StringComparison.Ordinal);
        Assert.Contains("BgSlot = Color.FromArgb(0x0C, 0x10, 0x18)", theme, StringComparison.Ordinal);
        Assert.Contains("AccentGold = Color.FromArgb(0xC9, 0xA2, 0x27)", theme, StringComparison.Ordinal);
        Assert.Contains("TextPrimary = Color.FromArgb(0xF2, 0xF4, 0xF8)", theme, StringComparison.Ordinal);
        Assert.Contains("BarHp = Color.FromArgb(0xC6, 0x28, 0x28)", theme, StringComparison.Ordinal);
        Assert.Contains("BarMp = Color.FromArgb(0x15, 0x65, 0xC0)", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusHud_HasCircularPortraitAndKeepsKenneyBars()
    {
        var status = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudStatusModule.cs"));

        Assert.Contains("PortraitDiameter = 44", status, StringComparison.Ordinal);
        Assert.Contains("ModuleWidth = 280", status, StringComparison.Ordinal);
        Assert.Contains("ModuleHeight = 72", status, StringComparison.Ordinal);
        Assert.Contains("CircularPortraitPlaceholder", status, StringComparison.Ordinal);
        Assert.Contains("FillEllipse", status, StringComparison.Ordinal);
        Assert.Contains("DrawEllipse", status, StringComparison.Ordinal);
        Assert.Contains("UiTheme.BgSlot", status, StringComparison.Ordinal);
        Assert.Contains("UiTheme.AccentGold", status, StringComparison.Ordinal);
        Assert.Contains("TryGetBarBack", status, StringComparison.Ordinal);
        Assert.Contains("TryGetBarFill", status, StringComparison.Ordinal);
        Assert.Contains("XpBarVisibleForTest => false", status, StringComparison.Ordinal);
        Assert.Contains("ApplyCombat", status, StringComparison.Ordinal);
        Assert.Contains("GetColumn(_body) != 1", status, StringComparison.Ordinal);
        Assert.Contains("GetColumn(host) != 0", status, StringComparison.Ordinal);
        Assert.DoesNotContain("GetColumn(bodyRow)", status, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayerWorldAssets", status, StringComparison.Ordinal);
        Assert.DoesNotContain("FROG_MOVEMENT_MEASURE", status, StringComparison.Ordinal);
        Assert.DoesNotContain("HudMenuCommand", status, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusHud_DoesNotTouchMenuLoginOrChrome()
    {
        var status = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudStatusModule.cs"));
        var menu = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudMenuRing.cs"));
        var chrome = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudWindowChrome.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));

        Assert.DoesNotContain("HudWindowChrome", status, StringComparison.Ordinal);
        Assert.Contains("HudMenuCommand.Character", menu, StringComparison.Ordinal);
        Assert.Contains("TitleBarHeight = 30", chrome, StringComparison.Ordinal);
        Assert.Contains("_hudStatus.ApplyCombat(state, _username)", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("immersif", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatusDoc_RecordsPortraitStep_OwnerNetsun()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-status-portrait.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("280×72", text, StringComparison.Ordinal);
        Assert.Contains("ø44", text, StringComparison.Ordinal);
        Assert.Contains("#161C28", text, StringComparison.Ordinal);
        Assert.Contains("#C9A227", text, StringComparison.Ordinal);
        Assert.Contains("#0C1018", text, StringComparison.Ordinal);
        Assert.Contains("#F2F4F8", text, StringComparison.Ordinal);
        Assert.Contains("Kenney", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContrastAndChromeStatus_RemainForPriorSteps()
    {
        var contrast = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS.md"));
        var chrome = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-chrome.md"));
        Assert.Contains("HudHotbar", contrast, StringComparison.Ordinal);
        Assert.Contains("bg.slot", contrast, StringComparison.Ordinal);
        Assert.Contains("STATUS-da-v2-status-portrait.md", contrast, StringComparison.Ordinal);
        Assert.Contains("titlebar", chrome, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20×20", chrome, StringComparison.Ordinal);
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
