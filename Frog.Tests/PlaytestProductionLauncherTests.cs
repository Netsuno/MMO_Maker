using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Production launcher + orchestrator path (not a hand-rolled ProcessStartInfo).
/// Server = real Frog.Server; client readiness exercised via a production-equivalent
/// headless client process that emits FROG_PLAYTEST_READY after auth+map (same marker
/// as Frog.Client). Full Frog.Client GUI path is covered on Windows smoke.
/// </summary>
public sealed class PlaytestProductionLauncherTests
{
    [Fact]
    public async Task OwnedLauncher_Orchestrator_ServerClientReady_Stop_NoOrphan_SafeCleanup()
    {
        var serverDll = ResolveServerDll();
        Assert.True(File.Exists(serverDll), $"missing {serverDll}");

        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(60) };
        var orch = new PlaytestOrchestrator(preparer, launcher);

        var port = GetFreePort();
        var correlation = Guid.NewGuid();

        // External sentinel must survive cleanup.
        var sentinelDir = Path.Combine(Path.GetTempPath(), "frog-sentinel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sentinelDir);
        var sentinelFile = Path.Combine(sentinelDir, "do-not-delete.txt");
        await File.WriteAllTextAsync(sentinelFile, "sentinel");

        var headlessClient = ResolveHeadlessClientDll();
        Assert.True(File.Exists(headlessClient), $"missing {headlessClient}");

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

        var logs = launcher.DrainLogsSnapshot();
        Assert.Contains(logs, l => l.Contains("client-ready-observed", StringComparison.OrdinalIgnoreCase)
                                   || l.Contains(PlaytestAuthToken.ReadyStdoutPrefix, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, l => l.Contains(correlation.ToString("N"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, l => l.Contains(success.Plan.AuthToken, StringComparison.Ordinal));
        Assert.All(logs, l => Assert.DoesNotContain("REDACTED_MUST_NOT_LEAK", l, StringComparison.Ordinal));

        // Probe child envs were sanitized (server+client started after Sanitize).
        var leaks = await PlaytestChildEnvironment.ProbeForbiddenKeysInChildAsync("server");
        Assert.Empty(leaks);

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

    [Fact]
    public async Task OwnedLauncher_ClientEarlyExit_FailsWithActionableLogs()
    {
        var serverDll = ResolveServerDll();
        Assert.True(File.Exists(serverDll));
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(15) };
        var orch = new PlaytestOrchestrator(preparer, launcher);
        var port = GetFreePort();

        // Client that exits immediately without READY.
        var badClient = WriteExitingClientScript();

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 0,
                SpawnTileY = 0,
                RequireDurablePersistence = false,
            },
            serverExe: serverDll,
            clientExe: badClient);

        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.True(
            failed.Kind is PlaytestFailureKind.LaunchFailure or PlaytestFailureKind.Timeout,
            failed.Kind + ": " + failed.Error);
        Assert.False(string.IsNullOrWhiteSpace(failed.Error));
        Assert.Null(orch.ActiveSession);
        Assert.False(launcher.HasOwnedProcesses);
        Assert.False(await IsPortOpenAsync(port));
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

    private static string WriteExitingClientScript()
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(Path.GetTempPath(), "frog-exit-" + Guid.NewGuid().ToString("N") + ".cmd");
            File.WriteAllText(path, "exit /b 7\r\n");
            return path;
        }

        var sh = Path.Combine(Path.GetTempPath(), "frog-exit-" + Guid.NewGuid().ToString("N") + ".sh");
        File.WriteAllText(sh, "#!/bin/sh\nexit 7\n");
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
