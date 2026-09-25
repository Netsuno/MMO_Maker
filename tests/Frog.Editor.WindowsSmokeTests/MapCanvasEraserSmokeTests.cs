using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Frog.Editor.Panels;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasEraserSmokeTests
{
    [Fact]
    public void EraserTool_ClearsActiveLayerStamp_OneUndoRestores_MaskStays()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);

            var canvas = CreateCanvas();
            var map = canvas.Map ?? throw new InvalidOperationException("carte");
            var ground = canvas.ActiveLayerIndex;
            var mask = map.Layers.Count;
            map.Layers.Add(new Layer { LayerType = LayerType.Mask, Visible = true });
            MapEditOperations.PaintTile(map, ground, 1, 1, Sheet(canvas.ActiveTilesetId, 4));
            MapEditOperations.PaintTile(map, ground, 2, 1, Sheet(canvas.ActiveTilesetId, 5));
            MapEditOperations.PaintTile(map, ground, 3, 1, Sheet(canvas.ActiveTilesetId, 6));
            MapEditOperations.PaintTile(map, mask, 1, 1, Sheet(canvas.ActiveTilesetId, 9));

            canvas.SelectedStampInTiles = new System.Drawing.Size(2, 1);
            Assert.Contains("couche active", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.False(canvas.History.CanUndo);

            Assert.Equal(2, canvas.TryEraseStampForTest(1, 1));
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.DoesNotContain(map.Layers[ground].Tiles, t => t.X == 1 && t.Y == 1);
            Assert.DoesNotContain(map.Layers[ground].Tiles, t => t.X == 2 && t.Y == 1);
            Assert.Contains(map.Layers[ground].Tiles, t => t.X == 3 && t.Y == 1 && t.SrcX == 6);
            Assert.Contains(map.Layers[mask].Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 9);
            Assert.True(canvas.History.CanUndo);

            canvas.PerformUndo();
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Equal(3, map.Layers[ground].Tiles.Count);
            Assert.Single(map.Layers[mask].Tiles);
            Assert.False(canvas.History.CanUndo);
        });
    }

    [Fact]
    public void EraserTool_DragIsOneUndo_HiddenOrLockedDoesNothing()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            var map = canvas.Map ?? throw new InvalidOperationException("carte");
            var ground = canvas.ActiveLayerIndex;
            MapEditOperations.PaintTile(map, ground, 0, 0, Sheet(canvas.ActiveTilesetId, 1));
            MapEditOperations.PaintTile(map, ground, 1, 0, Sheet(canvas.ActiveTilesetId, 2));

            Assert.Equal(1, canvas.TryContinueEraserStrokeForTest(0, 0));
            Assert.Equal(1, canvas.TryContinueEraserStrokeForTest(1, 0));
            canvas.EndPaintStrokeForTest();
            Assert.Empty(canvas.Map!.Layers[ground].Tiles);
            Assert.True(canvas.History.CanUndo);
            canvas.PerformUndo();
            Assert.Equal(2, canvas.Map!.Layers[ground].Tiles.Count);
            Assert.False(canvas.History.CanUndo);

            var layer = canvas.Map.Layers[ground];
            layer.Visible = false;
            Assert.Equal(0, canvas.TryEraseStampForTest(0, 0));
            Assert.Equal(2, layer.Tiles.Count);
            Assert.False(canvas.History.CanUndo);

            layer.Visible = true;
            layer.Locked = true;
            Assert.Equal(0, canvas.TryEraseStampForTest(0, 0));
            Assert.Equal(2, layer.Tiles.Count);
            Assert.False(canvas.History.CanUndo);

            layer.Locked = false;
            Assert.Equal(0, canvas.TryEraseStampForTest(4, 4));
            Assert.False(canvas.History.CanUndo);
        });
    }

    [Fact]
    public void EraserTool_TileAssetMap_ClearsAssetOnActiveLayer()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var catalogue = new TileAssetCatalogue();
            catalogue.ImportStraightRgba(Solid(255, 0, 0), 48, 48);
            var red = catalogue.Ids[0];
            var canvas = new MapCanvas { TileSize = 48, TileAssets = catalogue, ActiveTileAssetId = red };
            var map = MapFormat.CreateTileAssetMap("gomme-v6", 3, 2);
            map.Layers.Add(new Layer { LayerType = LayerType.Ground, Visible = true });
            map.Layers.Add(new Layer { LayerType = LayerType.Mask, Visible = true });
            canvas.Map = map;
            canvas.ActiveLayerIndex = 0;
            MapEditOperations.PaintTile(map, 0, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });
            MapEditOperations.PaintTile(map, 1, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });

            Assert.Equal(1, canvas.TryEraseStampForTest(0, 0));
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Empty(map.Layers[0].Tiles);
            Assert.Single(map.Layers[1].Tiles);
            Assert.Equal(48, map.TileSizePixels);
            Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);

            canvas.PerformUndo();
            Assert.Single(canvas.Map!.Layers[0].Tiles);
        });
    }

    [Fact]
    public void EraserChrome_ShowsGommeHint()
    {
        StaTestRunner.Run(() =>
        {
            var tools = new EditorLeftToolsWpf();
            Assert.Equal("Gomme", tools.EraserButtonTextForTest);
            tools.SetSelectedTool(EditorTool.Eraser);
            Assert.Contains("Gomme (E)", tools.DrawToolHintForTest, StringComparison.Ordinal);
            Assert.Contains("couche active visible et déverrouillée", tools.DrawToolHintForTest, StringComparison.Ordinal);
        });
    }

    private static MapCanvas CreateCanvas()
    {
        var canvas = new MapCanvas { TileSize = 32 };
        canvas.Map = DemoMapFactory.CreateStarter("Gomme", 8, 6);
        canvas.ActiveTilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
        canvas.SelectedSrc = new System.Drawing.Point(0, 0);
        canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
        canvas.SelectedTileType = TileType.Ground;
        canvas.ActiveLayerIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Ground);
        canvas.ActiveTool = EditorTool.Eraser;
        return canvas;
    }

    private static Tile Sheet(int tilesetId, int srcX) => new()
    {
        Type = TileType.Ground,
        TilesetId = tilesetId,
        SrcX = srcX,
    };

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
