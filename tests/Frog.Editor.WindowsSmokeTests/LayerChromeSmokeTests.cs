using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Controls;
using Frog.Editor.Panels;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class LayerChromeSmokeTests
{
    [Fact]
    public void LayerStrip_SelectsShowLockAndOpacity_WithoutWritingFmap()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
            Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
            Assert.Equal((byte)6, MapFormat.CurrentWriteVersion);

            var panel = new LayersProjectPanel();
            var window = new System.Windows.Window
            {
                Content = panel,
                Width = 640,
                Height = 420,
                WindowStyle = System.Windows.WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            window.Show();
            try
            {
                int? selected = null;
                (int index, bool visible)? visibility = null;
                (int index, bool locked)? locked = null;
                (int index, float opacity)? opacity = null;
                bool? dim = null;
                panel.LayerSelected += (_, index) => selected = index;
                panel.LayerVisibilityChanged += (_, change) => visibility = change;
                panel.LayerLockChanged += (_, change) => locked = change;
                panel.LayerPreviewOpacityChanged += (_, change) => opacity = change;
                panel.DimOthersChanged += (_, on) => dim = on;

                panel.ApplyRows(
                [
                    Row(2, "Frange", locked: false),
                    Row(1, "Masque", locked: true),
                    Row(0, "Sol", locked: false),
                ],
                selectedIndex: 0);
                window.UpdateLayout();

                Assert.Equal("COUCHES", panel.TitleForTest);
                Assert.Contains("1 = dessous", panel.HintForTest, StringComparison.Ordinal);
                Assert.Equal("Atténuer les autres", panel.DimOthersCaptionForTest);
                Assert.Contains(".fmap", panel.DimOthersToolTipForTest, StringComparison.Ordinal);
                Assert.Equal("Opacité", panel.OpacityHeaderForTest);
                Assert.Contains("enregistré", panel.OpacityHeaderToolTipForTest, StringComparison.Ordinal);
                Assert.Equal(["1 Sol", "2 Masque", "3 Frange"], panel.StripCaptionsForTest);
                Assert.Equal(3, panel.StripCountForTest);
                Assert.False(panel.DimOthersForTest);
                Assert.Equal(0, panel.GetSelectedLayerIndex());

                panel.SelectStripForTest(2);
                Assert.Equal(2, selected);
                Assert.Equal(2, panel.GetSelectedLayerIndex());

                panel.ToggleVisibleForTest(2);
                Assert.True(visibility.HasValue);
                Assert.Equal(2, visibility.Value.index);
                Assert.False(visibility.Value.visible);

                panel.ToggleLockForTest(1);
                Assert.True(locked.HasValue);
                Assert.Equal(1, locked.Value.index);
                Assert.False(locked.Value.locked);

                panel.SetOpacityPercentForTest(0, 40);
                Assert.True(opacity.HasValue);
                Assert.Equal(0, opacity.Value.index);
                Assert.Equal(0.4f, opacity.Value.opacity);

                panel.ToggleDimOthersForTest();
                Assert.True(dim);
                Assert.True(panel.DimOthersForTest);
            }
            finally
            {
                window.Close();
            }

            var map = MapFormat.CreateTileAssetMap("Bois", 4, 3);
            map.Layers.Add(new Layer { LayerType = LayerType.Ground, DisplayName = "Sol" });
            map.Layers.Add(new Layer { LayerType = LayerType.Fringe, DisplayName = "Frange" });
            map.Layers.Add(new Layer { LayerType = LayerType.Attributes, DisplayName = "Attributs" });
            var before = MapFormat.Write(map);

            var canvas = new MapCanvas { TileSize = TileAssetMetrics.TargetTileSizePixels };
            canvas.Map = map;
            canvas.ActiveLayerIndex = 0;
            Assert.Equal(48, canvas.TileSize);
            Assert.Equal(1f, canvas.LayerDrawAlphaForTest(0));
            Assert.Equal(1f, canvas.LayerDrawAlphaForTest(1));

            canvas.SetLayerPreviewOpacity(1, 0.5f);
            Assert.Equal(0.5f, canvas.LayerDrawAlphaForTest(1));
            canvas.DimOtherLayers = true;
            Assert.Equal(1f, canvas.LayerDrawAlphaForTest(0));
            Assert.Equal(0.5f * LayerPreviewOpacity.DimOthersFactor, canvas.LayerDrawAlphaForTest(1));
            Assert.Equal(before, MapFormat.Write(map));
            Assert.Equal((byte)6, before[4]);
            Assert.Equal(48, map.TileSizePixels);

            map.Layers[0].Visible = false;
            Assert.Equal(0f, canvas.LayerDrawAlphaForTest(0));
            canvas.ResetLayerPreview();
            Assert.False(canvas.DimOtherLayers);
            Assert.Equal(1f, canvas.GetLayerPreviewOpacity(1));
            Assert.Equal(0f, canvas.LayerDrawAlphaForTest(0));
        });
    }

    private static LayerListRow Row(int index, string name, bool locked) => new()
    {
        Index = index,
        Visible = true,
        Locked = locked,
        Display = name,
        StripCaption = LayerTypeLabels.StripCaption(index, name),
        StripHint = LayerTypeLabels.StripHint(index, 3, name, locked),
        LockLabel = LayerTypeLabels.LockCaption(locked),
        OpacityPercent = 100,
        IsPaintTarget = index == 0,
    };
}
