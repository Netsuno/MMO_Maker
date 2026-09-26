using System;
using System.IO;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Parité MenuStrip WinForms : « Données de jeu… » sous Ressources ouvre le GameDataForm existant.
/// Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class MainFormGameDataMenuTests
{
    [Fact]
    public void ClassicMainForm_RessourcesMenu_OpensExistingGameDataForm_ProtocolStays11_TilesStay48()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var root = RepoRoot();
        var winForms = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "MainForm.cs"));
        var wpfXaml = File.ReadAllText(Path.Combine(root, "Frog.Editor", "MainWindow.xaml"));
        var wpfCode = File.ReadAllText(Path.Combine(root, "Frog.Editor", "MainWindow.xaml.cs"));
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));

        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);

        var resources = Slice(
            winForms,
            "var mResources = new ToolStripMenuItem(\"Ressources\");",
            "var mMap = new ToolStripMenuItem(\"Carte\");");
        var gameDataAt = resources.IndexOf("Données de jeu…", StringComparison.Ordinal);
        var tilesAt = resources.IndexOf("Charger une image tuiles…", StringComparison.Ordinal);
        Assert.True(gameDataAt >= 0, "Menu Ressources WinForms sans « Données de jeu… ».");
        Assert.True(tilesAt > gameDataAt, "« Données de jeu… » doit précéder « Charger une image tuiles… ».");
        Assert.Contains("(_, _) => OpenGameData()", resources, StringComparison.Ordinal);

        var open = Slice(winForms, "private void OpenGameData()", "internal void BrowsePhase8Content()");
        Assert.Contains("new GameData.GameDataForm()", open, StringComparison.Ordinal);
        Assert.Contains("EditorTestHooks.GameDataNonModalForTest", open, StringComparison.Ordinal);
        Assert.Contains("EditorTestHooks.OnGameDataFormShown?.Invoke(dlg)", open, StringComparison.Ordinal);
        Assert.Contains("dlg.Show(GetDialogOwner())", open, StringComparison.Ordinal);
        Assert.Contains("dlg.ShowDialog(GetDialogOwner())", open, StringComparison.Ordinal);
        Assert.DoesNotContain("ActorEditorPanel", open, StringComparison.Ordinal);
        Assert.DoesNotContain("class GameDataForm", open, StringComparison.Ordinal);

        var wpfResources = Slice(wpfXaml, "Header=\"_Ressources\"", "Header=\"_Carte\"");
        Assert.Contains("Header=\"Données de jeu…\"", wpfResources, StringComparison.Ordinal);
        Assert.Contains("CmdGameData", wpfResources, StringComparison.Ordinal);
        Assert.Contains("new Forms.GameData.GameDataForm()", wpfCode, StringComparison.Ordinal);
        Assert.Contains("EditorTestHooks.OnGameDataFormShown?.Invoke(dlg)", wpfCode, StringComparison.Ordinal);
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
