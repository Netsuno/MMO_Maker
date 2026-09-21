using System.Drawing;
using System.IO;
using Frog.Application.Assets;
using Frog.Application.Prefabs;
using Frog.Client.Assets;
using Frog.Client.UI;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Editor;
using Frog.Editor.Forms.GameData;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class AssetPipelineSmokeTests
{
    [Fact]
    public void GameData_Tileset_ImportFillsPathShaAndPalette()
    {
        StaTestRunner.Run(() =>
        {
            GameDataSmokeTestHelper.ConfigureInMemory();
            var assetRoot = GameDataSmokeUiDriver.CreateSmokeAssetRoot();
            var source = Path.Combine(Path.GetTempPath(), $"frog-import-smoke-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(source, File.ReadAllBytes(Path.Combine(assetRoot, "preview.png")));
            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                EditorSmokeTestAccess.AssertShellReady(window);

                var form = GameDataSmokeUiDriver.OpenViaMainWindowCommand(window, EditorSmokeTestAccess.DefaultTimeout);
                var panel = form.TilesetsForTest;
                GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                GameDataSmokeUiDriver.SetText(panel.NameForTest, "ImportedTileset");
                panel.ImportAssetFromPathForTest(source);
                Assert.StartsWith("tiles/", panel.PathForTest.Text, StringComparison.Ordinal);
                Assert.True(panel.PathForTest.Text.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(AssetPreviewState.Loaded, panel.PreviewForTest.PreviewState);
                GameDataSmokeUiDriver.CloseForm(form, EditorSmokeTestAccess.DefaultTimeout);
            }
            finally
            {
                try
                {
                    File.Delete(source);
                }
                catch
                {
                    // best-effort
                }

                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
                GameDataSmokeUiDriver.CleanupAssetRoot(assetRoot);
            }
        });
    }

    [Fact]
    public void Client_LoadsSidecarAndRendersCustomTileNotFallback()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), $"frog-client-tiles-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                using var tile = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(tile))
                {
                    g.Clear(Color.FromArgb(200, 40, 40));
                }

                var pngPath = Path.Combine(dir, "source.png");
                tile.Save(pngPath);

                var bytes = File.ReadAllBytes(pngPath);
                MapTilesetPackage.WriteSidecars(dir, ["Coral Field"], [new MapTilesetFile(1, bytes)]);

                var map = new Map { Width = 2, Height = 1, Name = "Coral Field" };
                var layer = new Layer { LayerType = LayerType.Ground, Visible = true };
                layer.Tiles.Add(new Tile
                {
                    X = 0,
                    Y = 0,
                    TilesetId = 1,
                    SrcX = 0,
                    SrcY = 0,
                    Type = TileType.Ground,
                });
                map.Layers.Add(layer);

                var loaded = ClientTilesetLoader.LoadForMap(map, dir);
                Assert.True(loaded.ContainsKey(1));

                using var rendered = MapViewRenderer.Render(
                    map,
                    new Dictionary<string, (float, float)>(),
                    localUsername: null,
                    localCenterXPx: 48,
                    localCenterYPx: 16,
                    tilesetBitmaps: loaded);
                // Corner of tile (0,0) — away from the local player sprite on tile (1,0).
                var pixel = rendered.GetPixel(2, 2);
                Assert.True(
                    pixel.R > 150 && pixel.G < 80 && pixel.B < 80,
                    $"expected coral tile, got {pixel}");
                Assert.False(
                    pixel.R is >= 100 and <= 140 && pixel.G is >= 140 and <= 180,
                    $"fallback GroundTile color, got {pixel}");
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // best-effort
                }
            }
        });
    }

    [Fact]
    public void Client_MaterializesPublishedCatalogPng()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), $"frog-client-cat-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                using var tile = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(tile))
                {
                    g.Clear(Color.FromArgb(20, 180, 40));
                }

                using var ms = new MemoryStream();
                tile.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                var bytes = ms.ToArray();
                var sha = TilesetDefinition.ComputeSha256Hex(bytes);
                var pngBase64 = Convert.ToBase64String(bytes);
                var mapId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
                const string mapName = "Green Field";

                var catalog = new PublishedCatalogWire
                {
                    Tilesets =
                    [
                        new PublishedTilesetWireEntry
                        {
                            Id = Guid.NewGuid().ToString("D"),
                            Name = "Green",
                            PaletteId = 9,
                            LogicalPath = "tiles/green.png",
                            Sha256Hex = sha,
                            PngBase64 = pngBase64,
                            TileSizePixels = 32,
                            WidthPixels = 32,
                            HeightPixels = 32,
                        },
                    ],
                    Prefabs =
                    [
                        new PublishedPrefabWireEntry
                        {
                            Id = "sofa",
                            DisplayName = "Canapé",
                            Variants =
                            [
                                new PublishedPrefabVariantWire
                                {
                                    Facing = "south",
                                    SpriteFileName = "sofa-south.png",
                                    Sha256Hex = sha,
                                    PngBase64 = pngBase64,
                                },
                            ],
                        },
                    ],
                    PrefabMaps =
                    [
                        new PublishedPrefabMapWireEntry
                        {
                            MapId = mapId.ToString("D"),
                            MapName = mapName,
                            RuntimeMapId = 1,
                            Placements =
                            [
                                new PublishedPrefabPlacementWire
                                {
                                    PrefabId = "sofa",
                                    Facing = "south",
                                    TileX = 0,
                                    TileY = 0,
                                },
                            ],
                        },
                    ],
                };

                var map = new Map { Width = 1, Height = 1, Name = mapName };
                var layer = new Layer { LayerType = LayerType.Ground, Visible = true };
                layer.Tiles.Add(new Tile
                {
                    X = 0,
                    Y = 0,
                    TilesetId = 9,
                    SrcX = 0,
                    SrcY = 0,
                    Type = TileType.Ground,
                });
                map.Layers.Add(layer);
                Assert.Equal(new[] { 9 }, PublishedTilesetClientCoverage.MissingTilesetIds(map, catalog: null));

                var n = ClientPublishedTilesetMaterializer.Materialize(catalog, dir, map.Name);
                Assert.Equal(1, n);
                Assert.True(ClientPublishedPrefabMaterializer.Materialize(catalog, dir) > 0);
                Assert.True(File.Exists(Path.Combine(dir, "Tilesets", "9.png")));
                Assert.True(File.Exists(Path.Combine(dir, "Tilesets", "manifest.json")));
                Assert.True(File.Exists(Path.Combine(dir, "Maps", map.Name + ".tilesets.json")));
                Assert.True(File.Exists(Path.Combine(dir, "Prefabs", "sofa-south.png")));
                Assert.True(File.Exists(Path.Combine(dir, "Prefabs", "catalog.json")));
                Assert.True(File.Exists(Path.Combine(dir, "Maps", map.Name + ".prefabs.json")));
                Assert.True(File.Exists(Path.Combine(dir, "Maps", MapPrefabPackage.PlacementSidecarFileName(map.Name, mapId))));
                Assert.Empty(PublishedTilesetClientCoverage.MissingTilesetIds(map, catalog, dir));
                var loaded = ClientTilesetLoader.LoadForMap(map, dir);
                var prefabs = ClientPrefabLoader.LoadForMap(map, dir, catalog, mapId, runtimeMapId: 1);
                try
                {
                    Assert.True(loaded.ContainsKey(9));
                    Assert.Equal("sofa", Assert.Single(prefabs.Placements).PrefabId);
                    Assert.True(prefabs.Bitmaps.ContainsKey("sofa-south.png"));
                }
                finally
                {
                    foreach (var bmp in loaded.Values)
                    {
                        bmp.Dispose();
                    }

                    ClientPrefabLoader.DisposeBitmaps(prefabs.Bitmaps);
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // best-effort
                }
            }
        });
    }
}
