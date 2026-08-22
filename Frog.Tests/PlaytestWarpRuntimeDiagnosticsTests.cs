using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Server.Config;
using Frog.Server.Playtest;
using Frog.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestWarpRuntimeDiagnosticsTests
{
    [Fact]
    public async Task PreparedAbcGraph_MapServiceResolvesBothWarps()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var mapC = CreateOpenMap("C", 8, 8);
        var saveC = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null, Map = mapC, ExpectedRevision = 0, Intent = SaveMapIntent.Publish,
        }));
        var mapB = CreateOpenMap("B", 8, 8);
        SetWarp(mapB, 1, 0, saveC.MapId, 2, 2);
        var saveB = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null, Map = mapB, ExpectedRevision = 0, Intent = SaveMapIntent.Publish,
        }));
        var mapA = CreateOpenMap("A", 8, 8);
        SetWarp(mapA, 1, 0, saveB.MapId, 0, 0);
        var saveA = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null, Map = mapA, ExpectedRevision = 0, Intent = SaveMapIntent.Publish,
        }));

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(saveA.MapId));
        var preparer = new PlaytestMapPreparer(repo);
        var workDir = Path.Combine(Path.GetTempPath(), "frog-diag-" + Guid.NewGuid().ToString("N"));
        var prepared = await preparer.PrepareAsync(workspace, new PlaytestPrepareRequest
        {
            WorkDirectory = workDir,
            RequireDurablePersistence = false,
            PublishCurrentBeforeLaunch = false,
            SpawnTileX = 0,
            SpawnTileY = 0,
        });
        var plan = Assert.IsType<PlaytestPreparationResult.Success>(prepared).Plan;

        var runtimeB = plan.Maps.Single(m => m.Name == "B").RuntimeMapId;
        var runtimeC = plan.Maps.Single(m => m.Name == "C").RuntimeMapId;
        var runtimeA = plan.Maps.Single(m => m.Name == "A").RuntimeMapId;
        Assert.Equal(1, runtimeA);

        var blob = PlaytestMapBlobStore.FromLaunchPlan(plan);
        var primaryPath = Path.Combine(plan.WorkDirectory, $"map-{runtimeA}.fmap");
        var mapService = new MapService(
            Options.Create(new WorldMapOptions { WorldMapPath = primaryPath, DatabaseFallbackMapId = runtimeA }),
            blob,
            NullLogger<MapService>.Instance);

        Assert.True(mapService.TryGetWarpDestination(runtimeA, 1, 0, out var toB, out _, out _));
        Assert.Equal(runtimeB, toB);
        Assert.True(mapService.TryEnsureMapLoaded(runtimeB));
        Assert.True(mapService.TryGetWarpDestination(runtimeB, 1, 0, out var toC, out var tx, out var ty));
        Assert.Equal(runtimeC, toC);
        Assert.Equal(2, tx);
        Assert.Equal(2, ty);
        Assert.True(mapService.TryEnsureMapLoaded(runtimeC));
    }

    private static Map CreateOpenMap(string name, int w, int h)
    {
        var map = new Map { Name = name, Width = w, Height = h };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                ground.Tiles.Add(new Tile { X = x, Y = y, TilesetId = 1, Type = TileType.Ground });
        map.Layers.Add(ground);
        return map;
    }

    private static void SetWarp(Map map, int x, int y, Guid target, int tx, int ty)
    {
        var t = map.Layers[0].Tiles.First(tile => tile.X == x && tile.Y == y);
        t.Type = TileType.Warp;
        t.WarpTargetMapId = target;
        t.WarpTargetX = tx;
        t.WarpTargetY = ty;
    }
}
