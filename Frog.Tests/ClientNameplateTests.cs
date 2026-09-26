using System;
using System.IO;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Plaques de nom client (joueurs, PNJ, événements <c>pnj_</c>). Hello reste 11.
/// </summary>
public sealed class ClientNameplateTests
{
    [Fact]
    public void Protocol_Stays11_NameplatesReuseKnownNames()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);

        var painter = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "NameplatePainter.cs"));
        var renderer = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "MapViewRenderer.cs"));
        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));

        Assert.Contains("NpcEventSlugPrefix = \"pnj_\"", painter, StringComparison.Ordinal);
        Assert.Contains("MessageBoxFont", painter, StringComparison.Ordinal);
        Assert.Contains("GraphicsUnit.Pixel", painter, StringComparison.Ordinal);
        Assert.Contains("UiTheme.TextPrimary", painter, StringComparison.Ordinal);
        Assert.DoesNotContain("AccentGold", painter, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId", painter, StringComparison.Ordinal);

        Assert.Contains("NameplatePainter.DrawAbove", renderer, StringComparison.Ordinal);
        Assert.Contains("NameplatePainter.LabelForNpcMapEvent", renderer, StringComparison.Ordinal);
        Assert.Contains("MapPlacedKind.Npc", renderer, StringComparison.Ordinal);
        Assert.Contains("includeName: false", renderer, StringComparison.Ordinal);
        Assert.Contains("localDisplayName", renderer, StringComparison.Ordinal);
        Assert.Contains("FormatLabel(localDisplayName)", renderer, StringComparison.Ordinal);
        Assert.Contains("FormatLabel(localUsername)", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("FrogWireProtocol.Version = 12", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId.Nameplate", renderer, StringComparison.Ordinal);

        Assert.Contains("localDisplayName: _activeCharacterName", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId.Nameplate", shell, StringComparison.Ordinal);
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
