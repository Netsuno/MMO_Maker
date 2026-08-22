using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Real Frog.Server + Frog.Client.exe success path via production launcher/orchestrator.
/// No screenshots — READY marker proves auth + exact map/spawn.
/// </summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class ClientPlaytestSuccessSmokeTests
{
    [Fact]
    public async Task FrogClient_PlaytestAutoStart_ExactSpawn_Ready_CleanShutdown()
    {
        var serverDll = ResolveServerDll();
        var clientExe = ResolveClientExe();
        Assert.True(File.Exists(serverDll), $"missing server {serverDll}");
        Assert.True(File.Exists(clientExe), $"missing client {clientExe}");

        foreach (var name in PlaytestChildEnvironment.ForbiddenVariableNames)
        {
            Environment.SetEnvironmentVariable(name, "REDACTED_MUST_NOT_LEAK");
        }

        try
        {
            var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
            var workspace = new MapWorkspaceSession(repo);
            await workspace.InitializeAsync();
            var preparer = new PlaytestMapPreparer(repo);
            var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(90) };
            var orch = new PlaytestOrchestrator(preparer, launcher);
            var port = GetFreePort();
            var correlation = Guid.NewGuid();

            var argsProbe = PlaytestOwnedProcessLauncher.BuildClientArguments(correlation, port);
            Assert.DoesNotContain("playtest-token", argsProbe, StringComparison.OrdinalIgnoreCase);

            var result = await orch.StartAsync(
                workspace,
                new PlaytestPrepareRequest
                {
                    CorrelationId = correlation,
                    Host = "127.0.0.1",
                    Port = port,
                    SpawnTileX = 1,
                    SpawnTileY = 1,
                    RequireDurablePersistence = false,
                    PublishCurrentBeforeLaunch = true,
                },
                serverExe: serverDll,
                clientExe: clientExe);

            var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
            Assert.True(orch.ActiveSession!.IsActive);
            Assert.True(launcher.IsRunning(orch.ActiveSession.Server!));
            Assert.True(launcher.IsRunning(orch.ActiveSession.Client!));

            var (px, py) = WorldMetrics.TileCenterToPixels(1, 1);
            var logs = launcher.DrainLogsSnapshot();
            Assert.Contains(
                logs,
                l => PlaytestReadyMarker.TryValidateAgainstPlan(
                         l,
                         correlation,
                         success.Plan.Spawn,
                         out var ready,
                         out _)
                     && ready.PixelX == px
                     && ready.PixelY == py);
            Assert.DoesNotContain(logs, l => l.Contains(success.Plan.AuthToken, StringComparison.Ordinal));
            Assert.DoesNotContain(logs, l => l.Contains("REDACTED_MUST_NOT_LEAK", StringComparison.Ordinal));
            Assert.DoesNotContain(logs, l => l.Contains("forbidden-env-present", StringComparison.OrdinalIgnoreCase));

            var workDir = success.Plan.WorkDirectory;
            var serverPid = orch.ActiveSession.Server!.ProcessId;
            var clientPid = orch.ActiveSession.Client!.ProcessId;

            await orch.StopAsync();
            Assert.Null(orch.ActiveSession);
            Assert.False(launcher.HasOwnedProcesses);
            Assert.False(await IsPortOpenAsync(port));
            Assert.False(Directory.Exists(workDir));
            Assert.False(IsProcessAlive(serverPid));
            Assert.False(IsProcessAlive(clientPid));
        }
        finally
        {
            foreach (var name in PlaytestChildEnvironment.ForbiddenVariableNames)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveServerDll()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var cfg in new[] { "Release", "Debug" })
        {
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "Frog.Server", "bin", cfg, "net8.0", "Frog.Server.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Frog.Server.dll");
    }

    private static string ResolveClientExe()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var cfg in new[] { "Release", "Debug" })
        {
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "Frog.Client", "bin", cfg, "net8.0-windows", "Frog.Client.exe"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Frog.Client.exe");
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static async Task<bool> IsPortOpenAsync(int port)
    {
        try
        {
            using var c = new TcpClient();
            using var cts = new CancellationTokenSource(200);
            await c.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
