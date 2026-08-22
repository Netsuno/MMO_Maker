using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestMapPreparerTests
{
    [Fact]
    public async Task Prepare_PublishesAndWritesManifest_NeverUsesDraftOnly()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        Assert.NotNull(workspace.CurrentMapId);

        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Port = 6011,
                SpawnTileX = 1,
                SpawnTileY = 1,
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
            });

        var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.True(File.Exists(success.Plan.ManifestPath));
        Assert.True(success.Plan.PrimaryPublishedRevision > 0);
        Assert.All(success.Plan.Maps, m => Assert.True(m.PublishedRevision > 0));

        // Dirty draft after publish must not change published snapshot used by playtest.
        workspace.CurrentMap!.Name = "DRAFT_ONLY_NAME";
        workspace.MarkDirty();
        await workspace.SaveCurrentAsync(SaveMapIntent.SaveDraft);
        var published = await repo.LoadPublishedByIdAndRevisionAsync(
            success.Plan.PrimaryCanonicalMapId,
            success.Plan.PrimaryPublishedRevision);
        Assert.NotNull(published);
        Assert.NotEqual("DRAFT_ONLY_NAME", published!.Map.Name);

        var reloaded = PlaytestManifestWriter.Read(success.Plan.ManifestPath);
        Assert.Equal(success.Plan.PrimaryPublishedRevision, reloaded.PrimaryPublishedRevision);
    }

    [Fact]
    public async Task Prepare_RejectsNonDurableWhenRequired()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryDemo);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest { RequireDurablePersistence = true });
        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.Equal(PlaytestFailureKind.NotDurable, failed.Kind);
    }

    [Fact]
    public async Task Prepare_SavesDirtyBeforePublish()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        workspace.CurrentMap!.Name = "BeforePlaytest";
        workspace.MarkDirty();
        Assert.True(workspace.IsDirty);

        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                SpawnTileX = 0,
                SpawnTileY = 0,
            });

        Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.False(workspace.IsDirty);
        Assert.Equal("BeforePlaytest", workspace.CurrentMap.Name);
    }

    [Fact]
    public async Task Prepare_RejectsInvalidSpawn()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                SpawnTileX = 9999,
                SpawnTileY = 9999,
                RequireDurablePersistence = false,
            });
        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.Equal(PlaytestFailureKind.Validation, failed.Kind);
    }

    [Fact]
    public void RuntimeMapIdAllocator_RewritesWarpsToPackedGuids()
    {
        var allocator = new RuntimeMapIdAllocator();
        var primary = Guid.NewGuid();
        var target = Guid.NewGuid();
        allocator.Allocate(primary);
        var targetRuntime = allocator.Allocate(target);

        var map = MapSamples.StarterMeadow(target);
        var rewritten = allocator.RewriteWarpsToRuntimeGuids(map);
        var warp = rewritten.Layers[0].Tiles.Single(t => t.Type == TileType.Warp);
        Assert.Equal(MapSamples.RuntimeMapIdToGuid(targetRuntime), warp.WarpTargetMapId);
    }

    [Fact]
    public async Task Prepare_BrandNewUnsavedMap_SavesPublishesAndReturnsMapId()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        var map = CreateOpenMap("BrandNew", 6, 6);
        workspace.AdoptLocalDraft(map);
        Assert.Null(workspace.CurrentMapId);
        Assert.True(workspace.IsDirty);

        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
                SpawnTileX = 0,
                SpawnTileY = 0,
            });

        var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.NotEqual(Guid.Empty, success.Plan.PrimaryCanonicalMapId);
        Assert.Equal(workspace.CurrentMapId, success.Plan.PrimaryCanonicalMapId);
        Assert.True(success.Plan.PrimaryPublishedRevision > 0);
        Assert.True(File.Exists(success.Plan.ManifestPath));
    }

    [Fact]
    public async Task Prepare_WarpGraph_A_to_B_to_C_IncludesAllPublishedMaps()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var idC = await PublishOpenAsync(repo, "C");
        var idB = await PublishOpenAsync(repo, "B", warpTo: idC, warpX: 1, warpY: 1);
        var idA = await PublishOpenAsync(repo, "A", warpTo: idB, warpX: 1, warpY: 0);

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(idA));
        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = false,
                SpawnTileX = 0,
                SpawnTileY = 0,
            });

        var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.Equal(3, success.Plan.Maps.Count);
        Assert.Contains(success.Plan.Maps, m => m.CanonicalMapId == idA && m.RuntimeMapId == 1);
        Assert.Contains(success.Plan.Maps, m => m.CanonicalMapId == idB);
        Assert.Contains(success.Plan.Maps, m => m.CanonicalMapId == idC);
        Assert.Equal(new[] { 1, 2, 3 }, success.Plan.Maps.Select(m => m.RuntimeMapId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Prepare_WarpGraph_Cycle_A_B_DoesNotHang_AndIncludesBoth()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        // Publish A first without warp, then B→A, then update A→B.
        var saveA0 = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreateOpenMap("A", 6, 6),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));
        idA = saveA0.MapId;

        var mapB = CreateOpenMap("B", 6, 6);
        SetWarp(mapB, 1, 1, idA, 0, 0);
        var saveB = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = mapB,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));
        idB = saveB.MapId;

        var storedA = await repo.LoadByIdAsync(idA);
        Assert.NotNull(storedA);
        SetWarp(storedA!.Map, 2, 2, idB, 0, 0);
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = idA,
            Map = storedA.Map,
            ExpectedRevision = storedA.Revision,
            Intent = SaveMapIntent.Publish,
        }));

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(idA));
        var preparer = new PlaytestMapPreparer(repo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = false,
                SpawnTileX = 0,
                SpawnTileY = 0,
            },
            cts.Token);

        var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.Equal(2, success.Plan.Maps.Count);
        Assert.Contains(success.Plan.Maps, m => m.CanonicalMapId == idA);
        Assert.Contains(success.Plan.Maps, m => m.CanonicalMapId == idB);
    }

    [Fact]
    public async Task Prepare_WarpGraph_SharedTarget_Deduplicates()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var idShared = await PublishOpenAsync(repo, "Shared");
        var idB = await PublishOpenAsync(repo, "B", warpTo: idShared, warpX: 0, warpY: 1);
        var mapA = CreateOpenMap("A", 6, 6);
        SetWarp(mapA, 1, 0, idShared, 0, 0);
        SetWarp(mapA, 2, 0, idB, 0, 0);
        var saveA = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = mapA,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(saveA.MapId));
        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = false,
                SpawnTileX = 0,
                SpawnTileY = 0,
            });

        var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.Equal(3, success.Plan.Maps.Count);
        Assert.Equal(1, success.Plan.Maps.Count(m => m.CanonicalMapId == idShared));
    }

    [Fact]
    public async Task Prepare_WarpGraph_UnpublishedTransitiveTarget_FailsClearly()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        // B exists only as draft (never published).
        var draftB = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreateOpenMap("B-DraftOnly", 6, 6),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        }));

        var mapA = CreateOpenMap("A", 6, 6);
        SetWarp(mapA, 1, 0, draftB.MapId, 0, 0);
        var saveA = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = mapA,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(saveA.MapId));
        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = false,
                SpawnTileX = 0,
                SpawnTileY = 0,
            });

        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.Equal(PlaytestFailureKind.NotPublished, failed.Kind);
        Assert.Contains(draftB.MapId.ToString(), failed.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> PublishOpenAsync(
        InMemoryMapRepository repo,
        string name,
        Guid? warpTo = null,
        int warpX = 0,
        int warpY = 0)
    {
        var map = CreateOpenMap(name, 6, 6);
        if (warpTo is Guid target)
        {
            SetWarp(map, warpX, warpY, target, 0, 0);
        }

        var save = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        }));
        return save.MapId;
    }

    private static Map CreateOpenMap(string name, int w, int h)
    {
        var map = new Map { Name = name, Width = w, Height = h };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                ground.Tiles.Add(new Tile { X = x, Y = y, TilesetId = 1, Type = TileType.Ground });
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private static void SetWarp(Map map, int x, int y, Guid target, int tx, int ty)
    {
        var t = map.Layers[0].Tiles.Single(tile => tile.X == x && tile.Y == y);
        t.Type = TileType.Warp;
        t.WarpTargetMapId = target;
        t.WarpTargetX = tx;
        t.WarpTargetY = ty;
    }
}

