using System.Linq;
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
public sealed class MapCanvasFillSmokeTests
{
    [Fact]
    public void FillTool_PaintsRegion_OneUndoStepRestores()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            var groundIndex = canvas.ActiveLayerIndex;
            MapEditOperations.PaintTile(canvas.Map!, groundIndex, 0, 0, Sheet(canvas.ActiveTilesetId, 8));
            MapEditOperations.PaintTile(canvas.Map!, groundIndex, 1, 0, Sheet(canvas.ActiveTilesetId, 8));
            MapEditOperations.PaintTile(canvas.Map!, groundIndex, 2, 0, Sheet(canvas.ActiveTilesetId, 3));

            canvas.ActiveTool = EditorTool.Fill;
            Assert.Contains("Remplissage (F)", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.Contains("4 directions", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.False(canvas.History.CanUndo);

            var changed = canvas.TryFloodFillForTest(0, 0);
            Assert.Equal(2, changed);
            var ground = canvas.Map!.Layers[groundIndex];
            Assert.Equal(0, ground.Tiles.Single(t => t.X == 0 && t.Y == 0).SrcX);
            Assert.Equal(0, ground.Tiles.Single(t => t.X == 1 && t.Y == 0).SrcX);
            Assert.Equal(3, ground.Tiles.Single(t => t.X == 2 && t.Y == 0).SrcX);
            Assert.True(canvas.History.CanUndo);
            Assert.False(canvas.History.CanRedo);

            canvas.PerformUndo();
            ground = canvas.Map!.Layers[groundIndex];
            Assert.Equal(8, ground.Tiles.Single(t => t.X == 0 && t.Y == 0).SrcX);
            Assert.Equal(8, ground.Tiles.Single(t => t.X == 1 && t.Y == 0).SrcX);
            Assert.False(canvas.History.CanUndo);
            Assert.True(canvas.History.CanRedo);

            canvas.PerformRedo();
            ground = canvas.Map!.Layers[groundIndex];
            Assert.Equal(0, ground.Tiles.Single(t => t.X == 0 && t.Y == 0).SrcX);
            Assert.Equal(2, ground.Tiles.Count(t => t.SrcX == 0));
            Assert.False(canvas.History.CanRedo);
        });
    }

    [Fact]
    public void FillTool_LockedOrHiddenLayer_DoesNotPaintOrUndo()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            var map = canvas.Map ?? throw new InvalidOperationException("carte");
            var layer = map.Layers[canvas.ActiveLayerIndex];
            MapEditOperations.PaintTile(map, canvas.ActiveLayerIndex, 1, 1, Sheet(canvas.ActiveTilesetId, 2));
            layer.Locked = true;

            Assert.Equal(0, canvas.TryFloodFillForTest(1, 1));
            Assert.Equal(2, layer.Tiles.Single().SrcX);
            Assert.False(canvas.History.CanUndo);

            layer.Locked = false;
            layer.Visible = false;
            Assert.Equal(0, canvas.TryFloodFillForTest(1, 1));
            Assert.Equal(2, layer.Tiles.Single().SrcX);
            Assert.False(canvas.History.CanUndo);
        });
    }

    [Fact]
    public void FillTool_EmptySelectionAndExplicitErase_ClearRegion()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            var map = canvas.Map ?? throw new InvalidOperationException("carte");
            var index = canvas.ActiveLayerIndex;
            MapEditOperations.PaintTile(map, index, 0, 1, Sheet(canvas.ActiveTilesetId, 5));
            MapEditOperations.PaintTile(map, index, 1, 1, Sheet(canvas.ActiveTilesetId, 5));
            MapEditOperations.PaintTile(map, index, 2, 1, Sheet(canvas.ActiveTilesetId, 6));

            Assert.Equal(2, canvas.TryFloodFillForTest(0, 1, erase: true));
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.DoesNotContain(map.Layers[index].Tiles, t => t.X == 0 && t.Y == 1);
            Assert.Contains(map.Layers[index].Tiles, t => t.X == 2 && t.Y == 1 && t.SrcX == 6);
            canvas.PerformUndo();
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Equal(2, map.Layers[index].Tiles.Count(t => t.SrcX == 5));

            canvas.ActiveTilesetId = 0;
            Assert.Equal(2, canvas.TryFloodFillForTest(0, 1, erase: false));
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Single(map.Layers[index].Tiles);
            Assert.Equal(6, map.Layers[index].Tiles.Single().SrcX);
        });
    }

    [Fact]
    public void FillTool_TileAssetMap_PaintsSelectedAsset_UndoRestores()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
            Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);

            var catalogue = new TileAssetCatalogue();
            catalogue.ImportStraightRgba(Solid(255, 0, 0), 48, 48);
            catalogue.ImportStraightRgba(Solid(0, 0, 255), 48, 48);
            var red = catalogue.Ids[0];
            var blue = catalogue.Ids[1];

            var canvas = new MapCanvas { TileSize = 48, TileAssets = catalogue, ActiveTileAssetId = blue };
            var map = MapFormat.CreateTileAssetMap("fill-v6", 3, 2);
            map.Layers.Add(new Layer { LayerType = LayerType.Ground, Visible = true });
            canvas.Map = map;
            canvas.ActiveLayerIndex = 0;
            MapEditOperations.PaintTile(map, 0, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });
            MapEditOperations.PaintTile(map, 0, 1, 0, new Tile { AssetId = red, Type = TileType.Ground });
            MapEditOperations.PaintTile(map, 0, 2, 0, new Tile { AssetId = blue, Type = TileType.Ground });

            Assert.Equal(2, canvas.TryFloodFillForTest(0, 0));
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Equal(blue, map.Layers[0].Tiles.Single(t => t.X == 0).AssetId);
            Assert.Equal(blue, map.Layers[0].Tiles.Single(t => t.X == 1).AssetId);
            Assert.All(map.Layers[0].Tiles, t => Assert.Equal(0, t.TilesetId));

            canvas.PerformUndo();
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Equal(red, map.Layers[0].Tiles.Single(t => t.X == 0).AssetId);
            Assert.Equal(red, map.Layers[0].Tiles.Single(t => t.X == 1).AssetId);

            canvas.ActiveTileAssetId = default;
            Assert.Equal(2, canvas.TryFloodFillForTest(0, 0, erase: false));
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X == 0 || t.X == 1);
            Assert.Equal(blue, map.Layers[0].Tiles.Single().AssetId);
        });
    }

    [Fact]
    public void FillChrome_ShowsRemplissageAndOptionsDefaultOff()
    {
        StaTestRunner.Run(() =>
        {
            var tools = new EditorLeftToolsWpf();
            Assert.Equal("Remplissage", tools.FillButtonTextForTest);
            Assert.False(tools.FillOptionsVisibleForTest);
            Assert.False(tools.FillVisibleLayersForTest);
            Assert.False(tools.FillRespectAttributesForTest);

            var visible = false;
            var respect = false;
            tools.FillVisibleUnlockedLayersChanged += value => visible = value;
            tools.FillRespectAttributesChanged += value => respect = value;

            tools.SetSelectedTool(EditorTool.Fill);
            Assert.True(tools.FillOptionsVisibleForTest);
            tools.FillVisibleLayersForTest = true;
            tools.FillRespectAttributesForTest = true;
            Assert.True(visible);
            Assert.True(respect);

            tools.SetSelectedTool(EditorTool.Brush);
            Assert.False(tools.FillOptionsVisibleForTest);
            Assert.True(tools.FillVisibleLayersForTest);
        });
    }

    private static MapCanvas CreateCanvas()
    {
        var canvas = new MapCanvas { TileSize = 32 };
        canvas.Map = DemoMapFactory.CreateStarter("Remplissage", 8, 6);
        canvas.ActiveTilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
        canvas.SelectedSrc = new System.Drawing.Point(0, 0);
        canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
        canvas.SelectedTileType = TileType.Ground;
        canvas.ActiveLayerIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Ground);
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
