using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Tests;

/// <summary>NPC / monster walk MVP: shared WalkClock + draw path for non-player entities.</summary>
public sealed class ClientNpcMonsterAnimTests
{
    [Fact]
    public void ProtocolVersion_Stays11()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Fact]
    public void WalkSheets_AreNinetySixByOneTwentyEightPngs()
    {
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "npc-walk.png"), 96, 128);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "monster-walk.png"), 96, 128);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "npc.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "monster.png"), 32, 32);
    }

    [Fact]
    public void DrawPath_ReferencesAnimationForNonPlayerEntities()
    {
        var renderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var assets = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "WorldEntityAssets.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Frog.Client.csproj"));
        var generator = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "generate-npc-monster-sprites.py"));
        var clock = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Gameplay", "WalkClock.cs"));

        Assert.Contains("npcCentersPx", renderer, StringComparison.Ordinal);
        Assert.Contains("monsterCentersPx", renderer, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyDictionary<string, WorldSpritePose>? npcPoses", renderer, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyDictionary<string, WorldSpritePose>? monsterPoses", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldEntityKind.Monster, actor.WorldPose)", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldEntityKind.Npc, actor.WorldPose)", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldDepth.RowSteps", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldEntityAssets.DrawFeetAnchored", renderer, StringComparison.Ordinal);
        Assert.Contains("WeatherOverlayRenderer.Draw(g, bmp.Size, weatherPlan, weatherTickMs)", renderer, StringComparison.Ordinal);

        Assert.Contains("npc-walk.png", assets, StringComparison.Ordinal);
        Assert.Contains("monster-walk.png", assets, StringComparison.Ordinal);
        Assert.Contains("WalkClock.SheetColumns", assets, StringComparison.Ordinal);
        Assert.Contains("centerYPx - dh + 1f", assets, StringComparison.Ordinal);
        Assert.Contains("NativeSize = 32", assets, StringComparison.Ordinal);
        Assert.Contains("DrawScale = 1", assets, StringComparison.Ordinal);
        Assert.Contains("Never Graal", assets, StringComparison.Ordinal);

        Assert.Contains("npcCentersPx: npcPx", shell, StringComparison.Ordinal);
        Assert.Contains("monsterCentersPx: monsterPx", shell, StringComparison.Ordinal);
        Assert.Contains("npcPoses: npcPoses", shell, StringComparison.Ordinal);
        Assert.Contains("monsterPoses: monsterPoses", shell, StringComparison.Ordinal);
        Assert.Contains("WalkClock.FacingFromVector", shell, StringComparison.Ordinal);
        Assert.Contains("AdvanceWorldEntitySmoothing", shell, StringComparison.Ordinal);
        Assert.Contains("new WorldSpritePose(", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("showTileGrid: true", shell, StringComparison.Ordinal);

        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\npc-walk.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\monster-walk.png\"", csproj, StringComparison.Ordinal);

        Assert.Contains("NPC_SRC_ROW = 9", generator, StringComparison.Ordinal);
        Assert.Contains("WALK_COLS = 3", generator, StringComparison.Ordinal);
        Assert.Contains("WALK_ROWS = 4", generator, StringComparison.Ordinal);
        Assert.Contains("WALK_FLIP_ROWS", generator, StringComparison.Ordinal);
        Assert.Contains("slime_cell", generator, StringComparison.Ordinal);
        Assert.Contains("Never embeds Graal", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("urllib", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("urlopen", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requests.get", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("itch", generator, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("FrameDurationMs = 140", clock, StringComparison.Ordinal);
        Assert.Contains("WorldEntityKind", clock, StringComparison.Ordinal);
        Assert.Contains("WorldSpritePose", clock, StringComparison.Ordinal);
        Assert.Equal(140, WalkClock.FrameDurationMs);
        Assert.Equal(PlayerWalkClock.Column(true, 280), WalkClock.Column(true, 280));
        Assert.Equal(PlayerWalkClock.Row(Direction.Left), WalkClock.Row(Direction.Left));
        Assert.Equal(1, WorldSpritePose.IdleDown.SheetColumn);
        Assert.Equal(0, WorldSpritePose.IdleDown.SheetRow);
        Assert.Equal(32, Frog.Core.Constants.WorldMetrics.DefaultTileSizePixels);
    }

    [Fact]
    public void StatusDoc_RecordsNpcMonsterAnim_NoWorldResizeOrExactShaEdits()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "animations", "STATUS-npc-monster.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("32×32", text, StringComparison.Ordinal);
        Assert.Contains("npc-walk.png", text, StringComparison.Ordinal);
        Assert.Contains("monster-walk.png", text, StringComparison.Ordinal);
        Assert.Contains("WalkClock", text, StringComparison.Ordinal);
        Assert.Contains("MapViewRenderer", text, StringComparison.Ordinal);
        Assert.Contains("WorldEntityAssets", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CC0", text, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("reste 11", text, StringComparison.Ordinal);
        Assert.Contains("exact-sha", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DefaultTileSizePixels = 32", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("itch", text, StringComparison.OrdinalIgnoreCase);

        var envPanel = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "EnvironmentPanel.cs"));
        Assert.DoesNotContain("WalkClock", envPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldEntityAssets", envPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("npc-walk", envPanel, StringComparison.Ordinal);
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
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | (bytes[offset + 3]);

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
