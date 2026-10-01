using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Enums;
using Frog.Editor.Forms;
using Frog.Editor.Panels;
using Frog.Editor.Ui;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// G4 : inspecteur replié sans sélection, couches compactes, statut mode · outil · couche · coords · zoom.
/// </summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorInspectorRailSmokeTests
{
    [Fact]
    public void StatusLine_OrdersModeToolLayerCoordsAndZoom()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
            Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
            Assert.Equal((byte)6, MapFormat.CurrentWriteVersion);

            var line = EditorStatusFormatter.Format(
                "Tuiles",
                "Pinceau (B)",
                "couche Sol",
                96,
                48,
                2,
                1,
                100,
                null,
                "    ·    catalogue mémoire");
            Assert.Equal(
                "Tuiles · Pinceau (B) · couche Sol · (96, 48) · tuile (2, 1) · zoom 100 %    ·    catalogue mémoire",
                line);

            var noticed = EditorStatusFormatter.Format(
                "Régions",
                "Région (G)",
                "couche Attributs",
                0,
                0,
                0,
                0,
                100,
                "Carte enregistrée",
                null);
            Assert.StartsWith("Carte enregistrée    ·    Régions · Région (G) · couche Attributs · (0, 0) · tuile (0, 0) · zoom 100 %", noticed, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AttributesMode_IsRegions_TileTypeAndRegion()
    {
        StaTestRunner.Run(() =>
        {
            var tools = new EditorLeftToolsWpf();
            Assert.Equal("Régions", tools.AttributesModeCaptionForTest);
            Assert.Equal("Tuiles", tools.PaletteModeLabel);
            tools.SetSelectedTool(EditorTool.Region);
            Assert.Equal("Régions", tools.PaletteModeLabel);
            tools.SetSelectedTool(EditorTool.Prefab);
            Assert.Equal("Objets", tools.PaletteModeLabel);
        });
    }

    [Fact]
    public void RightRail_CollapsesInspectorUntilSelection_KeepsLayers()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            MainForm? main = null;
            try
            {
                main = new MainForm(embedAsWpfChild: false)
                {
                    WindowState = FormWindowState.Normal,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Bounds = new Rectangle(40, 40, 1400, 900),
                };
                main.Show();
                main.NotifyWpfShellLayout();
                var form = main;
                StaTestRunner.PumpUntil(
                    () => form.LayersDockHeightForTest >= 120 && form.AssetsDockHeightForTest > form.LayersDockHeightForTest,
                    TimeSpan.FromSeconds(8));

                Assert.True(form.MapBannerHiddenForTest);
                Assert.True(form.InspectorCollapsedForTest);
                Assert.Equal(0, form.InspectorDockHeightForTest);
                Assert.InRange(form.LayersDockHeightForTest, 120, 200);
                Assert.True(form.LayersDockVisibleForTest);
                Assert.True(form.AssetsDockHeightForTest > form.LayersDockHeightForTest * 2);
                Assert.Contains("Tuiles ·", form.StatusTextForTest, StringComparison.Ordinal);
                Assert.Contains("couche ", form.StatusTextForTest, StringComparison.Ordinal);
                Assert.Contains("zoom ", form.StatusTextForTest, StringComparison.Ordinal);
                // Show() peut déjà placer le curseur sur la carte : la paire est le dernier survol
                // (défaut (0, 0) avant tout déplacement), pas forcément zéro.
                Assert.Matches(
                    @"· \(-?\d+, -?\d+\) · tuile \(-?\d+, -?\d+\) · zoom ",
                    form.StatusTextForTest);

                form.SelectEditorToolForTest(EditorTool.Prefab);
                StaTestRunner.PumpUntil(
                    () => form.StatusTextForTest.Contains("Objets ·", StringComparison.Ordinal)
                          && form.InspectorCollapsedForTest
                          && form.LayersDockHeightForTest >= 120,
                    TimeSpan.FromSeconds(4));
                Assert.True(form.LayersDockVisibleForTest);

                form.SelectEditorToolForTest(EditorTool.Region);
                StaTestRunner.PumpUntil(
                    () => !form.InspectorCollapsedForTest
                          && form.InspectorDockHeightForTest is >= 120 and <= 180
                          && form.RegionsInspectorVisibleForTest,
                    TimeSpan.FromSeconds(4));
                Assert.False(form.EntityInspectorVisibleForTest);
                Assert.False(form.TileInspectorVisibleForTest);
                Assert.Contains("Régions ·", form.StatusTextForTest, StringComparison.Ordinal);
                Assert.True(form.LayersDockVisibleForTest);

                form.SelectEditorToolForTest(EditorTool.Brush);
                StaTestRunner.PumpUntil(
                    () => form.InspectorCollapsedForTest && form.InspectorDockHeightForTest == 0,
                    TimeSpan.FromSeconds(4));

                form.SelectEditorToolForTest(EditorTool.Place);
                StaTestRunner.PumpUntil(
                    () => form.EntityInspectorVisibleForTest
                          && form.InspectorDockHeightForTest is >= 120 and <= 180,
                    TimeSpan.FromSeconds(4));

                form.SelectEditorToolForTest(EditorTool.Cursor);
                form.SetInspectedObjectForTest(new Tile { X = 2, Y = 3, Type = TileType.Block });
                StaTestRunner.PumpUntil(
                    () => form.TileInspectorVisibleForTest
                          && form.InspectorDockHeightForTest is >= 120 and <= 180,
                    TimeSpan.FromSeconds(4));

                form.SelectEditorToolForTest(EditorTool.Brush);
                StaTestRunner.PumpUntil(
                    () => form.InspectorCollapsedForTest,
                    TimeSpan.FromSeconds(4));
                Assert.True(form.LayersDockVisibleForTest);
                Assert.True(form.AssetsDockHeightForTest > form.LayersDockHeightForTest);
            }
            finally
            {
                if (main is { IsDisposed: false })
                {
                    main.Close();
                    var closing = main;
                    StaTestRunner.PumpUntil(() => closing.IsDisposed, TimeSpan.FromSeconds(5));
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }
}
