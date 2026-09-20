using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>Player / NPC walk MVP: idle + 4-dir frames on the existing compose/draw path.</summary>
public sealed class ClientPlayerAnimTests
{
    [Fact]
    public void WalkSheets_AreNinetySixByOneTwentyEightPngs()
    {
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "player-walk.png"), 96, 128);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "player-walk-body.png"), 96, 128);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "player-walk-head.png"), 96, 128);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "player.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "player-body.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "player-head.png"), 32, 32);
    }

    [Fact]
    public void DrawPath_PassesPoseIntoComposer_KeepsEquipmentSlots()
    {
        var renderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var assets = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerWorldAssets.cs"));
        var slots = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerSpriteSlot.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Frog.Client.csproj"));
        var generator = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "generate-player-sprite.py"));

        Assert.Contains("PlayerSpritePose localPose", renderer, StringComparison.Ordinal);
        Assert.Contains("PlayerWorldAssets.DrawFeetAnchored", renderer, StringComparison.Ordinal);
        Assert.Contains("TryComposeBodyAndHead", assets, StringComparison.Ordinal);
        Assert.Contains("player-walk-body.png", assets, StringComparison.Ordinal);
        Assert.Contains("player-walk-head.png", assets, StringComparison.Ordinal);
        Assert.Contains("ComposeCell", assets, StringComparison.Ordinal);
        Assert.Contains("PlayerSpriteSlot.Body", assets, StringComparison.Ordinal);
        Assert.Contains("PlayerSpriteSlot.Head", assets, StringComparison.Ordinal);
        Assert.Contains("NativeSize = 32", assets, StringComparison.Ordinal);
        Assert.Contains("DrawScale = 1", assets, StringComparison.Ordinal);
        Assert.Contains("Tunic", slots, StringComparison.Ordinal);
        Assert.Contains("never Graal", slots, StringComparison.Ordinal);

        Assert.Contains("localPose: localPose", shell, StringComparison.Ordinal);
        Assert.Contains("otherPoses: otherPoses", shell, StringComparison.Ordinal);
        Assert.Contains("PlayerWalkClock.FacingFromVector", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("showTileGrid: true", shell, StringComparison.Ordinal);

        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-walk.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-walk-body.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-walk-head.png\"", csproj, StringComparison.Ordinal);

        Assert.Contains("WALK_COLS = 3", generator, StringComparison.Ordinal);
        Assert.Contains("WALK_ROWS = 4", generator, StringComparison.Ordinal);
        Assert.Contains("Never embeds Graal", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("urllib", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("urlopen", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requests.get", generator, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatusDoc_RecordsAnimMvp_NoWorldResize()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "animations", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("32×32", text, StringComparison.Ordinal);
        Assert.Contains("player-walk-body.png", text, StringComparison.Ordinal);
        Assert.Contains("player-walk-head.png", text, StringComparison.Ordinal);
        Assert.Contains("MapViewRenderer", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CC0", text, StringComparison.Ordinal);
        Assert.Contains("aucune sheet Graal", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.Contains("DefaultTileSizePixels = 32", text, StringComparison.Ordinal);
    }

    private static void AssertSheetPng(string path, int width, int height)
    {
        Assert.True(File.Exists(path), path);
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 33, "PNG too small: " + path);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
        Assert.Equal(width, ReadBigEndianInt32(bytes, 16));
        Assert.Equal(height, ReadBigEndianInt32(bytes, 20));
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

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
