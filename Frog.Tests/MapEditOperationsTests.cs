using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Core.Enums;
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
    public void EraseTile_RemovesTile()
    {
        var map = CreateMap();
        MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground });
        MapEditOperations.EraseTile(map, 0, 1, 1);
        Assert.Empty(map.Layers[0].Tiles);
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
}
