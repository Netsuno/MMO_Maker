using System.Windows.Forms;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MobSpawnZoneSmokeTests
{
    [Fact]
    public void Panel_AndCanvas_DefineZoneAndMobList_InFrench()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
            Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

            var map = new Map { Name = "Camp", Width = 8, Height = 6 };
            map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            var canvas = new MapCanvas { Map = map, ActiveTool = EditorTool.MobZone };
            Assert.Equal(32, canvas.TileSize);
            Assert.True(canvas.TryCommitMobZoneMarqueeForTest(1, 1, 3, 2));
            var zone = Assert.Single(canvas.MobSpawnZones.Zones);
            Assert.Equal(1, zone.TileX);
            Assert.Equal(1, zone.TileY);
            Assert.Equal(3, zone.Width);
            Assert.Equal(2, zone.Height);

            Assert.True(canvas.TryMoveSelectedMobZoneForTest(2, 2));
            Assert.Equal(2, canvas.SelectedMobSpawnZone!.TileX);
            Assert.True(canvas.TryCopySelectedMobZoneForTest(0, 0));
            Assert.Equal(2, canvas.MobSpawnZones.Zones.Count);
            Assert.Equal(0, canvas.SelectedMobSpawnZone!.TileX);

            var panel = new MobSpawnZonesPanel();
            panel.SetTroops(new[]
            {
                new MapEncounterTroopChoice(Guid.Parse("aaaaaaaa-0004-4000-8000-000000000001"), 4, "Gelée"),
            });
            panel.Bind(canvas.MobSpawnZones, canvas.SelectedMobZoneId);
            var labels = panel.JoinedLabelsForTest;
            Assert.Contains(MobSpawnZoneLabels.PanelTitle, labels, StringComparison.Ordinal);
            Assert.Contains(MobSpawnZoneLabels.Quantity, labels, StringComparison.Ordinal);
            Assert.Contains(MobSpawnZoneLabels.Respawn, labels, StringComparison.Ordinal);
            Assert.Contains(MobSpawnZoneLabels.Hint, labels, StringComparison.Ordinal);
            Assert.Equal(MobSpawnZoneLabels.ToolName, EditorToolHotkeys.DisplayName(EditorTool.MobZone));
            Assert.Equal("Z", EditorToolHotkeys.ShortcutGlyph(EditorTool.MobZone));
            Assert.Contains("glisser gauche déplace", EditorToolHotkeys.StatusHint(EditorTool.MobZone), StringComparison.Ordinal);
            Assert.Contains("glisser droit copie", EditorToolHotkeys.StatusHint(EditorTool.MobZone), StringComparison.Ordinal);
            Assert.Contains("Z zone", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);

            Assert.True(panel.TryAddEntryForTest(
                Guid.Parse("aaaaaaaa-0004-4000-8000-000000000001"),
                "Gelée",
                2,
                18));
            var entry = Assert.Single(canvas.SelectedMobSpawnZone.Entries);
            Assert.Equal(2, entry.Quantity);
            Assert.Equal(18, entry.RespawnSeconds);
            Assert.Contains("quantité 2", panel.EntryListForTest, StringComparison.Ordinal);
            Assert.Contains("18 s", panel.EntryListForTest, StringComparison.Ordinal);
        });
    }
}
