using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>DA v2 step 2 — window chrome (Netsun). Linux source gates.</summary>
public sealed class ClientUiDaV2ChromeTests
{
    [Fact]
    public void Theme_DeclaresWindowChromeTokensAndGoldTabs()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiTheme.cs"));
        Assert.Contains("BgPanel = Color.FromArgb(0x16, 0x1C, 0x28)", theme, StringComparison.Ordinal);
        Assert.Contains("AccentGold = Color.FromArgb(0xC9, 0xA2, 0x27)", theme, StringComparison.Ordinal);
        Assert.Contains("TextPrimary = Color.FromArgb(0xF2, 0xF4, 0xF8)", theme, StringComparison.Ordinal);
        Assert.Contains("StateError = Color.FromArgb(0xB7, 0x1C, 0x1C)", theme, StringComparison.Ordinal);
        Assert.Contains("StyleWindowCloseButton", theme, StringComparison.Ordinal);
        Assert.Contains("StyleGoldTabs", theme, StringComparison.Ordinal);
        Assert.Contains("IsWindowCloseButton", theme, StringComparison.Ordinal);
        Assert.Contains("GoldTabsTag", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowChrome_UsesDaV2Metrics_AndDoesNotShrinkTabCrop()
    {
        var chrome = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudWindowChrome.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));

        Assert.Contains("TitleBarHeight = 30", chrome, StringComparison.Ordinal);
        Assert.Contains("CloseButtonSize = 20", chrome, StringComparison.Ordinal);
        Assert.Contains("ContentPadding = 12", chrome, StringComparison.Ordinal);
        Assert.Contains("AccentGold", chrome, StringComparison.Ordinal);
        Assert.Contains("StyleWindowCloseButton", chrome, StringComparison.Ordinal);
        Assert.Contains("public void Dismiss()", chrome, StringComparison.Ordinal);
        Assert.Contains("public new void PerformClick()", chrome, StringComparison.Ordinal);
        Assert.Contains("class WindowCloseButton", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("HudHotbar", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("FROG_MOVEMENT_MEASURE", chrome, StringComparison.Ordinal);

        Assert.Contains("HudWindowChrome _windowChrome", shell, StringComparison.Ordinal);
        Assert.Contains("_windowChrome.Host(tabRight)", shell, StringComparison.Ordinal);
        Assert.Contains("UiTheme.StyleGoldTabs", shell, StringComparison.Ordinal);
        Assert.Contains("_gameplayTabs.Size = new Size(360, tabH)", shell, StringComparison.Ordinal);
        Assert.Contains("Phase8ExactShaPanelWidth = 324", shell, StringComparison.Ordinal);
        Assert.Contains("new(\"Inventaire\")", shell, StringComparison.Ordinal);
        Assert.Contains("new(\"Quêtes\")", shell, StringComparison.Ordinal);
        Assert.Contains("\"Perso\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("HudStatusModule", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsChromeStep_OwnerNetsun()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-chrome.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("titlebar", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#161C28", text, StringComparison.Ordinal);
        Assert.Contains("#C9A227", text, StringComparison.Ordinal);
        Assert.Contains("#F2F4F8", text, StringComparison.Ordinal);
        Assert.Contains("20×20", text, StringComparison.Ordinal);
        Assert.Contains("Inventaire", text, StringComparison.Ordinal);
        Assert.Contains("Quêtes", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContrastStatus_RemainsForStep1()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS.md");
        var text = File.ReadAllText(path);
        Assert.Contains("HudHotbar", text, StringComparison.Ordinal);
        Assert.Contains("bg.slot", text, StringComparison.Ordinal);
        Assert.Contains("#0C1018", text, StringComparison.Ordinal);
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
