using System.Linq;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
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

    [Fact]
    public void Selection_MultiLayerCopyPaste_IsOneUndoAndRedo()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32, ActiveTool = EditorTool.Selection };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);

                canvas.ActiveLayerIndex = 0;
                canvas.SelectedTileType = TileType.Ground;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(0, 0));
                Assert.True(canvas.TryPaintTileForTest(2, 2));
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(3, 2));
                Assert.True(canvas.TryPaintTileForTest(6, 7));

                canvas.ActiveLayerIndex = 1;
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 3));
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(6, 6));

                canvas.ActiveLayerIndex = 2;
                canvas.SelectedTileType = TileType.Block;
                Assert.True(canvas.TryPaintTileForTest(3, 3));
                canvas.Map!.Layers[2].Tiles.Single(t => t.X == 3 && t.Y == 3).Attributes.Add(new BlockAttribute());
                canvas.SelectedTileType = TileType.Ground;
                Assert.True(canvas.TryPaintTileForTest(6, 6));

                canvas.ActiveLayerIndex = 0;
                canvas.CommitSelectionForTest(2, 2, 2, 2);
                var hint = canvas.GetPaintStatusHint();
                Assert.Contains("toutes les couches", hint, StringComparison.Ordinal);
                Assert.Contains("Ctrl+Maj", hint, StringComparison.Ordinal);
                Assert.Contains("2×2", hint, StringComparison.Ordinal);

                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.C));
                Assert.False(EditorTileClipboard.IsSingleLayer);
                Assert.Equal(3, EditorTileClipboard.CapturedLayerCount);

                canvas.SetHoverTileForTest(6, 6);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.V));

                var ground = canvas.Map.Layers[0];
                var fringe = canvas.Map.Layers[1];
                var attributes = canvas.Map.Layers[2];
                Assert.Contains(ground.Tiles, t => t.X == 6 && t.Y == 6 && t.SrcX == 0);
                Assert.Contains(ground.Tiles, t => t.X == 7 && t.Y == 6 && t.SrcX == 32);
                Assert.DoesNotContain(ground.Tiles, t => t.X == 6 && t.Y == 7);
                Assert.Contains(ground.Tiles, t => t.X == 0 && t.Y == 0);
                Assert.Contains(fringe.Tiles, t => t.X == 6 && t.Y == 7 && t.SrcX == 32);
                Assert.DoesNotContain(fringe.Tiles, t => t.X == 6 && t.Y == 6);
                var pastedBlock = Assert.Single(attributes.Tiles, t => t.X == 7 && t.Y == 7);
                Assert.Equal(TileType.Block, pastedBlock.Type);
                Assert.Contains(pastedBlock.Attributes, attribute => attribute is BlockAttribute);
                Assert.DoesNotContain(attributes.Tiles, t => t.X == 6 && t.Y == 6);

                canvas.PerformUndo();
                ground = canvas.Map.Layers[0];
                fringe = canvas.Map.Layers[1];
                attributes = canvas.Map.Layers[2];
                Assert.Contains(ground.Tiles, t => t.X == 6 && t.Y == 7);
                Assert.DoesNotContain(ground.Tiles, t => t.X == 6 && t.Y == 6);
                Assert.DoesNotContain(ground.Tiles, t => t.X == 7 && t.Y == 6);
                Assert.Contains(fringe.Tiles, t => t.X == 6 && t.Y == 6);
                Assert.DoesNotContain(fringe.Tiles, t => t.X == 6 && t.Y == 7);
                Assert.Contains(attributes.Tiles, t => t.X == 6 && t.Y == 6 && t.Type == TileType.Ground);
                Assert.DoesNotContain(attributes.Tiles, t => t.X == 7 && t.Y == 7);
                Assert.Contains(ground.Tiles, t => t.X == 2 && t.Y == 2);
                Assert.Contains(fringe.Tiles, t => t.X == 2 && t.Y == 3);

                canvas.PerformRedo();
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 7 && t.Y == 6 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 6 && t.Y == 7 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[2].Tiles, t => t.X == 7 && t.Y == 7 && t.Type == TileType.Block);
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 6 && t.Y == 7);

                canvas.PerformUndo();
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.X));
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2);
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 3 && t.Y == 2);
                Assert.DoesNotContain(canvas.Map.Layers[1].Tiles, t => t.X == 2 && t.Y == 3);
                Assert.DoesNotContain(canvas.Map.Layers[2].Tiles, t => t.X == 3 && t.Y == 3);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 0 && t.Y == 0);
                canvas.PerformUndo();
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 2 && t.Y == 3);
                Assert.Contains(canvas.Map.Layers[2].Tiles, t => t.X == 3 && t.Y == 3 && t.Type == TileType.Block);
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Selection_ShiftModifier_CopiesAndTransformsActiveLayerOnly()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32, ActiveTool = EditorTool.Selection };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
                canvas.SelectedTileType = TileType.Ground;

                canvas.ActiveLayerIndex = 0;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(1, 1));
                canvas.ActiveLayerIndex = 1;
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(1, 1));

                canvas.ActiveLayerIndex = 0;
                canvas.CommitSelectionForTest(1, 1, 1, 1);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.Shift | Keys.C));
                Assert.True(EditorTileClipboard.IsSingleLayer);
                Assert.Equal(1, EditorTileClipboard.CapturedLayerCount);

                canvas.SetHoverTileForTest(8, 8);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.V));
                Assert.Contains(canvas.Map!.Layers[0].Tiles, t => t.X == 8 && t.Y == 8 && t.SrcX == 0);
                Assert.DoesNotContain(canvas.Map.Layers[1].Tiles, t => t.X == 8 && t.Y == 8);

                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.C));
                Assert.False(EditorTileClipboard.IsSingleLayer);
                canvas.SetHoverTileForTest(4, 4);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.Shift | Keys.V));
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 4 && t.Y == 4 && t.SrcX == 0);
                Assert.DoesNotContain(canvas.Map.Layers[1].Tiles, t => t.X == 4 && t.Y == 4);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 32);

                canvas.ActiveLayerIndex = 0;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 2));
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(3, 2));
                canvas.ActiveLayerIndex = 1;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 2));
                canvas.ActiveLayerIndex = 0;
                canvas.CommitSelectionForTest(2, 2, 2, 1);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Shift | Keys.Q));
                Assert.True(EditorTileClipboard.IsSingleLayer);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 3 && t.SrcX == 32);
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 3 && t.Y == 2);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);
                Assert.DoesNotContain(canvas.Map.Layers[1].Tiles, t => t.X == 2 && t.Y == 3);
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Selection_DragZone_CopyThenPaste_MultiTileRectangle()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32, ActiveTool = EditorTool.Selection };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
                canvas.SelectedTileType = TileType.Ground;

                canvas.ActiveLayerIndex = 0;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(0, 0));
                Assert.True(canvas.TryPaintTileForTest(1, 1));
                Assert.True(canvas.TryPaintTileForTest(1, 2));
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 1));
                Assert.True(canvas.TryPaintTileForTest(9, 5));

                canvas.ActiveLayerIndex = 1;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(8, 4));

                canvas.ActiveLayerIndex = 2;
                canvas.SelectedTileType = TileType.Block;
                Assert.True(canvas.TryPaintTileForTest(2, 1));
                canvas.Map!.Layers[2].Tiles.Single(t => t.X == 2 && t.Y == 1).Attributes.Add(new BlockAttribute());

                canvas.ActiveRegionId = 7;
                canvas.ActiveTool = EditorTool.Region;
                canvas.RaiseMouseDownForTest(MouseButtons.Left, 48, 48);
                Assert.Equal(7, canvas.Map.Regions!.Get(1, 1));

                canvas.ActiveTool = EditorTool.Selection;
                canvas.ActiveLayerIndex = 0;
                canvas.RaiseMouseDownForTest(MouseButtons.Right, 16, 16);
                canvas.RaiseMouseMoveForTest(MouseButtons.Right, 48, 16);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 0 && t.Y == 0 && t.SrcX == 0);

                canvas.RaiseMouseDownForTest(MouseButtons.Left, 80, 80);
                canvas.RaiseMouseMoveForTest(MouseButtons.Left, 48, 48);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.C));
                Assert.Equal(new System.Drawing.Rectangle(1, 1, 2, 2), canvas.GetCommittedSelectionForTest());
                Assert.False(EditorTileClipboard.IsSingleLayer);
                Assert.Equal(3, EditorTileClipboard.CapturedLayerCount);
                Assert.Equal(2, EditorTileClipboard.Width);
                Assert.Equal(2, EditorTileClipboard.Height);

                canvas.RaiseMouseUpForTest(MouseButtons.Left, 48, 48);
                Assert.Equal(new System.Drawing.Rectangle(1, 1, 2, 2), canvas.GetCommittedSelectionForTest());

                canvas.SetHoverTileForTest(8, 4);
                Assert.Equal(new System.Drawing.Rectangle(8, 4, 2, 2), canvas.GetPasteFootprintForTest());
                var hint = canvas.GetPaintStatusHint();
                Assert.Contains("zone copiée", hint, StringComparison.Ordinal);
                Assert.Contains("2×2", hint, StringComparison.Ordinal);
                Assert.Contains("toutes les couches", hint, StringComparison.Ordinal);
                Assert.Contains("Ctrl+V", hint, StringComparison.Ordinal);
                Assert.Contains("Ctrl+Maj", hint, StringComparison.Ordinal);

                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.V));
                var ground = canvas.Map.Layers[0];
                var fringe = canvas.Map.Layers[1];
                var attributes = canvas.Map.Layers[2];
                Assert.Contains(ground.Tiles, t => t.X == 8 && t.Y == 4 && t.SrcX == 0);
                Assert.Contains(ground.Tiles, t => t.X == 9 && t.Y == 4 && t.SrcX == 32);
                Assert.Contains(ground.Tiles, t => t.X == 8 && t.Y == 5 && t.SrcX == 0);
                Assert.DoesNotContain(ground.Tiles, t => t.X == 9 && t.Y == 5);
                Assert.DoesNotContain(fringe.Tiles, t => t.X == 8 && t.Y == 4);
                var pastedBlock = Assert.Single(attributes.Tiles, t => t.X == 9 && t.Y == 4);
                Assert.Equal(TileType.Block, pastedBlock.Type);
                Assert.Contains(pastedBlock.Attributes, attribute => attribute is BlockAttribute);
                Assert.Contains(ground.Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 0);
                Assert.Contains(ground.Tiles, t => t.X == 2 && t.Y == 1 && t.SrcX == 32);
                Assert.Contains(attributes.Tiles, t => t.X == 2 && t.Y == 1 && t.Type == TileType.Block);
                Assert.Equal(7, canvas.Map.Regions.Get(1, 1));
                Assert.Equal(0, canvas.Map.Regions.Get(8, 4));
                Assert.Equal(0, canvas.Map.Regions.Get(9, 4));

                canvas.PerformUndo();
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 9 && t.Y == 5 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 8 && t.Y == 4);
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 8 && t.Y == 4);
                Assert.DoesNotContain(canvas.Map.Layers[2].Tiles, t => t.X == 9 && t.Y == 4);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 1 && t.Y == 1);
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Selection_DragPastMapEdge_ClampsZoneRectangle()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32, ActiveTool = EditorTool.Selection };
                canvas.Map = DemoMapFactory.CreateStarter();
                canvas.RaiseMouseDownForTest(MouseButtons.Left, 48, 48);
                canvas.RaiseMouseMoveForTest(MouseButtons.Left, 680, 72);
                Assert.Equal(new System.Drawing.Point(19, 2), canvas.HoveredTile);
                canvas.RaiseMouseUpForTest(MouseButtons.Left, 680, 72);
                Assert.Equal(new System.Drawing.Rectangle(1, 1, 19, 2), canvas.GetCommittedSelectionForTest());
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Selection_LeftDrag_MovesGroup_OverlapUndoRedo_RightClickOutsideClears()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32, ActiveTool = EditorTool.Selection };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
                canvas.SelectedTileType = TileType.Ground;

                canvas.ActiveLayerIndex = 0;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 2));
                Assert.True(canvas.TryPaintTileForTest(4, 2));
                Assert.True(canvas.TryPaintTileForTest(6, 6));
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(3, 2));

                canvas.ActiveLayerIndex = 1;
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 2));

                canvas.ActiveLayerIndex = 2;
                canvas.SelectedTileType = TileType.Block;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(3, 2));
                canvas.Map!.Layers[2].Tiles.Single(t => t.X == 3 && t.Y == 2).Attributes.Add(new BlockAttribute());
                canvas.SelectedTileType = TileType.Ground;

                canvas.ActiveRegionId = 4;
                canvas.ActiveTool = EditorTool.Region;
                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(2), TilePx(2));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(2), TilePx(2));

                canvas.ActiveTool = EditorTool.Selection;
                canvas.ActiveLayerIndex = 0;
                canvas.CommitSelectionForTest(6, 6, 1, 1);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.C));
                Assert.Equal(1, EditorTileClipboard.Width);

                canvas.CommitSelectionForTest(2, 2, 2, 1);
                var idle = canvas.GetPaintStatusHint();
                Assert.Contains("glisser gauche déplace", idle, StringComparison.Ordinal);
                Assert.Contains("glisser droit copie", idle, StringComparison.Ordinal);

                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(2), TilePx(2));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(2), TilePx(2));
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);
                Assert.Equal(new System.Drawing.Rectangle(2, 2, 2, 1), canvas.GetCommittedSelectionForTest());

                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(2), TilePx(2));
                canvas.RaiseMouseMoveForTest(MouseButtons.Left, TilePx(3), TilePx(2));
                Assert.Contains("déplacer", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(3), TilePx(2));

                var ground = canvas.Map.Layers[0];
                var fringe = canvas.Map.Layers[1];
                var attributes = canvas.Map.Layers[2];
                Assert.DoesNotContain(ground.Tiles, t => t.X == 2 && t.Y == 2);
                Assert.Contains(ground.Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(ground.Tiles, t => t.X == 4 && t.Y == 2 && t.SrcX == 32);
                Assert.DoesNotContain(fringe.Tiles, t => t.X == 2 && t.Y == 2);
                Assert.Contains(fringe.Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 32);
                Assert.DoesNotContain(attributes.Tiles, t => t.X == 3 && t.Y == 2);
                var movedBlock = Assert.Single(attributes.Tiles, t => t.X == 4 && t.Y == 2);
                Assert.Equal(TileType.Block, movedBlock.Type);
                Assert.Contains(movedBlock.Attributes, attribute => attribute is BlockAttribute);
                Assert.Contains(ground.Tiles, t => t.X == 6 && t.Y == 6 && t.SrcX == 0);
                Assert.Equal(4, canvas.Map.Regions!.Get(2, 2));
                Assert.Equal(0, canvas.Map.Regions.Get(3, 2));
                Assert.Equal(new System.Drawing.Rectangle(3, 2, 2, 1), canvas.GetCommittedSelectionForTest());
                Assert.Equal(1, EditorTileClipboard.Width);

                canvas.PerformUndo();
                ground = canvas.Map.Layers[0];
                Assert.Contains(ground.Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(ground.Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 32);
                Assert.Contains(ground.Tiles, t => t.X == 4 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 32);
                Assert.DoesNotContain(canvas.Map.Layers[1].Tiles, t => t.X == 3 && t.Y == 2);
                Assert.Contains(canvas.Map.Layers[2].Tiles, t => t.X == 3 && t.Y == 2 && t.Type == TileType.Block);
                Assert.DoesNotContain(canvas.Map.Layers[2].Tiles, t => t.X == 4 && t.Y == 2);

                canvas.PerformRedo();
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 4 && t.Y == 2 && t.SrcX == 32);
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2);

                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(10), TilePx(10));
                canvas.RaiseMouseMoveForTest(MouseButtons.Left, TilePx(11), TilePx(11));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(11), TilePx(11));
                Assert.Equal(new System.Drawing.Rectangle(10, 10, 2, 2), canvas.GetCommittedSelectionForTest());
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 0);

                canvas.CommitSelectionForTest(3, 2, 2, 1);
                canvas.RaiseMouseDownForTest(MouseButtons.Right, TilePx(0), TilePx(0));
                Assert.Null(canvas.GetCommittedSelectionForTest());
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 4 && t.Y == 2 && t.SrcX == 32);
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Selection_RightDrag_CopiesGroup_SourceStays_UndoIsOneStep()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTileClipboard.Clear();
            try
            {
                var canvas = new MapCanvas { TileSize = 32, ActiveTool = EditorTool.Selection };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
                canvas.SelectedTileType = TileType.Ground;

                canvas.ActiveLayerIndex = 0;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 2));
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(3, 2));
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(8, 4));

                canvas.ActiveLayerIndex = 1;
                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(2, 3));

                canvas.ActiveLayerIndex = 2;
                canvas.SelectedTileType = TileType.Block;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                Assert.True(canvas.TryPaintTileForTest(3, 2));
                canvas.Map!.Layers[2].Tiles.Single(t => t.X == 3 && t.Y == 2).Attributes.Add(new BlockAttribute());

                canvas.ActiveRegionId = 9;
                canvas.ActiveTool = EditorTool.Region;
                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(2), TilePx(2));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(2), TilePx(2));

                canvas.ActiveTool = EditorTool.Selection;
                canvas.ActiveLayerIndex = 0;
                canvas.CommitSelectionForTest(2, 2, 2, 2);
                Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.C));
                var clipboardBefore = EditorTileClipboard.Snapshot().Count;

                canvas.RaiseMouseDownForTest(MouseButtons.Right, TilePx(2), TilePx(2));
                canvas.RaiseMouseMoveForTest(MouseButtons.Right, TilePx(8), TilePx(4));
                Assert.Contains("copier", canvas.GetPaintStatusHint(), StringComparison.Ordinal);
                canvas.RaiseMouseUpForTest(MouseButtons.Right, TilePx(8), TilePx(4));

                var ground = canvas.Map.Layers[0];
                Assert.Contains(ground.Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(ground.Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 32);
                Assert.Contains(ground.Tiles, t => t.X == 8 && t.Y == 4 && t.SrcX == 0);
                Assert.Contains(ground.Tiles, t => t.X == 9 && t.Y == 4 && t.SrcX == 32);
                Assert.DoesNotContain(ground.Tiles, t => t.X == 8 && t.Y == 5);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 2 && t.Y == 3 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 8 && t.Y == 5 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[2].Tiles, t => t.X == 3 && t.Y == 2 && t.Type == TileType.Block);
                var copiedBlock = Assert.Single(canvas.Map.Layers[2].Tiles, t => t.X == 9 && t.Y == 4);
                Assert.Contains(copiedBlock.Attributes, attribute => attribute is BlockAttribute);
                Assert.Equal(9, canvas.Map.Regions!.Get(2, 2));
                Assert.Equal(0, canvas.Map.Regions.Get(8, 4));
                Assert.Equal(new System.Drawing.Rectangle(8, 4, 2, 2), canvas.GetCommittedSelectionForTest());
                Assert.Equal(clipboardBefore, EditorTileClipboard.Snapshot().Count);
                Assert.Equal(2, EditorTileClipboard.Width);

                canvas.PerformUndo();
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 3 && t.Y == 2 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 8 && t.Y == 4 && t.SrcX == 0);
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 9 && t.Y == 4);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 2 && t.Y == 3);
                Assert.DoesNotContain(canvas.Map.Layers[1].Tiles, t => t.X == 8 && t.Y == 5);
                Assert.Contains(canvas.Map.Layers[2].Tiles, t => t.X == 3 && t.Y == 2 && t.Type == TileType.Block);
                Assert.DoesNotContain(canvas.Map.Layers[2].Tiles, t => t.X == 9 && t.Y == 4);

                canvas.PerformRedo();
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 9 && t.Y == 4 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[1].Tiles, t => t.X == 8 && t.Y == 5 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);
            }
            finally
            {
                EditorTileClipboard.Clear();
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void DoubleClick_PicksStamp_UndoAccidentalPaint_BrushDragStillPaints()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            try
            {
                var canvas = new MapCanvas { TileSize = 32, ActiveTool = EditorTool.Brush };
                canvas.Map = DemoMapFactory.CreateStarter();
                var tilesetId = EditorSmokeTestAccess.RegisterMinimalTileset();
                canvas.ActiveTilesetId = tilesetId;
                canvas.SelectedStampInTiles = new System.Drawing.Size(1, 1);
                canvas.SelectedTileType = TileType.Ground;
                canvas.ActiveLayerIndex = 0;

                canvas.SelectedSrc = new System.Drawing.Point(32, 0);
                Assert.True(canvas.TryPaintTileForTest(4, 5));
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);

                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(1), TilePx(1));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(1), TilePx(1));
                Assert.Contains(canvas.Map!.Layers[0].Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 0);

                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(1), TilePx(2));
                canvas.RaiseMouseMoveForTest(MouseButtons.Left, TilePx(2), TilePx(2));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(2), TilePx(2));
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 1 && t.Y == 2 && t.SrcX == 0);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);

                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(4), TilePx(5));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(4), TilePx(5));
                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(4), TilePx(5), clicks: 2);
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(4), TilePx(5));

                Assert.Equal(EditorTool.Brush, canvas.ActiveTool);
                Assert.Equal(tilesetId, canvas.ActiveTilesetId);
                Assert.Equal(new System.Drawing.Point(32, 0), canvas.SelectedSrc);
                Assert.Equal(new System.Drawing.Size(1, 1), canvas.SelectedStampInTiles);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 4 && t.Y == 5 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 0);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 2 && t.Y == 2 && t.SrcX == 0);

                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(7), TilePx(7));
                canvas.RaiseMouseMoveForTest(MouseButtons.Left, TilePx(8), TilePx(7));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(8), TilePx(7));
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 7 && t.Y == 7 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 8 && t.Y == 7 && t.SrcX == 32);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 4 && t.Y == 5 && t.SrcX == 32);

                canvas.ActiveTool = EditorTool.Fill;
                canvas.SelectedSrc = new System.Drawing.Point(0, 0);
                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(4), TilePx(5));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(4), TilePx(5));
                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(4), TilePx(5), clicks: 2);
                Assert.Equal(EditorTool.Fill, canvas.ActiveTool);
                Assert.Equal(new System.Drawing.Point(32, 0), canvas.SelectedSrc);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 4 && t.Y == 5 && t.SrcX == 32);
                Assert.DoesNotContain(canvas.Map.Layers[0].Tiles, t => t.X == 4 && t.Y == 4);

                canvas.ActiveTool = EditorTool.Selection;
                canvas.CommitSelectionForTest(1, 1, 2, 1);
                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(4), TilePx(5));
                canvas.RaiseMouseUpForTest(MouseButtons.Left, TilePx(4), TilePx(5));
                canvas.RaiseMouseDownForTest(MouseButtons.Left, TilePx(4), TilePx(5), clicks: 2);
                Assert.Equal(EditorTool.Selection, canvas.ActiveTool);
                Assert.Equal(new System.Drawing.Rectangle(1, 1, 2, 1), canvas.GetCommittedSelectionForTest());
                Assert.Equal(new System.Drawing.Point(32, 0), canvas.SelectedSrc);
                Assert.Contains(canvas.Map.Layers[0].Tiles, t => t.X == 1 && t.Y == 1 && t.SrcX == 0);
            }
            finally
            {
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    private static int TilePx(int tile) => tile * 32 + 8;
}
