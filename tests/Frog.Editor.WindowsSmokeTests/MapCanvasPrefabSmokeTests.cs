using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Frog.Editor;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasPrefabSmokeTests
{
    [Fact]
    public void PrefabTool_PlacesWithoutPaintingTiles()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            var groundCount = canvas.Map!.Layers[0].Tiles.Count;
            canvas.SelectedPrefabId = BuiltInPrefabCatalog.SofaId;
            canvas.SelectedPrefabFacing = PrefabFacing.South;

            Assert.True(canvas.TryApplyPrefabToolAtTileForTest(3, 4));
            Assert.Equal(EditorTool.Prefab, canvas.ActiveTool);
            var placed = Assert.Single(canvas.PrefabPlacements);
            Assert.Equal(BuiltInPrefabCatalog.SofaId, placed.PrefabId);
            Assert.Equal(3, placed.TileX);
            Assert.Equal(4, placed.TileY);
            Assert.Equal(groundCount, canvas.Map.Layers[0].Tiles.Count);

            Assert.Equal(1, canvas.TryErasePrefabAtForTest(3, 4));
            Assert.Empty(canvas.PrefabPlacements);
            Assert.Equal(groundCount, canvas.Map.Layers[0].Tiles.Count);
        });
    }

    [Fact]
    public void PrefabTool_RotatedSofaChangesFootprint()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            canvas.SelectedPrefabId = BuiltInPrefabCatalog.SofaId;
            canvas.SelectedPrefabFacing = PrefabFacing.East;
            Assert.True(canvas.TryPlaceSelectedPrefab(1, 1));
            var item = Assert.Single(canvas.PrefabPlacements);
            Assert.Equal(PrefabFacing.East, item.Facing);
            Assert.False(canvas.TryPlaceSelectedPrefab(canvas.Map!.Width - 1, canvas.Map.Height - 1));
        });
    }

    [Fact]
    public void PrefabTool_PipetteAndDragMove()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            canvas.SelectedPrefabId = BuiltInPrefabCatalog.ChestId;
            canvas.SelectedPrefabFacing = PrefabFacing.South;
            Assert.True(canvas.TryPlaceSelectedPrefab(2, 3));

            canvas.SelectedPrefabId = BuiltInPrefabCatalog.SofaId;
            Assert.True(canvas.TryPipettePrefabAt(2, 3));
            Assert.Equal(BuiltInPrefabCatalog.ChestId, canvas.SelectedPrefabId);

            Assert.True(canvas.TryBeginPrefabMoveAt(2, 3));
            Assert.True(canvas.TryMoveDraggingPrefabTo(5, 5));
            canvas.EndPrefabMove();
            var moved = Assert.Single(canvas.PrefabPlacements);
            Assert.Equal(BuiltInPrefabCatalog.ChestId, moved.PrefabId);
            Assert.Equal(5, moved.TileX);
            Assert.Equal(5, moved.TileY);
        });
    }

    [Fact]
    public void PrefabTool_DuplicateLastPlacesCopyBesideSource()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            canvas.SelectedPrefabId = BuiltInPrefabCatalog.SofaId;
            canvas.SelectedPrefabFacing = PrefabFacing.South;
            Assert.True(canvas.TryPlaceSelectedPrefab(2, 4));

            Assert.True(canvas.TryDuplicateLastPrefab(out var copy, out var error), error);
            Assert.Equal(2, canvas.PrefabPlacements.Count);
            Assert.NotNull(copy);
            Assert.Equal(BuiltInPrefabCatalog.SofaId, copy!.PrefabId);
            Assert.Equal(PrefabFacing.South, copy.Facing);
            Assert.Equal(4, copy.TileX);
            Assert.Equal(4, copy.TileY);
            Assert.Equal(2, canvas.PrefabPlacements[0].TileX);
            Assert.Same(copy, canvas.SelectedPrefabPlacement);
        });
    }

    [Fact]
    public void PrefabTool_DuplicateSelectedCopiesThatInstance_SelectsCopy_LeavesUndoUntouched()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            var changes = 0;
            canvas.PrefabPlacementsChanged += () => changes++;

            canvas.SelectedPrefabId = BuiltInPrefabCatalog.ChestId;
            canvas.SelectedPrefabFacing = PrefabFacing.South;
            Assert.True(canvas.TryPlaceSelectedPrefab(1, 2));
            Assert.Same(canvas.PrefabPlacements[0], canvas.SelectedPrefabPlacement);

            canvas.SelectedPrefabId = BuiltInPrefabCatalog.SofaId;
            canvas.SelectedPrefabFacing = PrefabFacing.East;
            Assert.True(canvas.TryPlaceSelectedPrefab(4, 2));
            Assert.Equal(PrefabFacing.East, canvas.SelectedPrefabPlacement!.Facing);

            Assert.True(canvas.TrySelectPrefabAt(1, 2));
            Assert.Equal(BuiltInPrefabCatalog.ChestId, canvas.SelectedPrefabPlacement!.PrefabId);
            Assert.False(canvas.History.CanUndo);

            Assert.True(canvas.TryDuplicateSelectedPrefab(out var copy, out var error), error);
            Assert.False(canvas.History.CanUndo);
            Assert.Equal(3, canvas.PrefabPlacements.Count);
            Assert.NotNull(copy);
            Assert.Equal(BuiltInPrefabCatalog.ChestId, copy!.PrefabId);
            Assert.Equal(PrefabFacing.South, copy.Facing);
            Assert.Equal(2, copy.TileX);
            Assert.Equal(2, copy.TileY);
            Assert.Same(copy, canvas.SelectedPrefabPlacement);
            Assert.Equal(1, canvas.PrefabPlacements[0].TileX);
            Assert.Equal(2, canvas.PrefabPlacements[0].TileY);
            Assert.Equal(BuiltInPrefabCatalog.SofaId, canvas.PrefabPlacements[1].PrefabId);
            Assert.Equal(4, canvas.PrefabPlacements[1].TileX);
            Assert.Equal(PrefabFacing.East, canvas.PrefabPlacements[1].Facing);
            Assert.True(changes >= 3);

            Assert.False(canvas.HandleEditorShortcuts(Keys.Control | Keys.Shift | Keys.D));
            Assert.Equal(3, canvas.PrefabPlacements.Count);

            Assert.True(canvas.HandleEditorShortcuts(Keys.Control | Keys.D));
            Assert.Equal(4, canvas.PrefabPlacements.Count);
            Assert.Equal(BuiltInPrefabCatalog.ChestId, canvas.SelectedPrefabPlacement!.PrefabId);
            Assert.Equal(3, canvas.SelectedPrefabPlacement.TileX);
            Assert.Equal(2, canvas.SelectedPrefabPlacement.TileY);

            canvas.ReplacePrefabPlacements(canvas.PrefabPlacements);
            Assert.Null(canvas.SelectedPrefabPlacement);
            Assert.Equal(4, canvas.PrefabPlacements.Count);
        });
    }
}
