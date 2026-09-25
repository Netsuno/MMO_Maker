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
public sealed class MapCanvasRectSmokeTests
{
    [Fact]
    public void RectangleTool_PaintsBounds_OneUndoStepRestores()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            var fringe = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Fringe);
            MapEditOperations.PaintTile(canvas.Map, canvas.Map.Layers.IndexOf(fringe), 1, 1, Sheet(canvas.ActiveTilesetId, 9));

            Assert.True(canvas.TryBeginRectangleDragForTest(0, 0));
            Assert.Equal(EditorTool.Rectangle, canvas.ActiveTool);
            canvas.SetHoverTileForTest(2, 1);
            var preview = canvas.GetRectanglePreviewCellsForTest();
            Assert.Equal(6, preview.Count);
            Assert.Contains("Rectangle (R)", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.Contains("plein", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.Contains("3×2", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.Empty(Ground(canvas).Tiles);

            Assert.True(canvas.TryCommitRectangleDragForTest(2, 1));
            var ground = Ground(canvas);
            Assert.Equal(6, ground.Tiles.Count);
            Assert.Contains(ground.Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 0);
            Assert.Contains(ground.Tiles, t => t.X == 2 && t.Y == 1 && t.SrcX == 0);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 3 || t.Y == 2);
            Assert.Equal(9, fringe.Tiles.Single().SrcX);
            Assert.Empty(canvas.GetRectanglePreviewCellsForTest());
            Assert.True(canvas.History.CanUndo);
            Assert.False(canvas.History.CanRedo);

            Assert.True(canvas.TryBeginRectangleDragForTest(4, 4));
            Assert.True(canvas.TryCommitRectangleDragForTest(4, 4));
            Assert.Contains(Ground(canvas).Tiles, t => t.X == 4 && t.Y == 4);

            canvas.PerformUndo();
            ground = Ground(canvas);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 4 && t.Y == 4);
            Assert.Equal(6, ground.Tiles.Count);
            Assert.True(canvas.History.CanUndo);

            canvas.PerformUndo();
            Assert.Empty(Ground(canvas).Tiles);
            Assert.False(canvas.History.CanUndo);
            Assert.True(canvas.History.CanRedo);

            canvas.PerformRedo();
            Assert.Equal(6, Ground(canvas).Tiles.Count);
            Assert.Contains(Ground(canvas).Tiles, t => t.X == 0 && t.Y == 0);
            Assert.DoesNotContain(Ground(canvas).Tiles, t => t.X == 4);
        });
    }

    [Fact]
    public void RectangleTool_OutlineAndShiftLeaveInteriorEmpty()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            Assert.True(canvas.TryBeginRectangleDragForTest(0, 0));
            canvas.SetHoverTileForTest(3, 2);
            var preview = canvas.GetRectanglePreviewCellsForTest(shiftOutline: true);
            Assert.Equal(10, preview.Count);
            Assert.DoesNotContain((1, 1), preview);
            Assert.DoesNotContain((2, 1), preview);
            Assert.Contains((0, 0), preview);
            Assert.Contains((3, 2), preview);

            Assert.True(canvas.TryCommitRectangleDragForTest(3, 2, shiftOutline: true));
            var ground = Ground(canvas);
            Assert.Equal(10, ground.Tiles.Count);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 1 && t.Y == 1);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 2 && t.Y == 1);
            Assert.Contains(ground.Tiles, t => t.X == 0 && t.Y == 0);
            Assert.Contains(ground.Tiles, t => t.X == 3 && t.Y == 2);

            canvas.PerformUndo();
            Assert.Empty(Ground(canvas).Tiles);

            canvas.RectangleOutline = true;
            Assert.Contains("contour", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.True(canvas.TryBeginRectangleDragForTest(0, 0));
            Assert.True(canvas.TryCommitRectangleDragForTest(2, 2));
            ground = Ground(canvas);
            Assert.Equal(8, ground.Tiles.Count);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 1 && t.Y == 1);
            canvas.PerformUndo();
            Assert.Empty(Ground(canvas).Tiles);
            Assert.False(canvas.History.CanUndo);
        });
    }

    [Fact]
    public void RectangleTool_EllipsePaintsInscribedCells_UndoRestores()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            canvas.ActiveTool = EditorTool.Rectangle;
            canvas.RectangleEllipse = true;
            Assert.Contains("ellipse", canvas.GetPaintStatusHint(), StringComparison.Ordinal);

            Assert.True(canvas.TryBeginRectangleDragForTest(0, 0));
            canvas.SetHoverTileForTest(4, 2);
            var expected = MapEditOperations.EnumerateShape(0, 0, 4, 2, new ShapeStampOptions { Ellipse = true });
            Assert.Equal(expected, canvas.GetRectanglePreviewCellsForTest());
            Assert.Contains("ellipse", canvas.GetPaintStatusHint(), StringComparison.Ordinal);

            Assert.True(canvas.TryCommitRectangleDragForTest(4, 2));
            var ground = Ground(canvas);
            Assert.Equal(expected.Count, ground.Tiles.Count);
            Assert.Contains(ground.Tiles, t => t.X == 2 && t.Y == 1);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 0 && t.Y == 0);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 4 && t.Y == 2);

            canvas.PerformUndo();
            Assert.Empty(Ground(canvas).Tiles);
            Assert.False(canvas.History.CanUndo);
        });
    }

    [Fact]
    public void RectangleTool_RepeatsSheetStamp_AndSkipsLockedOrHidden()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            canvas.SelectedStampInTiles = new System.Drawing.Size(2, 2);
            Assert.True(canvas.TryBeginRectangleDragForTest(0, 0));
            Assert.True(canvas.TryCommitRectangleDragForTest(1, 1));
            var ground = Ground(canvas);
            Assert.Equal(4, ground.Tiles.Count);
            Assert.Equal((0, 0), SourceAt(ground, 0, 0));
            Assert.Equal((32, 0), SourceAt(ground, 1, 0));
            Assert.Equal((0, 32), SourceAt(ground, 0, 1));
            Assert.Equal((32, 32), SourceAt(ground, 1, 1));
            canvas.PerformUndo();
            Assert.Empty(Ground(canvas).Tiles);

            var layer = canvas.Map!.Layers[canvas.ActiveLayerIndex];
            layer.Locked = true;
            Assert.False(canvas.TryBeginRectangleDragForTest(1, 1));
            Assert.Empty(layer.Tiles);
            Assert.False(canvas.History.CanUndo);

            layer.Locked = false;
            layer.Visible = false;
            Assert.False(canvas.TryBeginRectangleDragForTest(1, 1));
            Assert.Empty(layer.Tiles);
            Assert.False(canvas.History.CanUndo);
        });
    }

    [Fact]
    public void RectangleTool_EscapeCancelsWithoutUndo()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            Assert.True(canvas.TryBeginRectangleDragForTest(1, 1));
            canvas.SetHoverTileForTest(4, 3);
            Assert.NotEmpty(canvas.GetRectanglePreviewCellsForTest());

            Assert.True(canvas.TryCancelShapeGesture());
            Assert.Empty(canvas.GetRectanglePreviewCellsForTest());
            Assert.Empty(Ground(canvas).Tiles);
            Assert.False(canvas.History.CanUndo);
            Assert.Contains("Rectangle (R)", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
            Assert.DoesNotContain("→", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void RectangleTool_TileAssetMap_PaintsSelectedAsset_UndoRestores()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
            Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);

            var catalogue = new TileAssetCatalogue();
            catalogue.ImportStraightRgba(Solid(20, 40, 60), 48, 48);
            catalogue.ImportStraightRgba(Solid(200, 10, 10), 48, 48);
            var red = catalogue.Ids[1];

            var canvas = new MapCanvas { TileSize = 48, TileAssets = catalogue, ActiveTileAssetId = red };
            var map = MapFormat.CreateTileAssetMap("rect-v6", 4, 3);
            map.Layers.Add(new Layer { LayerType = LayerType.Ground, Visible = true });
            map.Layers.Add(new Layer { LayerType = LayerType.Fringe, Visible = true });
            canvas.Map = map;
            canvas.ActiveLayerIndex = 0;
            MapEditOperations.PaintTile(map, 1, 0, 0, new Tile { AssetId = catalogue.Ids[0], Type = TileType.Ground });

            Assert.True(canvas.TryBeginRectangleDragForTest(0, 0));
            Assert.True(canvas.TryCommitRectangleDragForTest(2, 1));
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Equal(6, map.Layers[0].Tiles.Count);
            Assert.All(map.Layers[0].Tiles, t =>
            {
                Assert.Equal(red, t.AssetId);
                Assert.Equal(0, t.TilesetId);
            });
            Assert.Equal(catalogue.Ids[0], map.Layers[1].Tiles.Single().AssetId);
            Assert.Equal(48, map.TileSizePixels);

            canvas.PerformUndo();
            map = canvas.Map ?? throw new InvalidOperationException("carte");
            Assert.Empty(map.Layers[0].Tiles);
            Assert.Single(map.Layers[1].Tiles);
        });
    }

    [Fact]
    public void RectangleChrome_ShowsContourAndEllipse_FillStaysSeparate()
    {
        StaTestRunner.Run(() =>
        {
            var tools = new EditorLeftToolsWpf();
            Assert.Equal("Rectangle", tools.RectangleButtonTextForTest);
            Assert.False(tools.RectangleOptionsVisibleForTest);
            Assert.False(tools.RectangleOutlineForTest);
            Assert.False(tools.RectangleEllipseForTest);

            var outline = false;
            var ellipse = false;
            tools.RectangleOutlineChanged += value => outline = value;
            tools.RectangleEllipseChanged += value => ellipse = value;

            tools.SetSelectedTool(EditorTool.Rectangle);
            Assert.True(tools.RectangleOptionsVisibleForTest);
            Assert.False(tools.FillOptionsVisibleForTest);
            tools.RectangleOutlineForTest = true;
            tools.RectangleEllipseForTest = true;
            Assert.True(outline);
            Assert.True(ellipse);

            tools.SetSelectedTool(EditorTool.Fill);
            Assert.False(tools.RectangleOptionsVisibleForTest);
            Assert.True(tools.FillOptionsVisibleForTest);
            Assert.True(tools.RectangleOutlineForTest);

            tools.SetSelectedTool(EditorTool.Brush);
            Assert.False(tools.FillOptionsVisibleForTest);
            Assert.False(tools.RectangleOptionsVisibleForTest);
        });
    }

    private static Layer Ground(MapCanvas canvas)
        => canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);

    private static (int SrcX, int SrcY) SourceAt(Layer layer, int x, int y)
    {
        var tile = layer.Tiles.Single(t => t.X == x && t.Y == y);
        return (tile.SrcX, tile.SrcY);
    }

    private static MapCanvas CreateCanvas()
    {
        var canvas = new MapCanvas { TileSize = 32 };
        canvas.Map = DemoMapFactory.CreateStarter("Rectangle", 8, 6);
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
