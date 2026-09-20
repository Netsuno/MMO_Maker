using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>Local player is an original world sprite, not a gold ellipse (Netsun).</summary>
public sealed class ClientPlayerSkinTests
{
    [Fact]
    public void PlayerPng_IsOriginalSixteenSquarePng()
    {
        var path = Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World", "player.png");
        Assert.True(File.Exists(path), path);
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 33, "PNG too small");
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
        Assert.Equal(16, ReadBigEndianInt32(bytes, 16));
        Assert.Equal(16, ReadBigEndianInt32(bytes, 20));
    }

    [Fact]
    public void Renderer_DrawsNearestNeighborSprite_NotGoldEllipse()
    {
        var renderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var assets = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerWorldAssets.cs"));
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Frog.Client.csproj"));

        Assert.DoesNotContain("FillEllipse", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfPlayer", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("240, 200, 60", renderer, StringComparison.Ordinal);
        Assert.Contains("DrawPlayerSpriteAtPixelCenter", renderer, StringComparison.Ordinal);
        Assert.Contains("PlayerWorldAssets.DrawCentered", renderer, StringComparison.Ordinal);
        Assert.Contains("bool showTileGrid = false", renderer, StringComparison.Ordinal);

        Assert.Contains("NearestNeighbor", assets, StringComparison.Ordinal);
        Assert.Contains("DrawScale = 2", assets, StringComparison.Ordinal);
        Assert.Contains("player.png", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateGoldTintAttributes", assets, StringComparison.Ordinal);

        Assert.Contains("Assets\\World\\**\\*", csproj, StringComparison.Ordinal);
        Assert.Contains("CopyToOutputDirectory", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player.png\"", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void CreditDoc_RecordsOriginalCc0_NotThirdPartyPack()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "THIRD_PARTY.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("CC0", text, StringComparison.Ordinal);
        Assert.Contains("original", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a third-party pack", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player.png", text, StringComparison.Ordinal);
        Assert.Contains("no itch.io", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
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
