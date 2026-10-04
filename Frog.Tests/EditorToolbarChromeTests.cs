using System;
using System.IO;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Chrome WPF de l’éditeur : la barre d’outils doit porter Segoe MDL2 via
/// <c>ToolBar.ButtonStyleKey</c> (un style implicite Button est ignoré, glyphes en carrés),
/// et le libellé de carte dans l’arbre gauche tronque au mot au lieu d’être coupé.
/// Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class EditorToolbarChromeTests
{
    [Fact]
    public void ToolbarButtons_UseMdl2ViaButtonStyleKey_AndOverflowChevronIsDark()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Editor", "MainWindow.xaml"));
        var toolbar = Slice(xaml, "<ToolBarTray", "</ToolBarTray>");

        var styleAt = toolbar.IndexOf("ToolBar.ButtonStyleKey", StringComparison.Ordinal);
        var fontAt = toolbar.IndexOf("Property=\"FontFamily\" Value=\"Segoe MDL2 Assets\"", StringComparison.Ordinal);
        Assert.True(styleAt >= 0, "Le style des boutons doit être clé ToolBar.ButtonStyleKey.");
        Assert.True(fontAt > styleAt, "Segoe MDL2 Assets doit être dans le style ToolBar.ButtonStyleKey.");

        var styleStart = toolbar.LastIndexOf("<Style ", fontAt, StringComparison.Ordinal);
        Assert.True(styleStart >= 0, "Setter FontFamily hors style.");
        var styleHead = toolbar.Substring(styleStart, fontAt - styleStart);
        Assert.Contains("ToolBar.ButtonStyleKey", styleHead, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetType=\"Button\"", toolbar, StringComparison.Ordinal);

        Assert.Contains("PART_ToolBarPanel", toolbar, StringComparison.Ordinal);
        Assert.Contains("PART_ToolBarOverflowPanel", toolbar, StringComparison.Ordinal);
        Assert.Contains("HasOverflowItems", toolbar, StringComparison.Ordinal);
        Assert.Contains("Value=\"Collapsed\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"#FFEBEEF5\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Autres commandes", toolbar, StringComparison.Ordinal);

        Assert.Contains("Content=\"&#xE710;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Content=\"&#xE8E5;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Content=\"&#xE74E;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Content=\"&#xE7A7;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Content=\"&#xE7A6;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Content=\"&#xE8A3;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Content=\"&#xE71F;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("Content=\"&#xE768;\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Nouvelle carte\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Tester (playtest, Ctrl+F5)\"", toolbar, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"ColLeft\" Width=\"280\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"180\" MaxWidth=\"340\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MapTreeLabel_TrimsOnWordBoundary_InsteadOfClipping()
    {
        var panel = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Editor", "Panels", "MapsProjectPanel.xaml"));
        var code = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Editor", "Panels", "MapsProjectPanel.xaml.cs"));

        Assert.Contains("TextTrimming=\"WordEllipsis\"", panel, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Disabled\"", panel, StringComparison.Ordinal);
        Assert.Contains("MONDE — cartes", panel, StringComparison.Ordinal);
        Assert.Contains("FormatShortId(entry.MapId)", code, StringComparison.Ordinal);
        Assert.Contains("entry.Name", code, StringComparison.Ordinal);
        Assert.DoesNotContain("TextTrimming=\"None\"", panel, StringComparison.Ordinal);
    }

    private static string Slice(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Marqueur introuvable : {start}");
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"Marqueur introuvable : {end}");
        return text.Substring(from, to - from);
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

        throw new InvalidOperationException("Impossible de localiser Frog.Creator.sln depuis " + AppContext.BaseDirectory);
    }
}
