using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Frog.Core.Observability;
using Xunit;

namespace Frog.Tests;

/// <summary>Movement baseline probe: exists, flag defaults off, no gameplay change.</summary>
public sealed class MovementMeasureBaselineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("off")]
    [InlineData("no")]
    public void Flag_DefaultsOff_UnlessExplicitTruthy(string? value)
    {
        Assert.False(MovementMeasureOptions.IsEnabledValue(value));
        Assert.Equal("FROG_MOVEMENT_MEASURE", MovementMeasureOptions.EnvironmentVariable);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("ON")]
    public void Flag_AcceptsExplicitTruthy(string value)
        => Assert.True(MovementMeasureOptions.IsEnabledValue(value));

    [Fact]
    public void Environment_Unset_IsDisabled()
    {
        var previous = Environment.GetEnvironmentVariable(MovementMeasureOptions.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(MovementMeasureOptions.EnvironmentVariable, null);
            Assert.False(MovementMeasureOptions.IsEnabledFromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable(MovementMeasureOptions.EnvironmentVariable, previous);
        }
    }

    [Fact]
    public void Probe_Disabled_IsNoOp_AndSummarySaysOff()
    {
        var lines = new List<string>();
        var probe = new MovementMeasureProbe(enabled: false, lines.Add);

        probe.NoteKeyPress();
        Thread.Sleep(2);
        probe.NoteMoveIntent();
        probe.NoteVisibleUpdate();
        probe.NoteNetworkSend();
        probe.NoteLocalCorrection();
        probe.NoteOtherPlayerUpdate();
        probe.NoteFrameTime(16);
        probe.NoteServerApply(0.4);

        Assert.Empty(lines);
        Assert.Equal(0, probe.InputSampleCount);
        Assert.Equal(0, probe.RenderSampleCount);
        Assert.Equal(0, probe.NetworkLocalSampleCount);
        Assert.Equal(0, probe.OtherPlayerSampleCount);
        Assert.Equal(0, probe.FrameSampleCount);
        Assert.Equal(0, probe.ServerApplySampleCount);
        Assert.Equal("[measure] off", probe.FormatSummary());
    }

    [Fact]
    public void Probe_Enabled_RecordsInputRenderNetworkFrameAndServer()
    {
        var lines = new List<string>();
        var probe = new MovementMeasureProbe(enabled: true, lines.Add);

        probe.NoteKeyPress();
        Thread.Sleep(5);
        probe.NoteMoveIntent();
        Thread.Sleep(5);
        probe.NoteVisibleUpdate();
        probe.NoteNetworkSend();
        Thread.Sleep(5);
        probe.NoteLocalCorrection();
        probe.NoteOtherPlayerUpdate();
        Thread.Sleep(5);
        probe.NoteOtherPlayerUpdate();
        probe.NoteFrameTime(16.6);
        probe.NoteServerApply(0.5);

        Assert.Equal(1, probe.InputSampleCount);
        Assert.Equal(1, probe.RenderSampleCount);
        Assert.Equal(1, probe.NetworkLocalSampleCount);
        Assert.Equal(1, probe.OtherPlayerSampleCount);
        Assert.Equal(1, probe.FrameSampleCount);
        Assert.Equal(1, probe.ServerApplySampleCount);

        var summary = probe.FormatSummary();
        Assert.Contains("[measure] summary", summary, StringComparison.Ordinal);
        Assert.Contains("input_press_to_intent_ms: n=1", summary, StringComparison.Ordinal);
        Assert.Contains("render_intent_to_visible_ms: n=1", summary, StringComparison.Ordinal);
        Assert.Contains("net_send_to_local_correction_ms: n=1", summary, StringComparison.Ordinal);
        Assert.Contains("net_other_player_update_interval_ms: n=1", summary, StringComparison.Ordinal);
        Assert.Contains("frame_dt_ms: n=1", summary, StringComparison.Ordinal);
        Assert.Contains("server_apply_ms: n=1", summary, StringComparison.Ordinal);
        Assert.Contains("fps_note", summary, StringComparison.Ordinal);
        Assert.Contains("dt-scaled", summary, StringComparison.Ordinal);

        Assert.Contains(lines, l => l.Contains("input press→intent", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("render intent→visible", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("net send→local", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("server apply", StringComparison.Ordinal));
    }

    [Fact]
    public void ClientAndServer_WireOptInHooks_WithoutRewritingMovement()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Server", "Network", "PacketDispatcher.cs"));
        var movement = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Server", "Services", "MovementService.cs"));
        var logs = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Server", "Logging", "ServerNetworkLogs.cs"));

        Assert.Contains("FROG_MOVEMENT_MEASURE", shell, StringComparison.Ordinal);
        Assert.Contains("NoteKeyPress()", shell, StringComparison.Ordinal);
        Assert.Contains("NoteMoveIntent()", shell, StringComparison.Ordinal);
        Assert.Contains("NoteVisibleUpdate()", shell, StringComparison.Ordinal);
        Assert.Contains("NoteNetworkSend()", shell, StringComparison.Ordinal);
        Assert.Contains("NoteLocalCorrection()", shell, StringComparison.Ordinal);
        Assert.Contains("NoteOtherPlayerUpdate()", shell, StringComparison.Ordinal);
        Assert.Contains("NoteFrameTime", shell, StringComparison.Ordinal);
        Assert.Contains("MoveNetworkPulseMs = 52", shell, StringComparison.Ordinal);
        Assert.Contains("speedPxPerSec * dt", shell, StringComparison.Ordinal);

        Assert.Contains("MovementMeasureServerSink", dispatcher, StringComparison.Ordinal);
        Assert.Contains("TryApplyReportedPixelPosition", dispatcher, StringComparison.Ordinal);
        Assert.Contains("TryApplyMove", dispatcher, StringComparison.Ordinal);

        Assert.Contains("EventId = 5031", logs, StringComparison.Ordinal);
        Assert.Contains("movement_measure apply", logs, StringComparison.Ordinal);
        Assert.Contains("EventId = 5016", logs, StringComparison.Ordinal);

        Assert.Contains("PlayerMovePixelsPerRequest", movement, StringComparison.Ordinal);
        Assert.Contains("MaxPositionSyncPixelsPerSecond", movement, StringComparison.Ordinal);
        Assert.DoesNotContain("MovementMeasureProbe", movement, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasureBaselineDoc_HasRunbookAndPlaceholders()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "movement", "MEASURE-BASELINE.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Owner** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("FROG_MOVEMENT_MEASURE", text, StringComparison.Ordinal);
        Assert.Contains("$env:FROG_MOVEMENT_MEASURE = \"1\"", text, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project Frog.Client/Frog.Client.csproj", text, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project Frog.Server/Frog.Server.csproj", text, StringComparison.Ordinal);
        Assert.Contains("input_press_to_intent_ms", text, StringComparison.Ordinal);
        Assert.Contains("render_intent_to_visible_ms", text, StringComparison.Ordinal);
        Assert.Contains("net_send_to_local_correction_ms", text, StringComparison.Ordinal);
        Assert.Contains("net_other_player_update_interval_ms", text, StringComparison.Ordinal);
        Assert.Contains("frame_dt_ms", text, StringComparison.Ordinal);
        Assert.Contains("| _ |", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.Contains("No prediction rewrite", text, StringComparison.Ordinal);
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
