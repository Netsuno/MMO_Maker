using System;
using System.IO;

using Frog.Core.Constants;
using Frog.Core.IO;
using Frog.Core.Maps;

using Xunit;

namespace Frog.Tests;

/// <summary>
/// Le stub visuel d’attributs n’est pas branché : la PR #85 et le combo de type
/// possèdent déjà l’UX. Ce test fige la suppression et les surfaces réelles.
/// </summary>
public sealed class AttributePaletteCleanupTests
{
    [Fact]
    public void DeletedStubs_HaveNoReferences_AndShippedControlsOwnTheUx()
    {
        var root = RepoRoot();

        Assert.False(File.Exists(Path.Combine(root, "Frog.Editor", "Controls", "AttributePalette.cs")));
        Assert.False(File.Exists(Path.Combine(root, "Frog.Editor", "Services", "TileAttributeService.cs")));
        Assert.False(File.Exists(Path.Combine(root, "Frog.Editor", "Controls", "AttributePalette.resx")));

        var self = Path.GetFullPath(Path.Combine(root, "Frog.Tests", "AttributePaletteCleanupTests.cs"));
        foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (IsSkipped(path) || string.Equals(Path.GetFullPath(path), self, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ext = Path.GetExtension(path);
            if (ext is not ".cs" and not ".xaml" and not ".csproj" and not ".md" and not ".resx")
            {
                continue;
            }

            var text = File.ReadAllText(path);
            Assert.True(
                text.IndexOf("AttributePalette", StringComparison.Ordinal) < 0,
                $"Référence AttributePalette restante dans {path}");
            Assert.True(
                text.IndexOf("TileAttributeService", StringComparison.Ordinal) < 0,
                $"Référence TileAttributeService restante dans {path}");
        }

        var doc = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Docs", "analyse_edition_map.md"));
        Assert.Contains("TileTypePalette", doc, StringComparison.Ordinal);
        Assert.Contains("EditorLeftToolsWpf", doc, StringComparison.Ordinal);
        Assert.Contains("TileAssetFlagsPanel", doc, StringComparison.Ordinal);
        Assert.Contains("TileFlagEdit.Apply", doc, StringComparison.Ordinal);
        Assert.Contains("DrawTileTypeOverlay", doc, StringComparison.Ordinal);
        Assert.Contains("TileFlagEdit.OverlayText", doc, StringComparison.Ordinal);

        var typePalette = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Controls", "TileTypePalette.cs"));
        var leftTools = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Panels", "EditorLeftToolsWpf.xaml.cs"));
        foreach (var label in new[] { "Terrain", "Blocage", "Warp", "Ressource", "Script" })
        {
            Assert.Contains(label, typePalette, StringComparison.Ordinal);
            Assert.Contains(label, leftTools, StringComparison.Ordinal);
        }

        Assert.Contains("SelectedTileTypeChanged", typePalette, StringComparison.Ordinal);
        Assert.Contains("TileTypeChanged", leftTools, StringComparison.Ordinal);
        Assert.Contains("ComboTileType", leftTools, StringComparison.Ordinal);

        var mainForm = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "MainForm.cs"));
        Assert.Contains("_leftToolsWpf.TileTypeChanged += type => _canvas.SelectedTileType = type;", mainForm, StringComparison.Ordinal);

        var canvas = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Controls", "MapCanvas.cs"));
        Assert.Contains("Type = SelectedTileType", canvas, StringComparison.Ordinal);
        Assert.Contains("DrawTileTypeOverlay", canvas, StringComparison.Ordinal);

        var flags = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Controls", "TileAssetFlagsPanel.cs"));
        Assert.Contains("TileFlagEdit.Apply", flags, StringComparison.Ordinal);
        Assert.Contains("TileFlagEditMode.PassageGlobal", flags, StringComparison.Ordinal);
        Assert.Contains("TileFlagEditMode.Bush", flags, StringComparison.Ordinal);
        Assert.Contains("TileFlagEditMode.Counter", flags, StringComparison.Ordinal);
        Assert.Contains("TileFlagEditMode.Terrain", flags, StringComparison.Ordinal);

        var workbench = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Controls", "TileAssetWorkbench.cs"));
        Assert.Contains("TileFlagEdit.OverlayText", workbench, StringComparison.Ordinal);

        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((byte)6, MapFormat.CurrentWriteVersion);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    private static bool IsSkipped(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}.git{sep}", StringComparison.Ordinal)
            || path.Contains($"{sep}bin{sep}", StringComparison.Ordinal)
            || path.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
            || path.Contains($"{sep}TestResults{sep}", StringComparison.Ordinal)
            || path.Contains($"{sep}artifacts{sep}", StringComparison.Ordinal)
            || path.Contains($"{sep}node_modules{sep}", StringComparison.Ordinal);
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

        throw new InvalidOperationException("Racine du dépôt introuvable.");
    }
}
