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
        "frames/panel_brown.png",
        "frames/panelInset_brown.png",
        "slots/buttonSquare_brown.png",
        "slots/buttonSquare_brown_pressed.png",
        "menu/buttonRound_brown.png",
        "bars/barRed_horizontalLeft.png",
        "bars/barRed_horizontalMid.png",
        "bars/barRed_horizontalRight.png",
        "bars/barBlue_horizontalLeft.png",
        "bars/barBlue_horizontalBlue.png",
        "bars/barBlue_horizontalRight.png",
        "bars/barBack_horizontalLeft.png",
        "bars/barBack_horizontalMid.png",
        "bars/barBack_horizontalRight.png",
        "chrome/buttonLong_brown.png",
        "chrome/arrowBrown_left.png",
        "chrome/arrowBrown_right.png",
        "icons/menu/walk.png",
        "icons/menu/backpack.png",
        "icons/menu/scroll-unfurled.png",
        "icons/menu/treasure-map.png",
        "icons/menu/cog.png",
        "icons/hotbar/broadsword.png",
        "icons/hotbar/fire-spell-cast.png",
        "icons/hotbar/hand.png",
        "CREDITS.md",
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
            Path.Combine(RepoRoot(), "Frog.Client", "Assets", "Ui", "CREDITS.md"),
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