public sealed class PlaytestOrchestratorTests
{
    [Fact]
    public async Task Start_CleansUp_OnLaunchFailure()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new FakeLauncher { FailOnServer = true };
        var orch = new PlaytestOrchestrator(preparer, launcher);

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                Port = 6123,
                RequireDurablePersistence = false,
                SpawnTileX = 1,
                SpawnTileY = 1,
            },
            serverExe: "server",
            clientExe: "client");

        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.Equal(PlaytestFailureKind.LaunchFailure, failed.Kind);
        Assert.True(launcher.StopCount >= 0);
        Assert.Null(orch.ActiveSession);
    }

    [Fact]
    public async Task Start_CleansUp_OnCancellation()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new FakeLauncher { DelayServerMs = 5_000 };
        var orch = new PlaytestOrchestrator(preparer, launcher);
        using var cts = new CancellationTokenSource(50);

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                Port = 6124,
                RequireDurablePersistence = false,
                SpawnTileX = 1,
                SpawnTileY = 1,
            },
            "server",
            "client",
            cts.Token);

        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.Equal(PlaytestFailureKind.Cancellation, failed.Kind);
        Assert.Null(orch.ActiveSession);
    }

    [Fact]
    public async Task Start_And_Stop_TracksProcesses()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new FakeLauncher();
        var orch = new PlaytestOrchestrator(preparer, launcher);

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                Port = 6125,
                RequireDurablePersistence = false,
                SpawnTileX = 1,
                SpawnTileY = 1,
            },
            "server",
            "client");

        Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.True(orch.ActiveSession!.IsActive);
        Assert.NotNull(orch.ActiveSession.Server);
        Assert.NotNull(orch.ActiveSession.Client);

        await orch.StopAsync();
        Assert.Null(orch.ActiveSession);
        Assert.Equal(2, launcher.StopCount);
    }

    [Fact]
    public async Task Start_TimesOut_WhenServerNeverReady()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new FakeLauncher { ThrowTimeout = true };
        var orch = new PlaytestOrchestrator(preparer, launcher);

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                Port = 6126,
                RequireDurablePersistence = false,
                SpawnTileX = 1,
                SpawnTileY = 1,
            },
            "server",
            "client");

        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.Equal(PlaytestFailureKind.Timeout, failed.Kind);
        Assert.Null(orch.ActiveSession);
    }

    private sealed class FakeLauncher : IPlaytestProcessLauncher
    {
        private int _nextPid = 1000;
        public bool FailOnServer { get; set; }
        public bool ThrowTimeout { get; set; }
        public int DelayServerMs { get; set; }
        public int StopCount { get; private set; }

        public async Task<PlaytestProcessHandle> StartServerAsync(
            PlaytestServerStartRequest request,
            CancellationToken cancellationToken = default)
        {
            if (DelayServerMs > 0)
            {
                await Task.Delay(DelayServerMs, cancellationToken).ConfigureAwait(false);
            }

            if (ThrowTimeout)
            {
                throw new TimeoutException("fake timeout");
            }

            if (FailOnServer)
            {
                throw new InvalidOperationException("fake launch failure");
            }

            return new PlaytestProcessHandle
            {
                ProcessId = Interlocked.Increment(ref _nextPid),
                Role = "server",
                ExecutablePath = request.ExecutablePath,
            };
        }

        public Task<PlaytestProcessHandle> StartClientAsync(
            PlaytestClientStartRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PlaytestProcessHandle
            {
                ProcessId = Interlocked.Increment(ref _nextPid),
                Role = "client",
                ExecutablePath = request.ExecutablePath,
            });

        public Task StopAsync(PlaytestProcessHandle handle, CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public bool IsRunning(PlaytestProcessHandle handle) => false;

        public bool HasOwnedProcesses => false;
    }
}
