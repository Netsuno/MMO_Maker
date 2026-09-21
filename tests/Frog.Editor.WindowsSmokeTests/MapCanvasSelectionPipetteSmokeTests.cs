using System.Linq;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Editor;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasSelectionPipetteSmokeTests
{
    [Fact]
    public void Selection_RotateInPlace_AndClipboardMirrorPaste()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32 };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
                canvas.ActiveLayerIndex = 0;

                Assert.True(canvas.TryPaintTileForTest(2, 2));
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(3, 2));

                canvas.CommitSelectionForTest(2, 2, 2, 1);
                Assert.True(canvas.HandleEditorShortcuts(Keys.H));
                var mirrored = canvas.Map!.Layers[0];
                Assert.Contains(mirrored.Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 32);
                Assert.Contains(mirrored.Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 0);
                Assert.True(canvas.HandleEditorShortcuts(Keys.H));

                canvas.CommitSelectionForTest(2, 2, 2, 1);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Q));
                var rotated = canvas.GetCommittedSelectionForTest();
                Assert.Equal(new System.Drawing.Rectangle(2, 2, 1, 2), rotated);
                var layer = canvas.Map!.Layers[0];
                Assert.Contains(layer.Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(layer.Tiles, t => t.X == 2 && t.Y == 3 && t.SrcX == 32);
                Assert.DoesNotContain(layer.Tiles, t => t.X == 3 && t.Y == 2);

                canvas.ClearSelection();
                Assert.True(canvas.HandleEditorShortcuts(Keys.V));
                canvas.SetHoverTileForTest(5, 5);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.V));
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 5 && t.Y == 5 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 5 && t.Y == 6 && t.SrcX == 0);
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Pipette_SamplesTilesetAndSwitchesToBrush_AltClickKeepsTool()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            try
            {
                var canvas = new MapCanvas { TileSize = 32 };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
                canvas.SelectedTileType = TileType.Ground;
                canvas.ActiveLayerIndex = 0;
                Assert.True(canvas.TryPaintTileForTest(4, 5));

                canvas.ActiveTilesetId = 0;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                canvas.ActiveTool = EditorTool.Selection;
                canvas.SetHoverTileForTest(4, 5);

                Assert.True(canvas.HandleEditorShortcuts(Keys.I));
                Assert.Equal(EditorTool.Brush, canvas.ActiveTool);
                Assert.Equal(tilesetId, canvas.ActiveTilesetId);
                Assert.Equal(new System.Drawing.Point(32, 0), canvas.SelectedSrc);
                Assert.Equal(new System.Drawing.Size(1, 1), canvas.SelectedStampInTiles);

                canvas.ActiveTool = EditorTool.Fill;
                canvas.ActiveTilesetId = 0;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                var groundCount = canvas.Map!.Layers[0].Tiles.Count;
                Assert.True(canvas.TryHandleAltLeftClickForTest(4, 5));
                Assert.Equal(EditorTool.Fill, canvas.ActiveTool);
                Assert.Equal(tilesetId, canvas.ActiveTilesetId);
                Assert.Equal(new System.Drawing.Point(32, 0), canvas.SelectedSrc);
                Assert.Equal(groundCount, canvas.Map.Layers[0].Tiles.Count);

                Assert.False(canvas.HandleEditorShortcuts(Keys.Control | Keys.I));
                Assert.False(canvas.HandleEditorShortcuts(Keys.Control | Keys.C));
            }
            finally
            {
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void ControlClipboardShortcuts_StillCopyPasteAndDoNotRotate()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32 };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                canvas.ActiveLayerIndex = 0;
                Assert.True(canvas.TryPaintTileForTest(1, 1));
                canvas.CommitSelectionForTest(1, 1, 1, 1);

                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.C));
                Assert.True(EditorTileClipboard.HasContent);
                canvas.SetHoverTileForTest(6, 6);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.V));
                Assert.Contains(canvas.Map!.Layers[0].Tiles, t => t.X == 6 && t.Y == 6);

                Assert.False(EditorToolHotkeys.TryResolve(Keys.Q, out _));
                Assert.False(EditorToolHotkeys.TryResolve(Keys.I, out _));
                Assert.False(EditorToolHotkeys.TryResolve(Keys.Control | Keys.V, out _));
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }
}
