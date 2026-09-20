using System;
using Frog.Application.Maps;
using Frog.Editor;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasSpawnSmokeTests
{
    [Fact]
    public void SpawnTool_SetsTileWithoutPainting()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            var groundCount = canvas.Map!.Layers[0].Tiles.Count;

            Assert.True(canvas.TryApplySpawnToolAtTileForTest(5, 6));
            Assert.Equal(EditorTool.Spawn, canvas.ActiveTool);
            Assert.Equal(new System.Drawing.Point(5, 6), canvas.PlaytestSpawnTile);
            Assert.Equal(groundCount, canvas.Map.Layers[0].Tiles.Count);

            Assert.True(canvas.TrySetPlaytestSpawn(99, 99));
            Assert.Equal(new System.Drawing.Point(canvas.Map.Width - 1, canvas.Map.Height - 1), canvas.PlaytestSpawnTile);

            canvas.ClearPlaytestSpawn();
            Assert.Null(canvas.PlaytestSpawnTile);

            var contextOpened = false;
            canvas.TileContextMenuRequested += _ => contextOpened = true;
            Assert.False(canvas.TryHandleSpawnToolRightClickForTest(2, 3, control: false));
            Assert.False(contextOpened);
            Assert.True(canvas.TryHandleSpawnToolRightClickForTest(2, 3, control: true));
            Assert.True(contextOpened);
        });
    }
}
