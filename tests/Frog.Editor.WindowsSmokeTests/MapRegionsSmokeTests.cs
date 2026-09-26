using System.IO;
using System.Windows.Forms;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapRegionsSmokeTests
{
    [Fact]
    public void Panel_PaintsEncounters_AndSaveLoadKeepsV6()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
            Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

            var map = MapFormat.CreateTileAssetMap("Bois", 3, 2);
            map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            var untouched = MapFormat.Write(map);

            var monster = Guid.Parse("11111111-2222-4333-8444-555555555555");
            var panel = new MapRegionsPanel();
            panel.SetTroops(new[]
            {
                new MapEncounterTroopChoice(monster, 8, "Gelée"),
            });
            panel.Bind(map);

            var labels = panel.JoinedLabelsForTest;
            Assert.Contains(MapRegionLabels.PanelTitle, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.RegionNumber, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.Hint, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.Encounters, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.Steps, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.Weight, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.WholeMapHint, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.Add, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.Apply, labels, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.CatalogReady, panel.CatalogHintForTest, StringComparison.Ordinal);

            var empty = new MapRegionsPanel();
            empty.Bind(map);
            Assert.Contains(MapRegionLabels.NoCatalog, empty.CatalogHintForTest, StringComparison.Ordinal);

            panel.RegionIdForTest = 12;
            panel.StepsForTest = 18;
            Assert.Null(panel.TryAddEncounterForTest("Gelée", monster, 8, 6, "12"));
            Assert.Null(panel.TryAddEncounterForTest("Errant", Guid.Empty, null, 2, string.Empty));
            Assert.Equal(2, panel.EncounterCountForTest);
            Assert.Contains("Gelée · poids 6 · régions 12", panel.ListTextForTest, StringComparison.Ordinal);
            Assert.Contains(MapRegionLabels.WholeMap, panel.ListTextForTest, StringComparison.Ordinal);
            Assert.Equal(18, map.Regions!.EncounterSteps);

            using var canvas = new MapCanvas { TileSize = 48 };
            canvas.Map = map;
            canvas.ActiveTool = EditorTool.Region;
            canvas.ActiveRegionId = panel.RegionIdForTest;
            canvas.RaiseMouseDownForTest(MouseButtons.Left, 50, 4);
            canvas.RaiseMouseUpForTest(MouseButtons.Left, 50, 4);
            Assert.Equal(12, map.Regions.Get(1, 0));
            canvas.RaiseMouseDownForTest(MouseButtons.Right, 50, 4);
            canvas.RaiseMouseUpForTest(MouseButtons.Right, 50, 4);
            Assert.Equal(0, map.Regions.Get(1, 0));
            Assert.True(MapRegionEdit.TryPaint(map, 0, 1, panel.RegionIdForTest, out var paintError), paintError);

            var bytes = MapFormat.Write(map);
            Assert.Equal(untouched, bytes);
            Assert.Equal((byte)6, bytes[4]);
            Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);

            var dir = Path.Combine(Path.GetTempPath(), "frog-regions-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "bois.fmap");
                File.WriteAllBytes(path, bytes);
                MapRegionDocument.WriteForMap(path, map);
                var loaded = MapFormat.Read(File.ReadAllBytes(path));
                Assert.True(MapRegionDocument.TryAttach(loaded, path, out var error), error);
                Assert.Equal(48, loaded.TileSizePixels);
                Assert.Equal(12, loaded.Regions!.Get(0, 1));
                Assert.Equal(18, loaded.Regions.EncounterSteps);
                Assert.Equal(monster, loaded.Regions.Encounters[0].MonsterId);
                Assert.Equal(8, loaded.Regions.Encounters[0].AliasId);
                Assert.Equal("Errant", loaded.Regions.Encounters[1].Label);
                Assert.Empty(loaded.Regions.Encounters[1].Regions);
                Assert.Contains("numéro 12", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
                Assert.Equal(MapRegionLabels.ToolName, EditorToolHotkeys.DisplayName(EditorTool.Region));
                Assert.Equal("G", EditorToolHotkeys.ShortcutGlyph(EditorTool.Region));
                Assert.Contains("G région", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        });
    }
}
