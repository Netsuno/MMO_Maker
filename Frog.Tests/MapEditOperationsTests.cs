using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEditOperationsTests
{
    [Fact]
    public void PaintTile_SetsDirtyCandidate_OnEditableLayer()
    {
        var map = CreateMap();
        var stamp = new Tile { Type = TileType.Ground, SrcX = 1, SrcY = 2, TilesetId = 1 };
        MapEditOperations.PaintTile(map, 0, 1, 1, stamp);
        var tile = map.Layers[0].Tiles.Single(t => t.X == 1 && t.Y == 1);
        Assert.Equal(TileType.Ground, tile.Type);
        Assert.Equal(1, tile.SrcX);
    }

    [Fact]
    public void PaintTile_DoesNotModifyLockedLayer()
    {
        var map = CreateMap();
        map.Layers[0].Locked = true;
        MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground });
        Assert.Empty(map.Layers[0].Tiles);
    }

    [Fact]
    public void PaintTile_CopiesBlockWarpAndResourceAttributes()
    {
        var map = CreateMap();
        var warpTarget = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var stamp = new Tile
        {
            Type = TileType.Warp,
            WarpTargetMapId = warpTarget,
            WarpTargetX = 2,
            WarpTargetY = 3,
            Attributes =
            {
                new BlockAttribute(),
                new WarpAttribute { TargetMapId = warpTarget, TargetX = 2, TargetY = 3 },
                new ResourceAttribute { ResourceId = 9 },
            },
        };

        MapEditOperations.PaintTile(map, 0, 1, 1, stamp);
        var tile = map.Layers[0].Tiles.Single();
        Assert.Equal(3, tile.Attributes.Count);
        Assert.Contains(tile.Attributes, attribute => attribute is BlockAttribute);
        var warp = Assert.IsType<WarpAttribute>(tile.Attributes[1]);
        Assert.Equal(warpTarget, warp.TargetMapId);
        Assert.Equal(9, Assert.IsType<ResourceAttribute>(tile.Attributes[2]).ResourceId);
        Assert.NotSame(stamp.Attributes[0], tile.Attributes[0]);

        map.Layers.Add(new Layer { LayerType = LayerType.Fringe });
        MapEditOperations.PaintTile(map, 1, 0, 0, new Tile { Type = TileType.Ground, SrcX = 5 });
        map.Layers[1].Locked = true;
        MapEditOperations.EraseRectangle(map, 1, 0, 0, 1, 1);
        Assert.Contains(map.Layers[1].Tiles, tile => tile.X == 0 && tile.Y == 0 && tile.SrcX == 5);
    }

    [Fact]
    public void PaintTile_PreservesTileAssetId_AndFloodFillDoesNotCrossIds()
    {
        var red = TileAssetId.FromStraightRgba(SolidRgba(255, 0, 0, 255));
        var blue = TileAssetId.FromStraightRgba(SolidRgba(0, 0, 255, 255));
        var map = MapFormat.CreateTileAssetMap("TileAsset", 2, 1);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        MapEditOperations.PaintTile(map, 0, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });
        MapEditOperations.PaintTile(map, 0, 1, 0, new Tile { AssetId = blue, Type = TileType.Ground });

        var left = map.Layers[0].Tiles.Single(tile => tile.X == 0);
        Assert.Equal(red, left.AssetId);
        Assert.Equal(0, left.TilesetId);
        Assert.Equal(0, left.SrcX);
        Assert.Equal(0, left.SrcY);

        MapEditOperations.FloodFill(map, 0, 0, 0, new Tile { AssetId = red, Type = TileType.Ground });
        Assert.Equal(blue, map.Layers[0].Tiles.Single(tile => tile.X == 1).AssetId);
        Assert.Equal(TileAssetMetrics.TargetTileSizePixels, map.TileSizePixels);
    }

    [Fact]
    public void EraseTile_RemovesTile()
    {
        var map = CreateMap();
        MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground });
        MapEditOperations.EraseTile(map, 0, 1, 1);
        Assert.Empty(map.Layers[0].Tiles);
    }

    [Fact]
    public void EnumerateLine_IsOneTileWideAndIncludesBothEnds()
    {
        var diagonal = MapEditOperations.EnumerateLine(0, 0, 4, 2);
        Assert.Equal(new[] { (0, 0), (1, 0), (2, 1), (3, 1), (4, 2) }, diagonal);
        Assert.Equal(new[] { (2, 2) }, MapEditOperations.EnumerateLine(2, 2, 2, 2));
        Assert.Equal(new[] { (0, 0), (1, 0), (2, 0), (3, 0) }, MapEditOperations.EnumerateLine(0, 0, 3, 0));
        Assert.Equal(new[] { (1, 0), (1, 1), (1, 2) }, MapEditOperations.EnumerateLine(1, 0, 1, 2));

        var back = MapEditOperations.EnumerateLine(4, 2, 0, 0);
        Assert.Equal((4, 2), back[0]);
        Assert.Equal((0, 0), back[^1]);
        Assert.Equal(Math.Max(4, 2) + 1, back.Count);
        Assert.DoesNotContain((0, 2), back);
    }

    [Fact]
    public void PaintLine_PaintsOnlyBresenhamCells()
    {
        var map = CreateMap();
        var stamp = new Tile { Type = TileType.Ground, SrcX = 7, TilesetId = 3 };
        MapEditOperations.PaintLine(map, 0, 0, 0, 4, 2, stamp);

        var tiles = map.Layers[0].Tiles;
        Assert.Equal(5, tiles.Count);
        Assert.All(tiles, t => Assert.Equal(7, t.SrcX));
        Assert.Contains(tiles, t => t.X == 0 && t.Y == 0);
        Assert.Contains(tiles, t => t.X == 4 && t.Y == 2);
        Assert.DoesNotContain(tiles, t => t.X == 0 && t.Y == 2);
        Assert.DoesNotContain(tiles, t => t.X == 4 && t.Y == 0);
    }

    [Fact]
    public void PaintLine_SkipsLockedLayerAndOutOfBounds()
    {
        var map = CreateMap();
        map.Layers[0].Locked = true;
        MapEditOperations.PaintLine(map, 0, 0, 0, 2, 0, new Tile { Type = TileType.Ground });
        Assert.Empty(map.Layers[0].Tiles);

        map.Layers[0].Locked = false;
        MapEditOperations.PaintLine(map, 0, -1, 0, 2, 0, new Tile { Type = TileType.Ground, SrcX = 1 });
        Assert.Equal(3, map.Layers[0].Tiles.Count);
        Assert.DoesNotContain(map.Layers[0].Tiles, t => t.X < 0);
    }

    [Fact]
    public void ConstrainToDominantAxis_PrefersHorizontalOnTie()
    {
        Assert.Equal((4, 0), MapEditOperations.ConstrainToDominantAxis(0, 0, 4, 2));
        Assert.Equal((0, 3), MapEditOperations.ConstrainToDominantAxis(0, 0, 2, 3));
        Assert.Equal((3, 0), MapEditOperations.ConstrainToDominantAxis(0, 0, 3, 3));
    }

    [Fact]
    public void PaintRectangle_FillsArea()
    {
        var map = CreateMap();
        var stamp = new Tile { Type = TileType.Ground };
        MapEditOperations.PaintRectangle(map, 0, 0, 0, 2, 2, stamp);
        Assert.Equal(9, map.Layers[0].Tiles.Count);
    }

    [Fact]
    public void FloodFill_StaysWithinMapBounds()
    {
        var map = CreateMap();
        MapEditOperations.FloodFill(map, 0, 0, 0, new Tile { Type = TileType.Ground, SrcX = 5 });
        Assert.Equal(map.Width * map.Height, map.Layers[0].Tiles.Count);
    }

    [Fact]
    public void SetBlockTile_CreatesBlock()
    {
        var map = CreateMap();
        MapEditOperations.SetBlockTile(map, 0, 2, 2);
        var tile = map.Layers[0].Tiles.Single();
        Assert.Equal(TileType.Block, tile.Type);
    }

    [Fact]
    public void SetWarpDestination_StoresTarget()
    {
        var map = CreateMap();
        var targetId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        MapEditOperations.SetWarpDestination(map, 0, 1, 1, targetId, 3, 4);
        var tile = map.Layers[0].Tiles.Single();
        Assert.Equal(TileType.Warp, tile.Type);
        Assert.Equal(targetId, tile.WarpTargetMapId);
        Assert.Equal(3, tile.WarpTargetX);
        Assert.Equal(4, tile.WarpTargetY);
    }

    [Fact]
    public void LayerOperations_UpdateMetadata()
    {
        var map = CreateMap();
        MapEditOperations.AddLayer(map, LayerType.Mask);
        Assert.Equal(2, map.Layers.Count);
        MapEditOperations.RenameLayer(map, 1, "Masque");
        Assert.Equal("Masque", map.Layers[1].DisplayName);
        MapEditOperations.ChangeLayerType(map, 1, LayerType.Fringe);
        Assert.Equal(LayerType.Fringe, map.Layers[1].LayerType);
        MapEditOperations.SetLayerVisibility(map, 1, false);
        Assert.False(map.Layers[1].Visible);
        MapEditOperations.SetLayerLocked(map, 1, true);
        Assert.True(map.Layers[1].Locked);
        MapEditOperations.RemoveLayer(map, 1);
        Assert.Single(map.Layers);
    }

    [Fact]
    public async Task SaveAndReload_PreservesEditedModel()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();
        var map = session.CurrentMap!;
        MapEditOperations.SetBlockTile(map, map.Layers.FindIndex(l => l.LayerType == LayerType.Attributes), 2, 2);
        session.MarkDirty();

        Assert.IsType<SaveMapResult.Success>(await session.SaveCurrentAsync(SaveMapIntent.SaveDraft));

        var reloaded = await repo.LoadByIdAsync(session.CurrentMapId!.Value);
        Assert.NotNull(reloaded);
        var attr = reloaded.Map.Layers.First(l => l.LayerType == LayerType.Attributes);
        Assert.Contains(attr.Tiles, t => t.Type == TileType.Block && t.X == 2 && t.Y == 2);
    }

    private static Map CreateMap()
    {
        var map = new Map { Name = "Test", Width = 5, Height = 5 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        return map;
    }

    private static byte[] SolidRgba(byte r, byte g, byte b, byte a)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = a;
        }

        return bytes;
    }
}
