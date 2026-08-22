using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Production launcher + orchestrator path (not a hand-rolled ProcessStartInfo).
/// Server = real Frog.Server; READY client = committed headless (exact map/spawn).
/// Full Frog.Client GUI success path is covered on Windows smoke.
/// </summary>
public sealed class PlaytestProductionLauncherTests
{
    [Fact]
    public async Task OwnedLauncher_Orchestrator_ServerClientReady_ExactSpawn_Stop_NoOrphan_EnvIsolated()
    {
        var serverDll = ResolveServerDll();
        Assert.True(File.Exists(serverDll), $"missing {serverDll}");

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
            var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(60) };
            var orch = new PlaytestOrchestrator(preparer, launcher);

            var port = GetFreePort();
            var correlation = Guid.NewGuid();

            var sentinelDir = Path.Combine(Path.GetTempPath(), "frog-sentinel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sentinelDir);
            var sentinelFile = Path.Combine(sentinelDir, "do-not-delete.txt");
            await File.WriteAllTextAsync(sentinelFile, "sentinel");

            var headlessClient = ResolveHeadlessClientDll();
            Assert.True(File.Exists(headlessClient), $"missing {headlessClient}");

            var args = PlaytestOwnedProcessLauncher.BuildClientArguments(correlation, port);
            Assert.DoesNotContain("playtest-token", args, StringComparison.OrdinalIgnoreCase);

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
                clientExe: headlessClient);

            var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
            Assert.True(orch.ActiveSession!.IsActive);
            Assert.NotNull(orch.ActiveSession.Server);
            Assert.NotNull(orch.ActiveSession.Client);
            var serverPid = orch.ActiveSession.Server!.ProcessId;
            var clientPid = orch.ActiveSession.Client!.ProcessId;
            Assert.True(serverPid > 0);
            Assert.True(clientPid > 0);
            Assert.True(launcher.IsRunning(orch.ActiveSession.Server));
            Assert.True(launcher.IsRunning(orch.ActiveSession.Client));

            var (expectedPx, expectedPy) = WorldMetrics.TileCenterToPixels(1, 1);
            var logs = launcher.DrainLogsSnapshot();
            Assert.Contains(
                logs,
                l => PlaytestReadyMarker.TryValidateAgainstPlan(
                    l,
                    correlation,
                    success.Plan.Spawn,
                    out var ready,
                    out _)
                     && ready.PixelX == expectedPx
                     && ready.PixelY == expectedPy);
            Assert.Contains(
                logs,
                l => l.Contains("client-ready-observed", StringComparison.OrdinalIgnoreCase)
                     && l.Contains("tile=(1,1)", StringComparison.Ordinal));
            Assert.DoesNotContain(logs, l => l.Contains(success.Plan.AuthToken, StringComparison.Ordinal));
            Assert.DoesNotContain(logs, l => l.Contains("REDACTED_MUST_NOT_LEAK", StringComparison.Ordinal));
            Assert.DoesNotContain(logs, l => l.Contains("forbidden-env-present", StringComparison.OrdinalIgnoreCase));
            Assert.All(logs, l => Assert.DoesNotContain("--playtest-token", l, StringComparison.OrdinalIgnoreCase));

            var workDir = success.Plan.WorkDirectory;
            Assert.True(Directory.Exists(workDir));
            Assert.True(PlaytestWorkspacePaths.TryValidateOwnedWorkspace(workDir, correlation, out _));

            await orch.StopAsync();
            Assert.Null(orch.ActiveSession);
            Assert.False(launcher.HasOwnedProcesses);
            Assert.False(await IsPortOpenAsync(port));
            Assert.False(Directory.Exists(workDir), "owned workspace must be deleted by production cleanup");
            Assert.True(File.Exists(sentinelFile), "external sentinel must not be deleted");

