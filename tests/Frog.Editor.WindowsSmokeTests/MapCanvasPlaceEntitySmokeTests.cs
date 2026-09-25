using Frog.Application.Maps;
using Frog.Editor;
using Frog.Editor.Config;
using Frog.Editor.Controls;
using Frog.Editor.Enums;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasPlaceEntitySmokeTests
{
    [Fact]
    public void PlaceTool_DropsSpawnNpcAndObject_WithoutPainting()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            var groundCount = canvas.Map!.Layers[0].Tiles.Count;

            canvas.PlaceKind = MapPlacedKind.Spawn;
            Assert.True(canvas.TryApplyPlaceToolAtTileForTest(2, 2));
            canvas.PlaceKind = MapPlacedKind.Npc;
            Assert.True(canvas.TryApplyPlaceToolAtTileForTest(3, 2));
            canvas.PlaceKind = MapPlacedKind.Object;
            Assert.True(canvas.TryApplyPlaceToolAtTileForTest(4, 2));

            Assert.Equal(EditorTool.Place, canvas.ActiveTool);
            Assert.Equal(3, canvas.PlacedEntities.Count);
            Assert.Equal(groundCount, canvas.Map.Layers[0].Tiles.Count);
            Assert.Equal(MapPlacedKind.Object, canvas.SelectedPlacedEntity!.Kind);
            Assert.Equal("Objet 1", canvas.SelectedPlacedEntity.Name);

            Assert.True(canvas.TryApplyPlaceToolAtTileForTest(3, 2));
            Assert.Equal(3, canvas.PlacedEntities.Count);
            Assert.Equal(MapPlacedKind.Npc, canvas.SelectedPlacedEntity!.Kind);

            Assert.True(canvas.TryUpdateSelectedPlacedEntity(
                MapPlacedKind.Npc,
                "Garde",
                "Près de la porte",
                MapPlacedFacing.West,
                0,
                4,
                out var error));
            Assert.Null(error);
            Assert.Equal("Garde", canvas.SelectedPlacedEntity!.Name);
            Assert.Equal(MapPlacedFacing.West, canvas.SelectedPlacedEntity.Facing);
            Assert.Equal(groundCount, canvas.Map.Layers[0].Tiles.Count);

            Assert.False(canvas.TryUpdateSelectedPlacedEntity(
                MapPlacedKind.Npc,
                "",
                "",
                MapPlacedFacing.South,
                0,
                1,
                out error));
            Assert.False(string.IsNullOrEmpty(error));
            Assert.Equal("Garde", canvas.SelectedPlacedEntity.Name);

            var npcId = canvas.SelectedPlacedEntity.Id;
            Assert.True(canvas.TryMovePlacedEntityForTest(npcId, 5, 5));
            Assert.Equal((5, 5), (canvas.SelectedPlacedEntity.TileX, canvas.SelectedPlacedEntity.TileY));
            Assert.False(canvas.TryMovePlacedEntityForTest(npcId, 2, 2));

            Assert.True(canvas.TryHandlePlaceToolRightClickForTest(4, 2, control: false));
            Assert.Equal(2, canvas.PlacedEntities.Count);
            Assert.Equal(groundCount, canvas.Map.Layers[0].Tiles.Count);

            var contextOpened = false;
            canvas.TileContextMenuRequested += _ => contextOpened = true;
            Assert.True(canvas.TryHandlePlaceToolRightClickForTest(2, 2, control: true));
            Assert.True(contextOpened);
            Assert.Equal(2, canvas.PlacedEntities.Count);
        });
    }

    [Fact]
    public void PropertiesPanel_FrenchLabels_FollowSelection()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            canvas.PlaceKind = MapPlacedKind.Npc;
            Assert.True(canvas.TryApplyPlaceToolAtTileForTest(1, 1));

            var panel = new MapPlacedEntityPropertiesPanel();
            panel.Sync(canvas.PlacedEntities, canvas.SelectedPlacedEntity, canvas.PlaceKind);
            Assert.Equal("Supprimer", panel.DeleteButtonTextForTest);
            Assert.Contains("PNJ", panel.SelectionTextForTest, StringComparison.Ordinal);
            Assert.Equal("PNJ 1", panel.NameTextForTest);
            Assert.Equal(1, panel.RosterCountForTest);
            Assert.False(panel.RespawnEnabledForTest);
            Assert.True(panel.LevelEnabledForTest);
            Assert.Equal(MapPlacedKind.Npc, panel.KindToPlaceForTest);

            panel.SetKindToPlaceForTest(MapPlacedKind.Spawn);
            Assert.Equal(MapPlacedKind.Spawn, panel.KindToPlaceForTest);
        });
    }
}
