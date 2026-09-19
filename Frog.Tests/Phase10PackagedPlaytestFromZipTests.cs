using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// P10-3 : playtest production (orchestrator + launcher) depuis un <c>Frog.Server</c>
/// publié self-contained, copié hors de l’arbre git. Client = headless (Linux).
/// Frog.Client.exe / Frog.Editor.exe WinForms : <c>scripts/packaged-playtest-e2e.ps1</c>
/// (Hello + layouts frères). Menu Playtest WinForms : not proven on Linux agents.
/// </summary>
[Collection(PlaytestProcessCollectionDefinition.Name)]
public sealed class Phase10PackagedPlaytestFromZipTests
{
    [Fact]
    public async Task PackagedServerOutsideRepo_HeadlessClient_ReadyExactSpawn()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows unit budget already publishes client+editor+server in
            // packaged-playtest-e2e.ps1. This test is the Linux zip→READY proof.
            return;
        }

        var serverHost = await PublishServerOutsideRepoAsync();
        Assert.True(File.Exists(serverHost), "packaged Frog.Server missing: " + serverHost);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(serverHost, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch
            {
                // chmod best-effort
            }
        }

        var headless = ResolveHeadlessClientDll();
        Assert.True(File.Exists(headless), "headless client missing: " + headless);

        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(60) };
        var orch = new PlaytestOrchestrator(new PlaytestMapPreparer(repo), launcher);
        var port = GetFreePort();
        var correlation = Guid.NewGuid();

        try
        {
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
                serverExe: serverHost,
                clientExe: headless);

            if (result is PlaytestPreparationResult.Failed failed)
            {
                Assert.Fail($"{failed.Kind}: {failed.Error}\n{string.Join('\n', launcher.DrainLogsSnapshot())}");
            }

            var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
            var (px, py) = WorldMetrics.TileCenterToPixels(1, 1);
            Assert.Contains(
                launcher.DrainLogsSnapshot(),
                l => PlaytestReadyMarker.TryValidateAgainstPlan(
                         l, correlation, success.Plan.Spawn, out var ready, out _)
                     && ready.PixelX == px
                     && ready.PixelY == py);

            await orch.StopAsync();
            Assert.False(launcher.HasOwnedProcesses);
        }
        finally
        {
            try
            {
                await orch.StopAsync();
            }
            catch
            {
                // best-effort
            }
        }
    }

    private static async Task<string> PublishServerOutsideRepoAsync()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "publish-frog.sh");
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "frog-p10-3-playtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        Assert.False(
            outputRoot.StartsWith(repoRoot, StringComparison.Ordinal),
            "publish root must be outside the git tree");

        Assert.True(File.Exists(script), script);
        var startSh = new ProcessStartInfo
        {
            FileName = script,
            Arguments = $"--target server-linux-x64 --output-root \"{outputRoot}\" --force",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
        };
        using var pub = Process.Start(startSh) ?? throw new InvalidOperationException("publish-frog.sh failed to start");
        await pub.WaitForExitAsync();
        Assert.Equal(0, pub.ExitCode);
        return Path.Combine(outputRoot, "server-linux-x64", "Frog.Server");
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
                baseDir, "..", "..", "..", "..", "tests", "Frog.PlaytestHeadlessClient", "bin", cfg, "net8.0",
                "Frog.PlaytestHeadlessClient.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Frog.PlaytestHeadlessClient.dll");
    }

    private static string FindRepoRoot()
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

        throw new InvalidOperationException("Frog.Creator.sln not found");
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
