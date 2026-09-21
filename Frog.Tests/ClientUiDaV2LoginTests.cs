using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>DA v2 step 5 — login immersif (Netsun). Linux source gates.</summary>
public sealed class ClientUiDaV2LoginTests
{
    [Fact]
    public void Theme_KeepsContrastTokens_AndLoginShellApply()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiTheme.cs"));
        Assert.Contains("BgSlot = Color.FromArgb(0x0C, 0x10, 0x18)", theme, StringComparison.Ordinal);
        Assert.Contains("AccentGold = Color.FromArgb(0xC9, 0xA2, 0x27)", theme, StringComparison.Ordinal);
        Assert.Contains("TextPrimary = Color.FromArgb(0xF2, 0xF4, 0xF8)", theme, StringComparison.Ordinal);
        Assert.Contains("BgApp = Color.FromArgb(0x0E, 0x12, 0x18)", theme, StringComparison.Ordinal);
        Assert.Contains("BgPanel = Color.FromArgb(0x16, 0x1C, 0x28)", theme, StringComparison.Ordinal);
        Assert.Contains("IsUnderLoginShell", theme, StringComparison.Ordinal);
        Assert.Contains("case LoginShell", theme, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginShell_IsImmersiveCard_HostPortOffPlayerFace()
    {
        var login = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "LoginShell.cs"));

        Assert.Contains("CardWidth = 400", login, StringComparison.Ordinal);
        Assert.Contains("CharacterCardWidth = 520", login, StringComparison.Ordinal);
        Assert.Contains("CardPadding = 12", login, StringComparison.Ordinal);
        Assert.Contains("FieldWidth = 280", login, StringComparison.Ordinal);
        Assert.Contains("PaintDoubleGoldFrame", login, StringComparison.Ordinal);
        Assert.Contains("UiTheme.BgSlot", login, StringComparison.Ordinal);
        Assert.Contains("UiTheme.AccentGold", login, StringComparison.Ordinal);
        Assert.Contains("UiTheme.TextPrimary", login, StringComparison.Ordinal);
        Assert.Contains("Souvenir", login, StringComparison.Ordinal);
        Assert.Contains("Connexion", login, StringComparison.Ordinal);
        Assert.Contains("ToggleOps", login, StringComparison.Ordinal);
        Assert.Contains("Options → Réseau", login, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", login, StringComparison.Ordinal);
        Assert.Contains("class LoginCard", login, StringComparison.Ordinal);
        Assert.Contains("class LogoEmblem", login, StringComparison.Ordinal);
        Assert.Contains("HostCenteredCard", login, StringComparison.Ordinal);
        Assert.Contains("card.AutoScroll = needsScroll", login, StringComparison.Ordinal);
        Assert.Contains("row.MaximumSize = new Size(innerWidth, 0)", login, StringComparison.Ordinal);
        Assert.Contains("FillEllipse", login, StringComparison.Ordinal);
        Assert.Contains("DrawEllipse", login, StringComparison.Ordinal);
        Assert.DoesNotContain("CloneCta()", login, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateGoldTintAttributes", login, StringComparison.Ordinal);
        Assert.DoesNotContain("FROG_MOVEMENT_MEASURE", login, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayerWorldAssets", login, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_WiresLoginShell_AndF9Ops()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        Assert.Contains("LoginShell _loginShell", shell, StringComparison.Ordinal);
        Assert.Contains("_loginShell.Attach(", shell, StringComparison.Ordinal);
        Assert.Contains("Text = \"Connexion\"", shell, StringComparison.Ordinal);
        Assert.Contains("Keys.F9", shell, StringComparison.Ordinal);
        Assert.Contains("_loginShell.ToggleOps()", shell, StringComparison.Ordinal);
        Assert.Contains("ToggleLoginOpsForTest", shell, StringComparison.Ordinal);
        Assert.Contains("RememberAccountCheckBoxForTest", shell, StringComparison.Ordinal);
        Assert.Contains("PersistRememberedAccount", shell, StringComparison.Ordinal);
        Assert.Contains("HostTextBoxForTest", shell, StringComparison.Ordinal);
        Assert.Contains("PortNumericForTest", shell, StringComparison.Ordinal);
        Assert.Contains("LoginButtonForTest", shell, StringComparison.Ordinal);
        Assert.Contains("ConnectButtonForTest", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("AddStackToPanel(_panelLogin", shell, StringComparison.Ordinal);
        Assert.Contains("ShowCharacterSelectForTest", shell, StringComparison.Ordinal);
        Assert.Contains("rowEnter.Controls.Add(_btnEnterGame)", shell, StringComparison.Ordinal);
        Assert.Contains("rowCreateAction.Controls.Add(_btnCharCreate)", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("rowCharPick.Controls.Add(_btnEnterGame)", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("rowCreate.Controls.Add(_btnCharCreate)", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_RememberAccount_NeverStoresPassword()
    {
        var settings = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Config", "UserSettings.cs"));
        Assert.Contains("LastUsername", settings, StringComparison.Ordinal);
        Assert.Contains("RememberAccount", settings, StringComparison.Ordinal);
        Assert.Contains("Jamais le mot de passe", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("LastPassword", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("RememberPassword", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsLoginStep_OwnerNetsun()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-login.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("LoginShell", text, StringComparison.Ordinal);
        Assert.Contains("Connexion", text, StringComparison.Ordinal);
        Assert.Contains("Souvenir", text, StringComparison.Ordinal);
        Assert.Contains("F9", text, StringComparison.Ordinal);
        Assert.Contains("#0C1018", text, StringComparison.Ordinal);
        Assert.Contains("#C9A227", text, StringComparison.Ordinal);
        Assert.Contains("#F2F4F8", text, StringComparison.Ordinal);
        Assert.Contains("#161C28", text, StringComparison.Ordinal);
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
        var menu = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-da-v2-menu-five.md"));
        Assert.Contains("HudHotbar", contrast, StringComparison.Ordinal);
        Assert.Contains("bg.slot", contrast, StringComparison.Ordinal);
        Assert.Contains("STATUS-da-v2-login.md", contrast, StringComparison.Ordinal);
        Assert.Contains("titlebar", chrome, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("280×72", portrait, StringComparison.Ordinal);
        Assert.Contains("ø**40**", menu, StringComparison.Ordinal);
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
