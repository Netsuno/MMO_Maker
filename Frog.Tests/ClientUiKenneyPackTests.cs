using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Frog.Tests;

/// <summary>Pack Kenney + game-icons : fichiers, crédits, copie csproj. Pas de WinForms.</summary>
public sealed class ClientUiKenneyPackTests
{
    private static readonly string[] RequiredRelativeFiles =
    [
        "frames/panel.png",
        "frames/panel_inset.png",
        "slots/slot.png",
        "slots/slot_pressed.png",
        "menu/btn_round.png",
        "bars/hp_left.png",
        "bars/hp_mid.png",
        "bars/hp_right.png",
        "bars/mp_left.png",
        "bars/mp_mid.png",
        "bars/mp_right.png",
        "bars/track_left.png",
        "bars/track_mid.png",
        "bars/track_right.png",
        "chrome/btn_long.png",
        "chrome/arrow_left.png",
        "chrome/arrow_right.png",
        "menu/icon_perso.png",
        "menu/icon_inv.png",
        "menu/icon_quetes.png",
        "menu/icon_carte.png",
        "menu/icon_options.png",
        "hotbar/icon_melee.png",
        "hotbar/icon_spell.png",
        "hotbar/icon_interact.png",
        "menu/icon_perso.svg",
        "hotbar/icon_melee.svg",
        "THIRD_PARTY.md",
        "KIT-SELECTION.md",
    ];

    [Fact]
    public void UiPack_RequiredFilesExist_AndArePngOrCredits()
    {
        var root = Path.Combine(RepoRoot(), "Frog.Client", "Assets", "Ui");
        Assert.True(Directory.Exists(root), root);
        foreach (var relative in RequiredRelativeFiles)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), path);
            if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var header = new byte[8];
                using var stream = File.OpenRead(path);
                Assert.Equal(8, stream.Read(header, 0, 8));
                Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, header);
            }
        }
    }

    [Fact]
    public void Credits_RecordKenneyCc0_AndGameIconsCcBy_OwnerNetsun()
    {
        var files = new[]
        {
            Path.Combine(RepoRoot(), "THIRD_PARTY.md"),
            Path.Combine(RepoRoot(), "CREDITS.md"),
            Path.Combine(RepoRoot(), "Frog.Client", "Assets", "Ui", "THIRD_PARTY.md"),
            Path.Combine(RepoRoot(), "docs", "progress", "client-ui-kenney-pack", "STATUS.md"),
        };
        foreach (var path in files)
        {
            Assert.True(File.Exists(path), path);
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
            Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
        }

        var third = File.ReadAllText(Path.Combine(RepoRoot(), "THIRD_PARTY.md"));
        Assert.Contains("Kenney", third, StringComparison.Ordinal);
        Assert.Contains("CC0", third, StringComparison.Ordinal);
        Assert.Contains("game-icons.net", third, StringComparison.Ordinal);
        Assert.Contains("CC BY", third, StringComparison.Ordinal);
        Assert.Contains("barBlue_horizontalBlue.png", third, StringComparison.Ordinal);

        var packThird = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "Ui", "THIRD_PARTY.md"));
        Assert.Contains("Kenney", packThird, StringComparison.Ordinal);
        Assert.Contains("CC0", packThird, StringComparison.Ordinal);
        Assert.Contains("CC BY 3.0", packThird, StringComparison.Ordinal);
        Assert.Contains("mp_mid.png", packThird, StringComparison.Ordinal);

        var credits = File.ReadAllText(Path.Combine(RepoRoot(), "CREDITS.md"));
        Assert.Contains("Netsun", credits, StringComparison.Ordinal);
        Assert.Contains("Lorc", credits, StringComparison.Ordinal);
        Assert.Contains("Delapouite", credits, StringComparison.Ordinal);
        Assert.Contains("CC BY 3.0", credits, StringComparison.Ordinal);

        var status = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui-kenney-pack", "STATUS.md"));
        Assert.Contains("**Propriétaire** | Netsun", status, StringComparison.Ordinal);
        Assert.Contains("HudModulePanel", status, StringComparison.Ordinal);
        Assert.Contains("HudHotbar", status, StringComparison.Ordinal);
        Assert.Contains("HudMenuRing", status, StringComparison.Ordinal);
        Assert.Contains("DialoguePanel", status, StringComparison.Ordinal);
        Assert.Contains("non branchés", status, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientCsproj_CopiesUiPackToOutput()
    {
        var csproj = Path.Combine(RepoRoot(), "Frog.Client", "Frog.Client.csproj");
        var xml = XDocument.Load(csproj);
        var copies = xml.Descendants("None")
            .Where(e => ((string?)e.Attribute("Include") ?? string.Empty)
                .Contains("Assets\\Ui", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(copies);
        Assert.Contains(
            copies,
            e => string.Equals(
                (string?)e.Element("CopyToOutputDirectory"),
                "PreserveNewest",
                StringComparison.Ordinal));
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
