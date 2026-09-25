using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Controls;
using Frog.Editor.Dialogs;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapPropertiesDialogSmokeTests
{
    [Fact]
    public void Dialog_ShowsFrenchMeta_ApplyResizesWithoutNewFormat()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);

            var map = DemoMapFactory.CreateStarter("Bois", 8, 6);
            map.Layers.Add(new Layer { LayerType = LayerType.Mask, Visible = true });
            MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground, TilesetId = 1, SrcX = 2 });
            MapEditOperations.PaintTile(map, 0, 6, 1, new Tile { Type = TileType.Ground, TilesetId = 1, SrcX = 9 });
            MapEditOperations.PaintTile(map, map.Layers.Count - 1, 1, 1, new Tile { Type = TileType.Ground, TilesetId = 1, SrcX = 3 });

            using var dialog = new MapPropertiesDialog(map, new Point(2, 3));
            var labels = dialog.JoinedLabelsForTest;
            Assert.Equal("Propriétés de la carte", dialog.Text);
            Assert.Contains("Nom", labels, StringComparison.Ordinal);
            Assert.Contains("Largeur (tuiles)", labels, StringComparison.Ordinal);
            Assert.Contains("Hauteur (tuiles)", labels, StringComparison.Ordinal);
            Assert.Contains("Chevauchement", labels, StringComparison.Ordinal);
            Assert.Contains("Identité", labels, StringComparison.Ordinal);
            Assert.Contains("Départ", labels, StringComparison.Ordinal);
            Assert.Contains("Appliquer", labels, StringComparison.Ordinal);
            Assert.DoesNotContain("Musique", labels, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Bois", dialog.NameTextForTest);
            Assert.Contains($"Feuille (v5, {WorldMetrics.DefaultTileSizePixels} px)", dialog.IdentityTextForTest, StringComparison.Ordinal);
            Assert.Contains("Départ playtest : (2, 3)", dialog.SpawnTextForTest, StringComparison.Ordinal);
            Assert.Contains("fichier carte", dialog.SpawnTextForTest, StringComparison.Ordinal);

            dialog.NameTextForTest = "Clairière";
            dialog.WidthForTest = 4;
            dialog.HeightForTest = 5;
            dialog.OverlapForTest = true;
            Assert.True(MapEditOperations.TryApplyProperties(map, dialog.PendingEdit, out var error));
            Assert.Null(error);
            Assert.Equal("Clairière", map.Name);
            Assert.Equal(4, map.Width);
            Assert.Equal(5, map.Height);
            Assert.True(map.AllowPlayerOverlap);
            Assert.Equal(TileGraphicIdentity.SheetSource, map.GraphicIdentity);
            Assert.Equal(0, map.TileSizePixels);
            Assert.Contains(map.Layers[0].Tiles, t => t.X == 1 && t.Y == 1);
            Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X == 6);
            Assert.Contains(map.Layers[^1].Tiles, t => t.X == 1 && t.Y == 1);
            Assert.True(map.Validate(out var validateError), validateError);
            Assert.Equal(MapSerializer.MapFileFormatVersion, new MapSerializer().Serialize(map)[4]);

            var bar = new MapPropertiesBar();
            bar.Bind(map, new Point(1, 1));
            Assert.Equal("Modifier…", bar.EditButtonTextForTest);
            Assert.Contains("Nom : Clairière", bar.SummaryForTest, StringComparison.Ordinal);
            Assert.Contains("4 × 5", bar.SummaryForTest, StringComparison.Ordinal);
            Assert.Contains("Départ playtest : (1, 1)", bar.SummaryForTest, StringComparison.Ordinal);
            Assert.DoesNotContain("Musique", bar.SummaryForTest, StringComparison.OrdinalIgnoreCase);

            var opened = false;
            bar.EditRequested += (_, _) => opened = true;
            Button? edit = null;
            foreach (Control child in bar.Controls)
            {
                if (child is Button button)
                {
                    edit = button;
                }
            }

            Assert.NotNull(edit);
            edit!.PerformClick();
            Assert.True(opened);
            bar.Bind(null, null);
            Assert.Equal("Aucune carte.", bar.SummaryForTest);
        });
    }
}