            try
            {
                Directory.Delete(sentinelDir, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
        finally
        {
            foreach (var name in PlaytestChildEnvironment.ForbiddenVariableNames)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }
    }

    [Fact]
    public async Task OwnedLauncher_ClientEarlyExit_BeforeReady_LogsPidExitCode_AndSafeError()
    {
        var serverDll = ResolveServerDll();
        Assert.True(File.Exists(serverDll));
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(20) };
        var orch = new PlaytestOrchestrator(preparer, launcher);
        var port = GetFreePort();
        var correlation = Guid.NewGuid();

        var headless = ResolveHeadlessClientDll();
        // Launch via a thin wrapper args: we need --exit-before-ready on the dll.
        // Use a committed helper script that invokes dotnet with that flag… simpler: custom executable path
        // that is the headless dll, but StartClientAsync always passes --playtest args.
        // So use StartClientAsync directly with a modified approach: ProcessStartInfo override via
        // launching headless ourselves is not production path.
        // Instead: invoke orchestrator with a shim exe that is actually `dotnet` + dll + exit flag —
        // Build a tiny wrapper script that ignores extra args and runs exit-before-ready.
        var wrapper = WriteHeadlessExitBeforeReadyWrapper(headless);

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = correlation,
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 0,
                SpawnTileY = 0,
                RequireDurablePersistence = false,
            },
            serverExe: serverDll,
            clientExe: wrapper);

        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.True(
            failed.Kind is PlaytestFailureKind.LaunchFailure or PlaytestFailureKind.Timeout,
            failed.Kind + ": " + failed.Error);
        Assert.Null(orch.ActiveSession);
        Assert.False(launcher.HasOwnedProcesses);
        Assert.False(await IsPortOpenAsync(port));

        var logs = launcher.DrainLogsSnapshot();
        Assert.Contains(logs, l => l.Contains("started role=client pid=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, l => l.Contains("exited role=client", StringComparison.OrdinalIgnoreCase)
                                   && l.Contains("code=7", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, l => l.Contains("early-exit-before-ready", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, l => l.Contains("REDACTED_MUST_NOT_LEAK", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopAsync_ForceTimeout_RetainsOwnership_DoesNotDeleteWorkspace()
    {
        var serverDll = ResolveServerDll();
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new PlaytestOwnedProcessLauncher
        {
            ClientReadyTimeout = TimeSpan.FromSeconds(60),
            ProcessStopTimeout = TimeSpan.FromMilliseconds(50),
            ForceStopWaitTimeout = true,
        };
        var orch = new PlaytestOrchestrator(preparer, launcher);
        var port = GetFreePort();
        var correlation = Guid.NewGuid();
        var headless = ResolveHeadlessClientDll();

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
            },
            serverExe: serverDll,
            clientExe: headless);

        var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
        var workDir = success.Plan.WorkDirectory;
        Assert.True(Directory.Exists(workDir));

        await orch.StopAsync();

        Assert.True(launcher.HasOwnedProcesses, "ownership must be retained after stop failure");
        Assert.True(Directory.Exists(workDir), "workspace must not be deleted while owned processes remain");
        var logs = launcher.DrainLogsSnapshot();
        Assert.Contains(logs, l => l.Contains("stop-wait-timeout", StringComparison.OrdinalIgnoreCase)
                                   || l.Contains("ownership conservée", StringComparison.OrdinalIgnoreCase)
                                   || l.Contains("stop-error", StringComparison.OrdinalIgnoreCase));

        // Recover for process cleanup.
        launcher.ForceStopWaitTimeout = false;
        launcher.ProcessStopTimeout = TimeSpan.FromSeconds(10);
        await launcher.StopAllOwnedAsync();
        Assert.False(launcher.HasOwnedProcesses);
        if (Directory.Exists(workDir))
        {
            Assert.True(PlaytestWorkspacePaths.TryDeleteOwnedWorkspace(workDir, correlation, out _));
        }
    }

    [Fact]
    public void WorkspaceCleanup_RejectsExternalSentinelDirectory()
    {
        var sentinel = Path.Combine(Path.GetTempPath(), "frog-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sentinel);
        File.WriteAllText(Path.Combine(sentinel, "keep.txt"), "x");
        var corr = Guid.NewGuid();
        Assert.False(PlaytestWorkspacePaths.TryDeleteOwnedWorkspace(sentinel, corr, out var err));
        Assert.Contains("hors racine", err, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(sentinel));
        Directory.Delete(sentinel, recursive: true);
    }

    [Fact]
    public void LogSanitizer_RemovesFullSecretValues()
    {
        const string secret = "Host=super-secret-db;Password=hunter2";
        var raw = "FROG_POSTGRES_CONNECTION_STRING=" + secret + " more";
        var cleaned = PlaytestLogSanitizer.Sanitize(raw, [secret]);
        Assert.DoesNotContain(secret, cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", cleaned, StringComparison.Ordinal);
        Assert.Contains("FROG_POSTGRES_CONNECTION_STRING=***", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("***=", cleaned.Replace("FROG_POSTGRES_CONNECTION_STRING=***", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildClientArguments_NeverIncludesToken()
    {
        var token = PlaytestAuthToken.Create();
        var args = PlaytestOwnedProcessLauncher.BuildClientArguments(Guid.NewGuid(), 7777);
        Assert.DoesNotContain(token, args, StringComparison.Ordinal);
        Assert.DoesNotContain("playtest-token", args, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--playtest", args, StringComparison.Ordinal);
        Assert.Contains("--correlation", args, StringComparison.Ordinal);
    }

    private static string WriteHeadlessExitBeforeReadyWrapper(string headlessDll)
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(Path.GetTempPath(), "frog-exitready-" + Guid.NewGuid().ToString("N") + ".cmd");
            File.WriteAllText(
                path,
                $"@echo off\r\ndotnet \"{headlessDll}\" --exit-before-ready\r\nexit /b %ERRORLEVEL%\r\n");
            return path;
        }

        var sh = Path.Combine(Path.GetTempPath(), "frog-exitready-" + Guid.NewGuid().ToString("N") + ".sh");
        File.WriteAllText(sh, "#!/bin/sh\nexec dotnet \"" + headlessDll + "\" --exit-before-ready\n");
        Process.Start("chmod", $"+x \"{sh}\"")?.WaitForExit();
        return sh;
    }

    private static string ResolveHeadlessClientDll()
    {
        var baseDir = AppContext.BaseDirectory;
        var copied = Path.Combine(baseDir, "Frog.PlaytestHeadlessClient.dll");
        if (File.Exists(copied))
        {
            return copied;
        }

        foreach (var cfg in new[] { "Release", "Debug" })
        {
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir,
                "..",
                "..",
                "..",
                "..",
                "tests",
                "Frog.PlaytestHeadlessClient",
                "bin",
                cfg,
                "net8.0",
                "Frog.PlaytestHeadlessClient.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(
            baseDir,
            "..",
            "..",
            "..",
            "..",
            "tests",
            "Frog.PlaytestHeadlessClient",
            "bin",
            "Release",
            "net8.0",
            "Frog.PlaytestHeadlessClient.dll"));
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

        return Path.GetFullPath(Path.Combine(
            baseDir, "..", "..", "..", "..", "Frog.Server", "bin", "Release", "net8.0", "Frog.Server.dll"));
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
