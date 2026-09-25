using System;
using System.IO;
using System.Linq;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapStampTemplateTests
{
    [Fact]
    public void CaptureSelection_StoresTileAssetId_NotPalettePosition()
    {
        var grass = Asset(4);
        var wall = Asset(9);
        var map = TileMap();
        Paint(map, 0, 1, 1, grass);
        Paint(map, 0, 2, 1, grass);
        Paint(map, 1, 1, 2, wall);
        var sofa = new PrefabPlacement
        {
            PrefabId = BuiltInPrefabCatalog.SofaId,
            Facing = PrefabFacing.East,
            TileX = 2,
            TileY = 1,
        };
        var outside = new PrefabPlacement
        {
            PrefabId = BuiltInPrefabCatalog.ChairId,
            Facing = PrefabFacing.South,
            TileX = 0,
            TileY = 0,
        };

        Assert.True(MapStampTemplateOperations.TryCapture(
            map, 1, 1, 2, 2, new[] { sofa, outside }, "Maison", out var template, out var error), error);
        Assert.NotNull(template);
        Assert.Equal(2, template!.Width);
        Assert.Equal(2, template.Height);
        Assert.Equal(TileGraphicIdentity.TileAsset, template.GraphicIdentity);
        Assert.Equal(48, template.TileSizePixels);
        Assert.Equal(2, template.Layers[0].Tiles.Count);
        Assert.Contains(template.Layers[0].Tiles, tile => tile.X == 0 && tile.Y == 0 && tile.AssetId == grass.ToHex());
        Assert.Contains(template.Layers[1].Tiles, tile => tile.X == 0 && tile.Y == 1 && tile.AssetId == wall.ToHex());
        Assert.All(template.Layers.SelectMany(layer => layer.Tiles), tile =>
        {
            Assert.Equal(0, tile.TilesetId);
            Assert.Equal(0, tile.SrcX);
            Assert.Equal(0, tile.SrcY);
        });
        var prefab = Assert.Single(template.Prefabs);
        Assert.Equal(BuiltInPrefabCatalog.SofaId, prefab.PrefabId);
        Assert.Equal(PrefabFacing.East, prefab.Facing);
        Assert.Equal((1, 0), (prefab.TileX, prefab.TileY));
        Assert.Contains("TileAsset (v6, 48 px)", MapStampTemplateOperations.FormatSaved(template), StringComparison.Ordinal);
    }

    [Fact]
    public void CaptureWholeMap_ThenStampAtOriginAndCursor_OneUndoAndHoles()
    {
        var floor = Asset(3);
        var map = TileMap();
        Paint(map, 0, 0, 0, floor);
        Paint(map, 0, 1, 0, floor);
        map.Layers[0].Tiles.Add(new Tile
        {
            X = 1,
            Y = 1,
            Type = TileType.Block,
            Attributes = { new BlockAttribute() },
        });

        Assert.True(MapStampTemplateOperations.TryCapture(
            map, 0, 0, map.Width, map.Height, null, "Pièce", out var whole, out var error), error);

        var selection = MapStampTemplateOperations.TryCapture(
            map, 0, 0, 2, 2, null, "Coin", out var corner, out error);
        Assert.True(selection, error);
        Assert.Equal(3, corner!.Layers[0].Tiles.Count);
        Assert.Contains(corner.Layers[0].Tiles, tile => tile.X == 1 && tile.Y == 1 && tile.Block);

        var dest = TileMap();
        Paint(dest, 0, 3, 3, Asset(8));
        Paint(dest, 0, 4, 2, Asset(8));
        Paint(dest, 0, 5, 3, Asset(8));
        var undoCalls = 0;
        var stamped = MapStampTemplateOperations.ApplyTiles(dest, corner, 3, 2, () => undoCalls++);
        Assert.Equal(1, undoCalls);
        Assert.Equal(3, stamped.Painted);
        Assert.Equal(1, stamped.Cleared);
        Assert.Equal(floor, Find(dest, 0, 3, 2)!.AssetId);
        Assert.Equal(floor, Find(dest, 0, 4, 2)!.AssetId);
        Assert.Null(Find(dest, 0, 3, 3));
        var block = Find(dest, 0, 4, 3);
        Assert.NotNull(block);
        Assert.Contains(block!.Attributes, attribute => attribute is BlockAttribute);
        Assert.Equal(Asset(8), Find(dest, 0, 5, 3)!.AssetId);

        var origin = TileMap();
        var atOrigin = MapStampTemplateOperations.ApplyTiles(origin, whole!, 0, 0, () => { });
        Assert.True(atOrigin.Changed);
        Assert.Equal(floor, Find(origin, 0, 0, 0)!.AssetId);
        Assert.Equal(TileGraphicIdentity.TileAsset, origin.GraphicIdentity);
        Assert.Equal(48, origin.TileSizePixels);
    }

    [Fact]
    public void Stamp_RefusesSheetOntoTileAsset_AndTileAssetStays48()
    {
        var sheet = new Map { Name = "Feuille", Width = 4, Height = 4 };
        sheet.Layers.Add(new Layer { LayerType = LayerType.Ground });
        MapEditOperations.PaintTile(sheet, 0, 0, 0, new Tile { X = 0, Y = 0, TilesetId = 2, SrcX = 32, SrcY = 0, Type = TileType.Ground });
        Assert.True(MapStampTemplateOperations.TryCapture(sheet, 0, 0, 1, 1, null, "Ancien", out var sheetTemplate, out var error), error);
        Assert.NotNull(sheetTemplate);
        Assert.Equal(32, Assert.Single(sheetTemplate.Layers[0].Tiles).SrcX);
        Assert.Equal(TileGraphicIdentity.SheetSource, sheetTemplate.GraphicIdentity);

        var tileMap = TileMap();
        Assert.False(MapStampTemplateOperations.TryValidateForStamp(tileMap, sheetTemplate, 0, 0, out error));
        Assert.Contains("Feuille", error, StringComparison.Ordinal);
        Assert.Contains("TileAsset", error, StringComparison.Ordinal);
        Assert.Empty(tileMap.Layers[0].Tiles);

        var broken = TileMap();
        broken.TileSizePixels = 32;
        Paint(broken, 0, 0, 0, Asset(1));
        Assert.False(MapStampTemplateOperations.TryCapture(broken, 0, 0, 1, 1, null, "Trop petit", out _, out error));
        Assert.Contains("48", error, StringComparison.Ordinal);

        Assert.Equal(11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(6, MapFormat.CurrentWriteVersion);
    }

    [Fact]
    public void LockedLayer_IsSkipped_AndLibraryRoundTripKeepsId()
    {
        var grass = Asset(6);
        var map = TileMap();
        Paint(map, 0, 0, 0, grass);
        Paint(map, 1, 0, 0, Asset(7));
        Assert.True(MapStampTemplateOperations.TryCapture(map, 0, 0, 1, 1, null, "Mur", out var template, out var error), error);

        var dest = TileMap();
        Paint(dest, 1, 2, 2, Asset(2));
        dest.Layers[1].Locked = true;
        var result = MapStampTemplateOperations.ApplyTiles(dest, template!, 2, 2, () => { });
        Assert.Equal(1, result.Painted);
        Assert.Equal(1, result.LayersSkipped);
        Assert.Equal(grass, Find(dest, 0, 2, 2)!.AssetId);
        Assert.Equal(Asset(2), Find(dest, 1, 2, 2)!.AssetId);

        var directory = Path.Combine(Path.GetTempPath(), "frog-templates-" + Guid.NewGuid().ToString("N"));
        var library = new MapStampTemplateLibrary();
        var id = template!.Id;
        Assert.True(library.TryUpsert(template, out error), error);
        template.Name = "mur";
        Assert.True(library.TryUpsert(template, out error), error);
        Assert.Single(library.Templates);
        Assert.Equal(id, library.Templates[0].Id);
        Assert.True(library.TryWrite(directory, out error), error);
        var json = File.ReadAllText(Path.Combine(directory, MapStampTemplateLibrary.FileName));
        Assert.Contains(grass.ToHex(), json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"srcX\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"tilesetId\"", json, StringComparison.Ordinal);

        Assert.True(MapStampTemplateLibrary.TryLoad(directory, out var loaded, out error), error);
        var restored = Assert.Single(loaded.Templates);
        Assert.Equal(id, restored.Id);
        Assert.Equal(grass.ToHex(), restored.Layers[0].Tiles[0].AssetId);

        File.WriteAllText(Path.Combine(directory, MapStampTemplateLibrary.FileName), "{ \"version\": 2 }");
        Assert.False(MapStampTemplateLibrary.TryLoad(directory, out _, out error));
        Assert.Contains("Version", error, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(directory, MapStampTemplateLibrary.FileName), "{");
        Assert.False(MapStampTemplateLibrary.TryLoad(directory, out _, out error));
        Assert.Contains("illisible", error, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyRectangle_AndFullList_AreRefused()
    {
        var map = TileMap();
        Assert.False(MapStampTemplateOperations.TryCapture(map, 0, 0, 2, 2, null, "Vide", out _, out var error));
        Assert.Contains("Rien à enregistrer", error, StringComparison.Ordinal);

        var library = new MapStampTemplateLibrary();
        for (var i = 0; i < MapStampTemplateLibrary.MaxTemplates; i++)
        {
            Paint(map, 0, 0, 0, Asset((byte)(i + 1)));
            Assert.True(MapStampTemplateOperations.TryCapture(map, 0, 0, 1, 1, null, "M" + i, out var template, out error), error);
            Assert.True(library.TryUpsert(template!, out error), error);
        }

        Paint(map, 0, 0, 0, Asset(2));
        Assert.True(MapStampTemplateOperations.TryCapture(map, 0, 0, 1, 1, null, "Encore", out var extra, out error), error);
        Assert.False(library.TryUpsert(extra!, out error));
        Assert.Contains("Liste pleine", error, StringComparison.Ordinal);
    }

    private static Map TileMap()
    {
        var map = MapFormat.CreateTileAssetMap("Atelier", 8, 6);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe });
        return map;
    }

    private static void Paint(Map map, int layer, int x, int y, TileAssetId id)
        => MapEditOperations.PaintTile(map, layer, x, y, new Tile { X = x, Y = y, AssetId = id, Type = TileType.Ground });

    private static Tile? Find(Map map, int layer, int x, int y)
        => map.Layers[layer].Tiles.LastOrDefault(tile => tile.X == x && tile.Y == y);

    private static TileAssetId Asset(byte marker)
    {
        var bytes = new byte[TileAssetId.ByteLength];
        bytes[0] = marker;
        bytes[31] = 1;
        return TileAssetId.FromHashBytes(bytes);
    }
}
