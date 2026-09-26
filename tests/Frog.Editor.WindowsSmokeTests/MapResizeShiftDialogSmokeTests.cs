using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Editor;
using Frog.Editor.Dialogs;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapResizeShiftDialogSmokeTests
{
    [Fact]
    public void Dialog_FrenchResizeShift_KeepsTileAssetIdentity()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
            Assert.Equal(MapResizeShift.CommandLabel, MainWindow.CmdMapResizeShift.Text);

            var map = MapFormat.CreateTileAssetMap("Bois", 8, 6);
            using var dialog = new MapResizeShiftDialog(map);
            var labels = dialog.JoinedLabelsForTest;
            Assert.Equal(MapResizeShift.DialogTitle, dialog.Text);
            Assert.Contains("Largeur (tuiles)", labels, StringComparison.Ordinal);
            Assert.Contains("Hauteur (tuiles)", labels, StringComparison.Ordinal);
            Assert.Contains("Décalage X (tuiles)", labels, StringComparison.Ordinal);
            Assert.Contains("Décalage Y (tuiles)", labels, StringComparison.Ordinal);
            Assert.Contains("Appliquer", labels, StringComparison.Ordinal);
            Assert.Contains("Annuler", labels, StringComparison.Ordinal);
            Assert.Contains("v6, 48 px", dialog.IdentityTextForTest, StringComparison.Ordinal);
            Assert.Contains("TileAssetId", dialog.IdentityTextForTest, StringComparison.Ordinal);
            Assert.DoesNotContain("32", dialog.IdentityTextForTest, StringComparison.Ordinal);

            dialog.WidthForTest = 10;
            dialog.HeightForTest = 9;
            dialog.DeltaXForTest = 1;
            dialog.DeltaYForTest = -2;
            var edit = dialog.PendingEdit;
            Assert.Equal(10, edit.Width);
            Assert.Equal(9, edit.Height);
            Assert.Equal(1, edit.DeltaX);
            Assert.Equal(-2, edit.DeltaY);

            var tile = new Frog.Core.Models.Tile
            {
                Type = Frog.Core.Enums.TileType.Ground,
                AssetId = Frog.Core.Maps.TileAssetId.FromStraightRgba(Solid(9, 8, 7, 255)),
            };
            map.Layers.Add(new Frog.Core.Models.Layer { LayerType = Frog.Core.Enums.LayerType.Ground });
            MapEditOperations.PaintTile(map, 0, 2, 2, tile);
            Assert.True(MapResizeShift.TryApply(map, edit, null, null, null, null, out var report, out var error));
            Assert.Null(error);
            Assert.Equal(3, tile.X);
            Assert.Equal(0, tile.Y);
            Assert.Equal(48, map.TileSizePixels);
            Assert.Equal(TileGraphicIdentity.TileAsset, map.GraphicIdentity);
            Assert.Equal(0, tile.SrcX);
            Assert.Equal(0, report.TilesRemoved);
            Assert.True(map.Validate(out var validateError), validateError);
        });
    }

    private static byte[] Solid(byte r, byte g, byte b, byte a)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = a;
        }

        return bytes;
    }
}
