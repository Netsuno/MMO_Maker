using System.Drawing;
using System.IO;
using Frog.Application.Maps;
using Frog.Core.Animation;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Controls;
using Frog.Editor.Panels;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasTileAnimSmokeTests
{
    [Fact]
    public void PaletteMark_PaintsOrigin_AndCanvasResolvesNextFrame()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            try
            {
                EditorSmokeTestAccess.EnsureWpfApplicationInitialized();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                var picker = new TilesetPickerPanelWpf();
                Assert.Equal("Animer la sélection", picker.MarkButtonTextForTest);
                Assert.Equal("Retirer l’animation", picker.ClearButtonTextForTest);
                Assert.Contains("style RPG Maker", picker.AnimHintTextForTest, StringComparison.Ordinal);

                picker.SyncPaletteTileSize(32);
                picker.SetPaletteTileset(tilesetId);
                Assert.True(picker.TrySetStampPixels(new Point(0, 0), new Size(64, 32)));

                var message = picker.MarkSelectionAnimated();
                Assert.Contains("2 frames", message, StringComparison.Ordinal);
                Assert.Equal(new Size(32, 32), picker.StampSizeForTest);

                var canvas = new MapCanvas { TileSize = 32 };
                canvas.Map = DemoMapFactory.CreateStarter();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedSrc = new Point(0, 0);
                canvas.SelectedStampInTiles = new Size(2, 1);
                canvas.ActiveLayerIndex = 0;
                Assert.True(canvas.TryPaintTileForTest(1, 1));

                var tiles = canvas.Map!.Layers[0].Tiles;
                Assert.Contains(tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 0 && t.SrcY == 0);
                Assert.Contains(tiles, t => t.X == 2 && t.Y == 1 && t.SrcX == 0 && t.SrcY == 0);
                Assert.DoesNotContain(tiles, t => t.X == 2 && t.Y == 1 && t.SrcX == 32);

                Assert.True(canvas.TryResolveAnimDrawSourceForTest(tilesetId, 0, 0, 0, 64, 64, out var x0, out var y0));
                Assert.Equal(0, x0);
                Assert.Equal(0, y0);
                Assert.True(canvas.TryResolveAnimDrawSourceForTest(tilesetId, 0, 0, 200, 64, 64, out var x1, out _));
                Assert.Equal(32, x1);

                TilesetAnimCatalog.PreviewEnabled = false;
                Assert.False(canvas.TryResolveAnimDrawSourceForTest(tilesetId, 0, 0, 200, 64, 64, out _, out _));
                TilesetAnimCatalog.PreviewEnabled = true;

                Assert.Contains("retirée", picker.ClearSelectionAnimated(), StringComparison.OrdinalIgnoreCase);
                Assert.False(canvas.TryResolveAnimDrawSourceForTest(tilesetId, 0, 0, 200, 64, 64, out _, out _));
            }
            finally
            {
                TilesetAnimCatalog.PreviewEnabled = true;
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void ImageSidecar_RoundTripsOntoTilesetId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-anim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = Path.Combine(dir, "eau.png");
            File.WriteAllBytes(png, new byte[] { 1, 2, 3 });
            var set = new TilesetAnimationSet
            {
                TilesetId = 0,
                FrameDurationMs = 200,
                Strips =
                {
                    new AnimatedTileStrip
                    {
                        OriginX = 0,
                        OriginY = 0,
                        FrameCount = 3,
                        Layout = AnimatedTileFrames.LayoutHorizontal,
                    },
                },
            };
            File.WriteAllBytes(
                TilesetAnimationJsonPath(png),
                Frog.Core.IO.TilesetAnimationJson.SerializeSet(set));

            TilesetAnimCatalog.Clear();
            TilesetAnimCatalog.TryAttachImageSidecar(7, png);
            Assert.True(TilesetAnimCatalog.TryFrameCount(7, 0, 0, out var frames));
            Assert.Equal(3, frames);
        }
        finally
        {
            TilesetAnimCatalog.Clear();
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string TilesetAnimationJsonPath(string png) =>
        Frog.Core.IO.TilesetAnimationJson.ImageSidecarPath(png);
}
