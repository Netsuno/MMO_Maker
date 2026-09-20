using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Gameplay;
using Frog.Core.Maps;
using Xunit;

namespace Frog.Tests;

/// <summary>Fluidity MVP stays on the existing path: protocol 11, walk clock, pulse 52.</summary>
public sealed class MovementFluidityMvpTests
{
    [Fact]
    public void StatusDoc_RecordsBaselineAndCheapWins()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "movement", "STATUS-fluidity-mvp.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Owner** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("render_intent_to_visible", text, StringComparison.Ordinal);
        Assert.Contains("20.3", text, StringComparison.Ordinal);
        Assert.Contains("181", text, StringComparison.Ordinal);
        Assert.Contains("25.2", text, StringComparison.Ordinal);
        Assert.Contains("2faa511", text, StringComparison.Ordinal);
        Assert.Contains("ClampVisualDt", text, StringComparison.Ordinal);
        Assert.Contains("ResolveLocalServerSample", text, StringComparison.Ordinal);
        Assert.Contains("protocol 11", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientWiresHelpers_WithoutRewritingArchitecture()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var clock = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Gameplay", "PlayerWalkClock.cs"));

        Assert.Contains("MovementFluidity.ClampVisualDt", shell, StringComparison.Ordinal);
        Assert.Contains("MovementFluidity.ResolveLocalServerSample", shell, StringComparison.Ordinal);
        Assert.Contains("AdvanceCameraFocus", shell, StringComparison.Ordinal);
        Assert.Contains("MapViewportCamera.DampFocus", shell, StringComparison.Ordinal);
        Assert.Contains("NoteMoveIntent();", shell, StringComparison.Ordinal);
        Assert.Contains("AdvanceMovementSmoothing();", shell, StringComparison.Ordinal);
        Assert.Contains("RedrawMap();", shell, StringComparison.Ordinal);
        Assert.Contains("PrimeMoveNetworkPulse();", shell, StringComparison.Ordinal);
        Assert.Contains("MoveNetworkPulseMs = 52", shell, StringComparison.Ordinal);
        Assert.Contains("speedPxPerSec * dt", shell, StringComparison.Ordinal);
        Assert.Contains("PlayerWalkClock.FacingFromVector", shell, StringComparison.Ordinal);
        Assert.Contains("_localWalkElapsedMs += (int)(rawDt * 1000f)", shell, StringComparison.Ordinal);

        Assert.Contains("FrameDurationMs = 140", clock, StringComparison.Ordinal);
        Assert.Equal(140, PlayerWalkClock.FrameDurationMs);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(8, WorldMetrics.PlayerMovePixelsPerRequest);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(256f, MovementFluidity.SnapDesyncPx);
        Assert.Equal(0.048f, MovementFluidity.MaxVisualDtSeconds);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
