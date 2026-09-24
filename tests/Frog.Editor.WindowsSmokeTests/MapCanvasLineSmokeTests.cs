using System.Linq;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasLineSmokeTests
{
    [Fact]
    public void LineTool_PaintsBresenhamTiles_PreviewThenUndoRedo()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            var ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);

            Assert.True(canvas.TryBeginLineDragForTest(0, 0));
            Assert.Equal(EditorTool.Line, canvas.ActiveTool);
            Assert.Empty(ground.Tiles);

            canvas.SetHoverTileForTest(4, 2);
            Assert.Equal(MapEditOperations.EnumerateLine(0, 0, 4, 2), canvas.GetLinePreviewCellsForTest(axisAligned: false));
            Assert.Empty(ground.Tiles);

            Assert.True(canvas.TryCommitLineDragForTest(4, 2));
            ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);
            var expected = MapEditOperations.EnumerateLine(0, 0, 4, 2);
            Assert.Equal(expected.Count, ground.Tiles.Count);
            foreach (var (x, y) in expected)
            {
                Assert.Contains(ground.Tiles, t => t.X == x && t.Y == y);
            }

            Assert.DoesNotContain(ground.Tiles, t => t.X == 0 && t.Y == 2);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 4 && t.Y == 0);
            Assert.All(ground.Tiles, t => Assert.Equal(TileType.Ground, t.Type));
            Assert.Empty(canvas.GetLinePreviewCellsForTest(axisAligned: false));
            Assert.True(canvas.History.CanUndo);

            canvas.PerformUndo();
            ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);
            Assert.Empty(ground.Tiles);

            Assert.True(canvas.History.CanRedo);
            canvas.PerformRedo();
            ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);
            Assert.Contains(ground.Tiles, t => t.X == 0 && t.Y == 0);
            Assert.Contains(ground.Tiles, t => t.X == 4 && t.Y == 2);
            Assert.Equal(5, ground.Tiles.Count);
        });
    }

    [Fact]
    public void LineTool_ShiftConstrainsToDominantAxis()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();

            Assert.True(canvas.TryBeginLineDragForTest(1, 1));
            canvas.SetHoverTileForTest(5, 3);
            Assert.Equal(MapEditOperations.EnumerateLine(1, 1, 5, 1), canvas.GetLinePreviewCellsForTest(axisAligned: true));

            Assert.True(canvas.TryCommitLineDragForTest(5, 3, axisAligned: true));
            var ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);
            Assert.Equal(5, ground.Tiles.Count);
            Assert.All(ground.Tiles, t => Assert.Equal(1, t.Y));
            Assert.DoesNotContain(ground.Tiles, t => t.Y != 1);

            canvas.PerformUndo();
            ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);
            Assert.Empty(ground.Tiles);
        });
    }

    [Fact]
    public void LineTool_LockedLayer_DoesNotPaint()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = CreateCanvas();
            canvas.Map!.Layers[canvas.ActiveLayerIndex].Locked = true;

            Assert.False(canvas.TryBeginLineDragForTest(1, 1));
            Assert.Empty(canvas.Map.Layers[canvas.ActiveLayerIndex].Tiles);
            Assert.False(canvas.History.CanUndo);
        });
    }

    private static MapCanvas CreateCanvas()
    {
        var canvas = new MapCanvas { TileSize = 32 };
        canvas.Map = DemoMapFactory.CreateStarter();
        canvas.ActiveTilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
        canvas.SelectedSrc = new System.Drawing.Point(0, 0);
        canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
        canvas.SelectedTileType = TileType.Ground;
        canvas.ActiveLayerIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Ground);
        return canvas;
    }
}
