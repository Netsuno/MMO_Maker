using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor;
using Frog.Editor.Controls;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapCanvasTemplateSmokeTests
{
    [Fact]
    public void StampTemplate_PushesOneUndo_AndPlacesPrefab()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var grass = Asset(5);
            var map = MapFormat.CreateTileAssetMap("Atelier", 8, 6);
            map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            MapEditOperations.PaintTile(map, 0, 1, 1, new Tile
            {
                X = 1,
                Y = 1,
                AssetId = grass,
                Type = TileType.Ground,
            });
            var placements = new List<PrefabPlacement>
            {
                new()
                {
                    PrefabId = BuiltInPrefabCatalog.SofaId,
                    Facing = PrefabFacing.South,
                    TileX = 1,
                    TileY = 1,
                },
            };
            Assert.True(MapStampTemplateOperations.TryCapture(
                map, 1, 1, 2, 1, placements, "Canapé", out var template, out var error), error);

            var canvas = new MapCanvas { TileSize = TileAssetMetrics.TargetTileSizePixels };
            canvas.Map = MapFormat.CreateTileAssetMap("Dest", 8, 6);
            canvas.Map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            canvas.ReplacePrefabPlacements(Array.Empty<PrefabPlacement>());
            Assert.False(canvas.History.CanUndo);

            Assert.True(canvas.TryApplyMapTemplate(template!, 3, 2, out var status, out error), error);
            Assert.Contains("posé en (3, 2)", status, StringComparison.Ordinal);
            Assert.Contains("TileAsset", status, StringComparison.OrdinalIgnoreCase);
            Assert.True(canvas.History.CanUndo);
            Assert.Contains(canvas.Map!.Layers[0].Tiles, tile => tile.X == 3 && tile.Y == 2 && tile.AssetId == grass && tile.TilesetId == 0);
            var sofa = Assert.Single(canvas.PrefabPlacements);
            Assert.Equal(BuiltInPrefabCatalog.SofaId, sofa.PrefabId);
            Assert.Equal((3, 2), (sofa.TileX, sofa.TileY));

            canvas.PerformUndo();
            Assert.DoesNotContain(canvas.Map!.Layers[0].Tiles, tile => tile.X == 3 && tile.Y == 2);
            Assert.Single(canvas.PrefabPlacements);
            Assert.Equal(48, canvas.Map.TileSizePixels);
            Assert.Equal(TileGraphicIdentity.TileAsset, canvas.Map.GraphicIdentity);
            Assert.Equal(11, FrogWireProtocol.Version);
        });
    }

    private static TileAssetId Asset(byte marker)
    {
        var bytes = new byte[TileAssetId.ByteLength];
        bytes[0] = marker;
        bytes[31] = 1;
        return TileAssetId.FromHashBytes(bytes);
    }
}
