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
}
