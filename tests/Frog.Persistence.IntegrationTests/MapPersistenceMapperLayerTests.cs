using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// Deux couches du même <see cref="LayerType"/> (Sol + Tombe, toutes deux Ground)
/// ne doivent pas fusionner : le boot sérialise la carte publiée et refuse les tuiles superposées.
/// </summary>
public sealed class MapPersistenceMapperLayerTests
{
    [Fact]
    public void RoundTrip_KeepsTwoGroundLayers_AndTwoFringeLayers_OnTheSameCell()
    {
        var map = new Map { Name = "Carte démo", Width = 20, Height = 10 };
        var sol = new Layer { LayerType = LayerType.Ground, DisplayName = "Sol" };
        sol.Tiles.Add(Tile(15, 4, srcX: 1));
        sol.Tiles.Add(Tile(1, 1, srcX: 8));
        var tombe = new Layer { LayerType = LayerType.Ground, DisplayName = "Tombe" };
        tombe.Tiles.Add(Tile(15, 4, srcX: 2));
        tombe.Tiles.Add(Tile(3, 3, srcX: 9));
        var fringeA = new Layer { LayerType = LayerType.Fringe, DisplayName = "Frange A" };
        fringeA.Tiles.Add(Tile(15, 4, srcX: 3));
        var fringeB = new Layer { LayerType = LayerType.Fringe, DisplayName = "Frange B" };
        fringeB.Tiles.Add(Tile(15, 4, srcX: 4));
        var empty = new Layer { LayerType = LayerType.Mask, DisplayName = "Vide", Visible = false, Locked = true };
        map.Layers.Add(sol);
        map.Layers.Add(tombe);
        map.Layers.Add(fringeA);
        map.Layers.Add(fringeB);
        map.Layers.Add(empty);

        var entity = MapPersistenceMapper.ToEntity(map, DateTimeOffset.UtcNow);
        Assert.Contains("layerIndex", entity.Cells.Single(c => c.X == 15 && c.Y == 4).LayersJson, StringComparison.Ordinal);

        var loaded = MapPersistenceMapper.ToDomain(entity);
        AssertDistinctLayers(loaded);

        var bytes = new MapSerializer().Serialize(loaded);
        var fromBlob = new MapSerializer().Deserialize(bytes);
        Assert.Equal(5, fromBlob.Layers.Count);
        Assert.Equal("Tombe", fromBlob.Layers[1].DisplayName);
        Assert.Equal(2, fromBlob.Layers[1].Tiles.Single(t => t.X == 15 && t.Y == 4).SrcX);
    }

    [Fact]
    public void LegacyCellJson_WithoutLayerIndex_SplitsSameTypePayloadsByCatalogOrder()
    {
        var entity = new MapEntity
        {
            Name = "Carte démo",
            Width = 20,
            Height = 10,
            LayersCatalogJson =
                """
                [{"layerType":0,"displayName":"Sol","visible":true,"locked":false},{"layerType":0,"displayName":"Tombe","visible":true,"locked":false},{"layerType":3,"displayName":"Frange A","visible":true,"locked":false},{"layerType":3,"displayName":"Frange B","visible":true,"locked":false}]
                """,
            Cells =
            [
                new MapCellEntity
                {
                    X = 15,
                    Y = 4,
                    LayersJson =
                        """
                        [{"layerType":0,"tileType":0,"tilesetId":1,"srcX":1,"srcY":0},{"layerType":0,"tileType":0,"tilesetId":1,"srcX":2,"srcY":0},{"layerType":3,"tileType":0,"tilesetId":1,"srcX":3,"srcY":0},{"layerType":3,"tileType":0,"tilesetId":1,"srcX":4,"srcY":0}]
                        """,
                },
                new MapCellEntity
                {
                    X = 3,
                    Y = 3,
                    LayersJson =
                        """
                        [{"layerType":0,"tileType":0,"tilesetId":1,"srcX":9,"srcY":0}]
                        """,
                },
            ],
        };

        var loaded = MapPersistenceMapper.ToDomain(entity);
        Assert.True(loaded.Validate(out var error), error);
        Assert.Equal("Sol", loaded.Layers[0].DisplayName);
        Assert.Equal("Tombe", loaded.Layers[1].DisplayName);
        Assert.Equal(1, loaded.Layers[0].Tiles.Single(t => t.X == 15 && t.Y == 4).SrcX);
        Assert.Equal(2, loaded.Layers[1].Tiles.Single(t => t.X == 15 && t.Y == 4).SrcX);
        Assert.Equal(3, loaded.Layers[2].Tiles.Single(t => t.X == 15 && t.Y == 4).SrcX);
        Assert.Equal(4, loaded.Layers[3].Tiles.Single(t => t.X == 15 && t.Y == 4).SrcX);
        // Sans layerIndex, une seule charge utile Ground va à la première couche Ground du catalogue.
        Assert.Equal(9, loaded.Layers[0].Tiles.Single(t => t.X == 3 && t.Y == 3).SrcX);
        _ = new MapSerializer().Serialize(loaded);
    }

    private static void AssertDistinctLayers(Map loaded)
    {
        Assert.True(loaded.Validate(out var error), error);
        Assert.Equal(5, loaded.Layers.Count);
        Assert.Equal("Sol", loaded.Layers[0].DisplayName);
        Assert.Equal("Tombe", loaded.Layers[1].DisplayName);
        Assert.Equal(LayerType.Ground, loaded.Layers[0].LayerType);
        Assert.Equal(LayerType.Ground, loaded.Layers[1].LayerType);
        Assert.Equal(1, loaded.Layers[0].Tiles.Single(t => t.X == 15 && t.Y == 4).SrcX);
        Assert.Equal(8, loaded.Layers[0].Tiles.Single(t => t.X == 1 && t.Y == 1).SrcX);
        Assert.Equal(2, loaded.Layers[1].Tiles.Single(t => t.X == 15 && t.Y == 4).SrcX);
        Assert.Equal(9, loaded.Layers[1].Tiles.Single(t => t.X == 3 && t.Y == 3).SrcX);
        Assert.Equal("Frange A", loaded.Layers[2].DisplayName);
        Assert.Equal("Frange B", loaded.Layers[3].DisplayName);
        Assert.Equal(3, loaded.Layers[2].Tiles.Single().SrcX);
        Assert.Equal(4, loaded.Layers[3].Tiles.Single().SrcX);
        Assert.Equal("Vide", loaded.Layers[4].DisplayName);
        Assert.Empty(loaded.Layers[4].Tiles);
        Assert.False(loaded.Layers[4].Visible);
        Assert.True(loaded.Layers[4].Locked);
    }

    private static Tile Tile(int x, int y, int srcX) => new()
    {
        X = x,
        Y = y,
        Type = TileType.Ground,
        TilesetId = 1,
        SrcX = srcX,
    };
}
