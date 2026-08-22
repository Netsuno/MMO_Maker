using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Controls;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class MapCanvasUndoSmokeTests
{
    [Fact]
    public void MapCanvas_UndoRedo_RestoresPaintedTileBlockWarpLayerAndMapName()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new MapCanvas();
            var map = DemoMapFactory.CreateStarter("Before");
            canvas.Map = map;
            canvas.ActiveLayerIndex = map.Layers.FindIndex(l => l.LayerType == LayerType.Ground);
            canvas.SelectedTileType = TileType.Ground;

            canvas.ApplyEditForTest(c => c.PaintTileForTest(2, 2));
            Assert.Contains(map.Layers[canvas.ActiveLayerIndex].Tiles, t => t.X == 2 && t.Y == 2);

            var attrIndex = map.Layers.FindIndex(l => l.LayerType == LayerType.Attributes);
            canvas.ActiveLayerIndex = attrIndex;
            canvas.SetBlockTileForTest(1, 1);
            var targetId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            canvas.SetWarpTileForTest(3, 3, targetId, 1, 1);

            canvas.SetLayerVisibilityForTest(0, false);
            canvas.SetMapNameForTest("After edits");
            Assert.Equal("After edits", map.Name);

            canvas.PerformUndo();
            Assert.Equal("Before", map.Name);

            canvas.PerformUndo();
            Assert.True(map.Layers[0].Visible);

            canvas.PerformUndo();
            Assert.DoesNotContain(map.Layers[attrIndex].Tiles, t => t.Type == TileType.Warp && t.X == 3);

            canvas.PerformUndo();
            Assert.DoesNotContain(map.Layers[attrIndex].Tiles, t => t.Type == TileType.Block && t.X == 1);

            canvas.PerformUndo();
            Assert.DoesNotContain(map.Layers.First(l => l.LayerType == LayerType.Ground).Tiles, t => t.X == 2 && t.Y == 2);

            Assert.True(canvas.History.CanRedo);
            canvas.PerformRedo();
            Assert.Contains(map.Layers.First(l => l.LayerType == LayerType.Ground).Tiles, t => t.X == 2 && t.Y == 2);
        });
    }

    [Fact]
    public void MapCanvas_LockedLayer_DoesNotAcceptPaint()
    {
        StaTestRunner.Run(() =>
        {
            var canvas = new MapCanvas();
            var map = DemoMapFactory.CreateStarter();
            canvas.Map = map;
            canvas.ActiveLayerIndex = 0;
            map.Layers[0].Locked = true;
            canvas.SelectedTileType = TileType.Ground;

            canvas.ApplyEditForTest(c => c.PaintTileForTest(1, 1));
            Assert.Empty(map.Layers[0].Tiles);
        });
    }
}
