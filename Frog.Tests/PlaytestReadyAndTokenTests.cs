using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestReadyMarkerTests
{
    [Fact]
    public void Format_Parse_RoundTrip_Spawn11_AuthoritativePixels()
    {
        var corr = Guid.NewGuid();
        var (px, py) = WorldMetrics.TileCenterToPixels(1, 1);
        Assert.Equal(48, px);
        Assert.Equal(48, py);
        var line = PlaytestReadyMarker.Format(corr, runtimeMapId: 1, tileX: 1, tileY: 1, pixelX: px, pixelY: py);
        Assert.True(PlaytestReadyMarker.TryParse(line, out var values));
        Assert.Equal(corr, values.CorrelationId);
        Assert.Equal(1, values.RuntimeMapId);
        Assert.Equal(1, values.TileX);
        Assert.Equal(1, values.TileY);
        Assert.Equal(48, values.PixelX);
        Assert.Equal(48, values.PixelY);
    }

    [Fact]
    public void Validate_Rejects_WrongMap()
    {
        var corr = Guid.NewGuid();
        var (px, py) = WorldMetrics.TileCenterToPixels(1, 1);
        var line = PlaytestReadyMarker.Format(corr, 2, 1, 1, px, py);
        var spawn = new PlaytestSpawnPoint { RuntimeMapId = 1, TileX = 1, TileY = 1 };
        Assert.False(PlaytestReadyMarker.TryValidateAgainstPlan(line, corr, spawn, out _, out var err));
        Assert.Contains("map mismatch", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Rejects_WrongSpawnTile()
    {
        var corr = Guid.NewGuid();
        var (px, py) = WorldMetrics.TileCenterToPixels(0, 0);
        var line = PlaytestReadyMarker.Format(corr, 1, 0, 0, px, py);
        var spawn = new PlaytestSpawnPoint { RuntimeMapId = 1, TileX = 1, TileY = 1 };
        Assert.False(PlaytestReadyMarker.TryValidateAgainstPlan(line, corr, spawn, out _, out var err));
        Assert.Contains("tile mismatch", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Rejects_Malformed_And_CorrelationOnly()
    {
        var corr = Guid.NewGuid();
        var spawn = new PlaytestSpawnPoint { RuntimeMapId = 1, TileX = 1, TileY = 1 };
        Assert.False(PlaytestReadyMarker.TryValidateAgainstPlan(
            $"FROG_PLAYTEST_READY correlation={corr:N}",
            corr,
            spawn,
            out _,
            out var err));
        Assert.Contains("malformed", err, StringComparison.OrdinalIgnoreCase);

        Assert.False(PlaytestReadyMarker.TryValidateAgainstPlan(
            $"FROG_PLAYTEST_READY correlation={corr:N} map=1 x=0 y=0",
            corr,
            spawn,
            out _,
            out _));
    }

    [Fact]
    public void Validate_Accepts_PrefixedLauncherLogLine()
    {
        var corr = Guid.NewGuid();
        var (px, py) = WorldMetrics.TileCenterToPixels(1, 1);
        var raw = PlaytestReadyMarker.Format(corr, 1, 1, 1, px, py);
        var wrapped = $"[{corr:N}] [client:out] {raw}";
        var spawn = new PlaytestSpawnPoint { RuntimeMapId = 1, TileX = 1, TileY = 1 };
        Assert.True(PlaytestReadyMarker.TryValidateAgainstPlan(wrapped, corr, spawn, out var values, out _));
        Assert.Equal(48, values.PixelX);
    }
}

public sealed class PlaytestAuthTokenGateTests
{
    [Fact]
    public void TryConsume_FirstSucceeds_SecondFails_EvenSameValue()
    {
        var token = PlaytestAuthToken.Create();
        var gate = new PlaytestAuthTokenGate(token);
        Assert.True(gate.TryConsume(token));
        Assert.False(gate.HasRemainingToken);
        Assert.False(gate.TryConsume(token));
    }

    [Fact]
    public void TryConsume_WrongToken_DoesNotConsume()
    {
        var token = PlaytestAuthToken.Create();
        var gate = new PlaytestAuthTokenGate(token);
        Assert.False(gate.TryConsume("not-the-token"));
        Assert.True(gate.HasRemainingToken);
        Assert.True(gate.TryConsume(token));
    }

    [Fact]
    public void Concurrent_OnlyOneConsumerSucceeds()
    {
        var token = PlaytestAuthToken.Create();
        var gate = new PlaytestAuthTokenGate(token);
        var successes = 0;
        Parallel.For(0, 32, _ =>
        {
            if (gate.TryConsume(token))
            {
                System.Threading.Interlocked.Increment(ref successes);
            }
        });
        Assert.Equal(1, successes);
        Assert.False(gate.HasRemainingToken);
    }
}

public sealed class PlaytestWorkspaceLeakTests
{
    [Fact]
    public async Task Invalid_Supplied_WorkDirectory_Leaves_No_New_Owned_Directory()
    {
        var corr = Guid.NewGuid();
        var root = PlaytestWorkspacePaths.GetCanonicalRoot();
        var ownedPath = Path.Combine(root, corr.ToString("N"));
        Assert.False(Directory.Exists(ownedPath));

        var invalid = Path.Combine(Path.GetTempPath(), "frog-not-owned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(invalid);

        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var result = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = corr,
                SpawnTileX = 0,
                SpawnTileY = 0,
                WorkDirectory = invalid,
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
            });

        Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.False(Directory.Exists(ownedPath), "invalid WorkDirectory must not create a new owned workspace");

        Directory.Delete(invalid, recursive: true);
    }
}
