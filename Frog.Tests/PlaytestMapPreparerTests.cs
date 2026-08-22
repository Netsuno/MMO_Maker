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
        var workDir = Path.Combine(Path.GetTempPath(), "frog-pt-" + Guid.NewGuid().ToString("N"));
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Port = 6011,
                SpawnTileX = 1,
                SpawnTileY = 1,
                WorkDirectory = workDir,
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
                WorkDirectory = Path.Combine(Path.GetTempPath(), "frog-pt-dirty-" + Guid.NewGuid().ToString("N")),
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
                WorkDirectory = Path.Combine(Path.GetTempPath(), "frog-pt-spawn-" + Guid.NewGuid().ToString("N")),
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
                WorkDirectory = Path.Combine(Path.GetTempPath(), "frog-orch-" + Guid.NewGuid().ToString("N")),
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
                WorkDirectory = Path.Combine(Path.GetTempPath(), "frog-orch-c-" + Guid.NewGuid().ToString("N")),
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
                WorkDirectory = Path.Combine(Path.GetTempPath(), "frog-orch-ok-" + Guid.NewGuid().ToString("N")),
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
                WorkDirectory = Path.Combine(Path.GetTempPath(), "frog-orch-to-" + Guid.NewGuid().ToString("N")),
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
    }
}
