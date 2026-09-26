using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;

using Xunit;

namespace Frog.Tests;

public sealed class AutotileJoinTests
{
    [Fact]
    public void PreferredRole_CoversEveryOrthogonalMask()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal((byte)6, MapSerializer.TileAssetMapFileFormatVersion);
        Assert.Equal(1, TileAssetFlagTable.FormatVersion);

        Assert.Equal(AutotileRole.Isolated, AutotileJoin.PreferredRole(0));
        Assert.Equal(AutotileRole.Center, AutotileJoin.PreferredRole(15));
        Assert.Equal(AutotileRole.North, AutotileJoin.PreferredRole(14));
        Assert.Equal(AutotileRole.East, AutotileJoin.PreferredRole(13));
        Assert.Equal(AutotileRole.South, AutotileJoin.PreferredRole(11));
        Assert.Equal(AutotileRole.West, AutotileJoin.PreferredRole(7));
        Assert.Equal(AutotileRole.NorthEast, AutotileJoin.PreferredRole(12));
        Assert.Equal(AutotileRole.NorthWest, AutotileJoin.PreferredRole(6));
        Assert.Equal(AutotileRole.SouthEast, AutotileJoin.PreferredRole(9));
        Assert.Equal(AutotileRole.SouthWest, AutotileJoin.PreferredRole(3));
        Assert.Equal(AutotileRole.Horizontal, AutotileJoin.PreferredRole(10));
        Assert.Equal(AutotileRole.Vertical, AutotileJoin.PreferredRole(5));
        Assert.Equal(AutotileRole.South, AutotileJoin.PreferredRole(1));
        Assert.Equal(AutotileRole.West, AutotileJoin.PreferredRole(2));
        Assert.Equal(AutotileRole.North, AutotileJoin.PreferredRole(4));
        Assert.Equal(AutotileRole.East, AutotileJoin.PreferredRole(8));
    }

    [Fact]
    public void Paint_ResolvesCorners_AndKeepsTerrainOnTheSameFlags()
    {
        var center = Id(1);
        var northWest = Id(2);
        var northEast = Id(3);
        var southWest = Id(4);
        var southEast = Id(5);
        var isolated = Id(6);
        var sand = Id(7);

        var table = new TileAssetFlagTable();
        table.Set(center, TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.Center));
        table.Set(northWest, TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.NorthWest));
        table.Set(northEast, TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.NorthEast));
        table.Set(southWest, TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.SouthWest));
        table.Set(southEast, TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.SouthEast));
        table.Set(isolated, TileAssetFlags.Default.WithTerrain(3).WithAutotile("eau", AutotileRole.Isolated));
        table.Set(sand, TileAssetFlags.Default.WithTerrain(1).WithAutotile("sable", AutotileRole.Center));

        var map = MapFormat.CreateTileAssetMap("Eau", 2, 2);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.TileFlags = table;
        Paint(map, 0, 0, center);
        Paint(map, 1, 0, center);
        Paint(map, 0, 1, center);
        Paint(map, 1, 1, center);

        Assert.Equal(northWest, At(map, 0, 0));
        Assert.Equal(northEast, At(map, 1, 0));
        Assert.Equal(southWest, At(map, 0, 1));
        Assert.Equal(southEast, At(map, 1, 1));
        Assert.Equal(3, MapCollision.CellTerrain(map, 0, 0));
        Assert.Equal(TileType.Ground, map.Layers[0].Tiles.Single(tile => tile.X == 0 && tile.Y == 0).Type);

        var alone = MapFormat.CreateTileAssetMap("Isolée", 1, 1);
        alone.Layers.Add(new Layer { LayerType = LayerType.Ground });
        alone.TileFlags = table;
        Paint(alone, 0, 0, center);
        Assert.Equal(isolated, At(alone, 0, 0));

        var shore = MapFormat.CreateTileAssetMap("Rive", 2, 1);
        shore.Layers.Add(new Layer { LayerType = LayerType.Ground });
        shore.TileFlags = table;
        Paint(shore, 0, 0, center);
        Paint(shore, 1, 0, sand);
        Assert.Equal(isolated, At(shore, 0, 0));
        Assert.Equal(sand, At(shore, 1, 0));
        Assert.Equal(1, MapCollision.CellTerrain(shore, 1, 0));

        var bytes = new MapSerializer().Serialize(map);
        Assert.Equal((byte)6, bytes[4]);
        var loaded = new MapSerializer().Deserialize(bytes);
        Assert.Equal(northWest, loaded.Layers[0].Tiles.Single(tile => tile.X == 0 && tile.Y == 0).AssetId);
        Assert.Null(loaded.TileFlags);

        var json = table.ToJson();
        Assert.Contains("\"autotileGroup\": \"eau\"", json, StringComparison.Ordinal);
        Assert.Contains("\"autotileRole\": \"center\"", json, StringComparison.Ordinal);
        Assert.Contains("\"terrain\": 3", json, StringComparison.Ordinal);
        var round = TileAssetFlagTable.FromJson(json);
        Assert.Equal(AutotileRole.Center, round.Get(center).AutotileRole);
        Assert.Equal("eau", round.Get(center).AutotileGroup);
        Assert.Equal(3, round.Get(center).Terrain);
        Assert.Equal("n° terrain 3 · eau · Centre", TileAssetFlagLabels.FormatBrush(round.Get(center)));

        var legacy = TileAssetFlagTable.FromJson(
            $$"""{ "version": 1, "tiles": { "{{center.ToHex()}}": { "terrain": 4 } } }""");
        Assert.Equal(4, legacy.Get(center).Terrain);
        Assert.Equal(AutotileRole.None, legacy.Get(center).AutotileRole);
        Assert.Equal(string.Empty, legacy.Get(center).AutotileGroup);

        var bush = TileAssetFlags.Default.WithBush(true).WithTerrain(2);
        var tagged = new TileAssetFlagTable();
        tagged.Set(center, bush);
        Assert.Equal(bush, TileAssetFlagTable.FromJson(tagged.ToJson()).Get(center));

        Assert.Throws<ArgumentException>(() => table.Set(Id(8), TileAssetFlags.Default.WithAutotile("eau", AutotileRole.Center)));
        Assert.Throws<ArgumentException>(() => TileAssetFlags.Default.WithAutotile("eau", AutotileRole.None));
        Assert.Throws<ArgumentException>(() => TileAssetFlags.Default.WithAutotile("", AutotileRole.Center));
        Assert.Throws<InvalidDataException>(() => TileAssetFlagTable.FromJson(
            $$"""{ "version": 1, "tiles": { "{{Id(9).ToHex()}}": { "autotileGroup": "eau", "autotileRole": "cascade" } } }"""));

        var onlyCenter = new TileAssetFlagTable();
        onlyCenter.Set(center, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.Center));
        var blob = MapFormat.CreateTileAssetMap("Centre", 2, 2);
        blob.Layers.Add(new Layer { LayerType = LayerType.Ground });
        blob.TileFlags = onlyCenter;
        Paint(blob, 0, 0, center);
        Paint(blob, 1, 0, center);
        Paint(blob, 0, 1, center);
        Paint(blob, 1, 1, center);
        Assert.Equal(center, At(blob, 0, 0));
        Assert.Equal(center, At(blob, 1, 1));

        map.TileFlags = null;
        var frozen = new MapSerializer().Serialize(map);
        Assert.Equal((byte)6, frozen[4]);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Fact]
    public void Erase_UpdatesTheRemainingNeighbor()
    {
        var center = Id(1);
        var west = Id(2);
        var east = Id(3);
        var isolated = Id(4);
        var table = new TileAssetFlagTable();
        table.Set(center, TileAssetFlags.Default.WithAutotile("mur", AutotileRole.Center));
        table.Set(west, TileAssetFlags.Default.WithAutotile("mur", AutotileRole.West));
        table.Set(east, TileAssetFlags.Default.WithAutotile("mur", AutotileRole.East));
        table.Set(isolated, TileAssetFlags.Default.WithAutotile("mur", AutotileRole.Isolated));

        var map = MapFormat.CreateTileAssetMap("Mur", 2, 1);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.TileFlags = table;
        Paint(map, 0, 0, center);
        Paint(map, 1, 0, center);
        Assert.Equal(west, At(map, 0, 0));
        Assert.Equal(east, At(map, 1, 0));

        map.Layers[0].Tiles.RemoveAll(tile => tile.X == 1 && tile.Y == 0);
        Assert.Equal(1, AutotileJoin.ReconcileLayer(map, 0));
        Assert.Equal(isolated, At(map, 0, 0));
    }

    [Fact]
    public void PreviewStamp_MatchesPaint_WithoutMutating()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal("aperçu raccord · Isolée", TileAssetFlagLabels.FormatJoinPreview(AutotileRole.Isolated));
        Assert.Equal("aperçu raccord · Bord ouest", TileAssetFlagLabels.FormatJoinPreview(AutotileRole.West));
        Assert.DoesNotContain("frame", TileAssetFlagLabels.FormatJoinPreview(AutotileRole.North), StringComparison.OrdinalIgnoreCase);

        var center = Id(1);
        var west = Id(2);
        var east = Id(3);
        var isolated = Id(4);
        var northWest = Id(5);
        var northEast = Id(6);
        var southWest = Id(7);
        var southEast = Id(8);
        var table = new TileAssetFlagTable();
        table.Set(center, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.Center));
        table.Set(west, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.West));
        table.Set(east, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.East));
        table.Set(isolated, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.Isolated));
        table.Set(northWest, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.NorthWest));
        table.Set(northEast, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.NorthEast));
        table.Set(southWest, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.SouthWest));
        table.Set(southEast, TileAssetFlags.Default.WithAutotile("eau", AutotileRole.SouthEast));

        var shore = MapFormat.CreateTileAssetMap("Rive", 3, 1);
        shore.Layers.Add(new Layer { LayerType = LayerType.Ground });
        shore.TileFlags = table;
        var alone = AutotileJoin.PreviewStamp(shore, 0, center, new[] { (1, 0) });
        var aloneCell = Assert.Single(alone);
        Assert.Equal(isolated, aloneCell.Id);
        Assert.Empty(shore.Layers[0].Tiles);

        Paint(shore, 1, 0, center);
        Assert.Equal(isolated, At(shore, 1, 0));
        var preview = AutotileJoin.PreviewStamp(shore, 0, center, new[] { (0, 0) });
        Assert.Contains(preview, cell => cell.X == 0 && cell.Y == 0 && cell.Id == west);
        Assert.Contains(preview, cell => cell.X == 1 && cell.Y == 0 && cell.Id == east);
        Assert.Equal(isolated, At(shore, 1, 0));
        Assert.Single(shore.Layers[0].Tiles);

        var block = MapFormat.CreateTileAssetMap("Bloc", 2, 2);
        block.Layers.Add(new Layer { LayerType = LayerType.Ground });
        block.TileFlags = table;
        var stamp = new[] { (0, 0), (1, 0), (0, 1), (1, 1) };
        var joined = AutotileJoin.PreviewStamp(block, 0, center, stamp);
        Assert.Equal(northWest, PreviewAt(joined, 0, 0));
        Assert.Equal(northEast, PreviewAt(joined, 1, 0));
        Assert.Equal(southWest, PreviewAt(joined, 0, 1));
        Assert.Equal(southEast, PreviewAt(joined, 1, 1));
        Assert.Empty(block.Layers[0].Tiles);
        foreach (var (x, y) in stamp)
        {
            Paint(block, x, y, center);
        }

        Assert.Equal(northWest, At(block, 0, 0));
        Assert.Equal(northEast, At(block, 1, 0));
        Assert.Equal(southWest, At(block, 0, 1));
        Assert.Equal(southEast, At(block, 1, 1));

        var bare = MapFormat.CreateTileAssetMap("Nu", 1, 1);
        bare.Layers.Add(new Layer { LayerType = LayerType.Ground });
        Assert.Equal(center, Assert.Single(AutotileJoin.PreviewStamp(bare, 0, center, new[] { (0, 0) })).Id);
        Assert.Empty(AutotileJoin.PreviewStamp(bare, 0, center, new[] { (-1, 0) }));
    }

    private static void Paint(Map map, int x, int y, TileAssetId id)
    {
        MapEditOperations.PaintTile(map, 0, x, y, new Tile { X = x, Y = y, AssetId = id, Type = TileType.Ground });
        AutotileJoin.ReconcileNeighborhood(map, 0, x, y);
    }

    private static TileAssetId At(Map map, int x, int y)
        => map.Layers[0].Tiles.Single(tile => tile.X == x && tile.Y == y).AssetId;

    private static TileAssetId PreviewAt(IReadOnlyList<AutotilePreviewCell> cells, int x, int y)
        => cells.Single(cell => cell.X == x && cell.Y == y).Id;

    private static TileAssetId Id(byte mark)
    {
        var bytes = new byte[TileAssetId.ByteLength];
        bytes[0] = mark;
        bytes[^1] = 0x48;
        return TileAssetId.FromHashBytes(bytes);
    }
}
