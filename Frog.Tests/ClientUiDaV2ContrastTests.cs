using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>DA v2 step 1 — hotbar / menu ring contrast (Netsun). Linux source gates.</summary>
public sealed class ClientUiDaV2ContrastTests
{
    [Fact]
    public void Theme_DeclaresBgSlotAndPrimaryTint()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiTheme.cs"));
        Assert.Contains("BgSlot = Color.FromArgb(0x0C, 0x10, 0x18)", theme, StringComparison.Ordinal);
        Assert.Contains("AccentGold = Color.FromArgb(0xC9, 0xA2, 0x27)", theme, StringComparison.Ordinal);
        Assert.Contains("TextPrimary = Color.FromArgb(0xF2, 0xF4, 0xF8)", theme, StringComparison.Ordinal);
        Assert.Contains("CreatePrimaryTintAttributes", theme, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", theme, StringComparison.Ordinal);
        Assert.Contains("IsHudContrastButton", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void HotbarAndMenu_UseDarkFillGoldBorderCreamIcons()
    {
        var hotbar = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudHotbar.cs"));
        var menu = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudMenuRing.cs"));
        var pack = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiPackAssets.cs"));

        Assert.Contains("StyleContrastHudButton", hotbar, StringComparison.Ordinal);
        Assert.DoesNotContain("CloneSlot()", hotbar, StringComparison.Ordinal);
        Assert.Contains("bg.slot", hotbar, StringComparison.Ordinal);

        Assert.Contains("StyleContrastHudButton", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("CloneMenuPill()", menu, StringComparison.Ordinal);
        Assert.Contains("HudMenuCommand.Character", menu, StringComparison.Ordinal);
        Assert.Contains("HudMenuCommand.Inventory", menu, StringComparison.Ordinal);
        Assert.Contains("HudMenuCommand.Quests", menu, StringComparison.Ordinal);
        Assert.Contains("HudMenuCommand.Map", menu, StringComparison.Ordinal);
        Assert.Contains("HudMenuCommand.Options", menu, StringComparison.Ordinal);

        Assert.Contains("CloneTintedPrimary", pack, StringComparison.Ordinal);
        Assert.Contains("CreatePrimaryTintAttributes", pack, StringComparison.Ordinal);
        Assert.DoesNotContain("CloneTinted(source, new Size(18, 18), gold: true)", pack, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsContrastStep_OwnerNetsun()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("bg.slot", text, StringComparison.Ordinal);
        Assert.Contains("#0C1018", text, StringComparison.Ordinal);
        Assert.Contains("or-sur-or", text, StringComparison.Ordinal);
        Assert.Contains("HudHotbar", text, StringComparison.Ordinal);
        Assert.Contains("HudMenuRing", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
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
