using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>Grille monde client : défaut off pour les joueurs (Netsun).</summary>
public sealed class ClientWorldGridTests
{
    [Fact]
    public void MapViewRenderer_TileGridDefaultsOff_PlayPathDoesNotEnable()
    {
        var renderer = Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs");
        var shell = Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs");
        Assert.True(File.Exists(renderer), renderer);
        Assert.True(File.Exists(shell), shell);

        var rendererText = File.ReadAllText(renderer);
        var shellText = File.ReadAllText(shell);

        Assert.Contains("bool showTileGrid = false", rendererText, StringComparison.Ordinal);
        Assert.Contains("if (showTileGrid)", rendererText, StringComparison.Ordinal);
        Assert.DoesNotContain("showTileGrid: true", shellText, StringComparison.Ordinal);
        Assert.Contains("MapViewRenderer.Render(_map, otherPx, _username, lcx, lcy, _tilesetBitmaps, _mapEvents)", shellText, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_OwnerIsNetsun_RecordsDefaultOff()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-hide-world-grid", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("showTileGrid", text, StringComparison.Ordinal);
        Assert.Contains("DrawRectangle", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
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
