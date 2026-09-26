using System.Drawing;
using System.IO;
using Frog.Application.Maps;
using Frog.Core.Animation;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Controls;
using Frog.Editor.Panels;
using Frog.Editor.Services;
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

                canvas.ActiveTilesetId = 99;
                Assert.True(canvas.TryResolveDrawnTileForTest(1, 1, 200, 64, 64, out var placedX, out var placedY));
                Assert.Equal(32, placedX);
                Assert.Equal(0, placedY);
                canvas.ActiveTilesetId = tilesetId;

                var animHint = canvas.GetPaintStatusHint();
                Assert.Contains("aperçu animé", animHint, StringComparison.Ordinal);
                Assert.Contains("2 images", animHint, StringComparison.Ordinal);
                Assert.DoesNotContain("frame", animHint, StringComparison.OrdinalIgnoreCase);

                TilesetAnimCatalog.PreviewEnabled = false;
                Assert.Contains("aperçu arrêté", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
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

    [Fact]
    public void AutotileGhost_PreviewsShore_WithoutWritingTheMap()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            try
            {
                Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
                Assert.Equal((ushort)11, FrogWireProtocol.Version);
                Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);

                var catalogue = new TileAssetCatalogue();
                catalogue.ImportStraightRgba(Solid(20, 80, 200), 48, 48);
                catalogue.ImportStraightRgba(Solid(20, 140, 200), 48, 48);
                catalogue.ImportStraightRgba(Solid(20, 180, 220), 48, 48);
                catalogue.ImportStraightRgba(Solid(10, 40, 120), 48, 48);
                var center = catalogue.Ids[0];
                var west = catalogue.Ids[1];
                var east = catalogue.Ids[2];
                var isolated = catalogue.Ids[3];
                Assert.True(catalogue.TrySetFlags(
                    center,
                    TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.Center),
                    out var error), error);
                Assert.True(catalogue.TrySetFlags(
                    west,
                    TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.West),
                    out error), error);
                Assert.True(catalogue.TrySetFlags(
                    east,
                    TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.East),
                    out error), error);
                Assert.True(catalogue.TrySetFlags(
                    isolated,
                    TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.Isolated),
                    out error), error);

                var canvas = new MapCanvas
                {
                    TileSize = 48,
                    TileAssets = catalogue,
                    ActiveTileAssetId = center,
                    JoinAutotiles = true,
                    ActiveLayerIndex = 0,
                };
                var map = TileAssetMapEditing.CreateMap("Eau", 3, 1);
                canvas.Map = map;
                Assert.True(TileAssetMapEditing.TryPaint(
                    map,
                    0,
                    1,
                    0,
                    TileAssetMapEditing.CreateBrushTile(1, 0, center, TileType.Ground)));
                Assert.Equal(isolated, map.Layers[0].Tiles.Single().AssetId);

                canvas.SetHoverTileForTest(0, 0);
                var preview = canvas.PreviewAutotileGhostForTest();
                Assert.Equal(west, preview[(0, 0)]);
                Assert.Equal(east, preview[(1, 0)]);
                Assert.Equal(isolated, map.Layers[0].Tiles.Single().AssetId);
                Assert.DoesNotContain(map.Layers[0].Tiles, tile => tile.X == 0);

                var hint = canvas.GetPaintStatusHint();
                Assert.Contains("aperçu raccord", hint, StringComparison.Ordinal);
                Assert.Contains("Bord ouest", hint, StringComparison.Ordinal);
                Assert.DoesNotContain("frame", hint, StringComparison.OrdinalIgnoreCase);

                canvas.JoinAutotiles = false;
                var raw = canvas.PreviewAutotileGhostForTest();
                Assert.Equal(center, raw[(0, 0)]);
                Assert.False(raw.ContainsKey((1, 0)));
                Assert.DoesNotContain("aperçu raccord", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
                Assert.Equal(isolated, map.Layers[0].Tiles.Single().AssetId);
                Assert.Equal((byte)6, TileAssetMapEditing.Write(map)[4]);
            }
            finally
            {
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    private static string TilesetAnimationJsonPath(string png) =>
        Frog.Core.IO.TilesetAnimationJson.ImageSidecarPath(png);

    private static byte[] Solid(byte r, byte g, byte b)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = 255;
        }

        return bytes;
    }
}
