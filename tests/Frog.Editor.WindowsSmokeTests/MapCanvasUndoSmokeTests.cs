using System;
using System.Linq;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Editor;
using Frog.Editor.Controls;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasUndoSmokeTests
{
    [Fact]
    public void MapCanvas_UndoRedo_RestoresPaintedTileBlockWarpLayerAndMapName()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter("Before");
            var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
            canvas.ActiveTilesetId = tilesetId;
            canvas.SelectedSrc = new System.Drawing.Point(0, 0);
            canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
            canvas.SelectedTileType = TileType.Ground;
            canvas.ActiveLayerIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Ground);

            Assert.True(canvas.TryPaintTileForTest(2, 2));
            Assert.Contains(canvas.Map!.Layers[canvas.ActiveLayerIndex].Tiles, t => t.X == 2 && t.Y == 2);

            var attrIndex = canvas.Map.Layers.FindIndex(l => l.LayerType == LayerType.Attributes);
            canvas.ActiveLayerIndex = attrIndex;
            canvas.SetBlockTileForTest(1, 1);
            Assert.Contains(canvas.Map.Layers[attrIndex].Tiles, t => t.Type == TileType.Block && t.X == 1 && t.Y == 1);

            var targetId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            canvas.SetWarpTileForTest(3, 3, targetId, 1, 1);
            Assert.Contains(
                canvas.Map.Layers[attrIndex].Tiles,
                t => t.Type == TileType.Warp && t.X == 3 && t.Y == 3 && t.WarpTargetMapId == targetId);

            canvas.SetLayerVisibilityForTest(0, false);
            Assert.False(canvas.Map.Layers[0].Visible);

            canvas.SetMapNameForTest("After edits");
            Assert.Equal("After edits", canvas.Map.Name);

            Assert.True(canvas.History.CanUndo);

            // 1) undo name
            canvas.PerformUndo();
            Assert.Equal("Before", canvas.Map!.Name);
            Assert.False(canvas.Map.Layers[0].Visible);

            // 2) undo visibility
            canvas.PerformUndo();
            Assert.True(canvas.Map!.Layers[0].Visible);
            attrIndex = canvas.Map.Layers.FindIndex(l => l.LayerType == LayerType.Attributes);
            Assert.Contains(canvas.Map.Layers[attrIndex].Tiles, t => t.Type == TileType.Warp && t.X == 3);

            // 3) undo warp
            canvas.PerformUndo();
            attrIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Attributes);
            Assert.DoesNotContain(canvas.Map.Layers[attrIndex].Tiles, t => t.Type == TileType.Warp && t.X == 3);
            Assert.Contains(canvas.Map.Layers[attrIndex].Tiles, t => t.Type == TileType.Block && t.X == 1);

            // 4) undo block
            canvas.PerformUndo();
            attrIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Attributes);
            Assert.DoesNotContain(canvas.Map.Layers[attrIndex].Tiles, t => t.Type == TileType.Block && t.X == 1);

            // 5) undo paint
            canvas.PerformUndo();
            var ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);
            Assert.DoesNotContain(ground.Tiles, t => t.X == 2 && t.Y == 2);

            Assert.True(canvas.History.CanRedo);

            // redo paint
            canvas.PerformRedo();
            ground = canvas.Map!.Layers.First(l => l.LayerType == LayerType.Ground);
            Assert.Contains(ground.Tiles, t => t.X == 2 && t.Y == 2);

            // redo block
            canvas.PerformRedo();
            attrIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Attributes);
            Assert.Contains(canvas.Map.Layers[attrIndex].Tiles, t => t.Type == TileType.Block && t.X == 1);

            // redo warp
            canvas.PerformRedo();
            attrIndex = canvas.Map!.Layers.FindIndex(l => l.LayerType == LayerType.Attributes);
            Assert.Contains(canvas.Map.Layers[attrIndex].Tiles, t => t.Type == TileType.Warp && t.X == 3);

            // redo visibility
            canvas.PerformRedo();
            Assert.False(canvas.Map!.Layers[0].Visible);

            // redo name
            canvas.PerformRedo();
            Assert.Equal("After edits", canvas.Map!.Name);
        });
    }

    [Fact]
    public void MapCanvas_LockedLayer_DoesNotAcceptPaint()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
            canvas.ActiveTilesetId = tilesetId;
            canvas.SelectedSrc = new System.Drawing.Point(0, 0);
            canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
            canvas.SelectedTileType = TileType.Ground;
            canvas.ActiveLayerIndex = 0;
            canvas.Map!.Layers[0].Locked = true;

            Assert.False(canvas.TryPaintTileForTest(1, 1));
            Assert.Empty(canvas.Map.Layers[0].Tiles);
            Assert.False(canvas.History.CanUndo);
        });
    }
}
