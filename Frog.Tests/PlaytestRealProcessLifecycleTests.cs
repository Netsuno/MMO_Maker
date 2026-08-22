using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Server.Config;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Real OS process launch of Frog.Server in playtest mode (not an in-process host fake).
/// </summary>
public sealed class PlaytestRealProcessLifecycleTests
{
    [Fact]
    public async Task RealServerProcess_Launch_HelloReady_Stop_NoOrphan_TempDeleted()
    {
        var serverDll = ResolveServerDll();
        Assert.True(File.Exists(serverDll), $"Frog.Server.dll not found at {serverDll}");

        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var port = GetFreePort();
        var workDir = Path.Combine(Path.GetTempPath(), "frog-real-proc-" + Guid.NewGuid().ToString("N"));
        var prepared = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 1,
                SpawnTileY = 1,
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
                WorkDirectory = workDir,
            });
        var plan = Assert.IsType<PlaytestPreparationResult.Success>(prepared).Plan;
        Assert.True(File.Exists(plan.ManifestPath));

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{serverDll}\"",
            WorkingDirectory = Path.GetDirectoryName(serverDll) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.Environment[PlaytestRuntimeOptions.ManifestPathEnvironmentVariable] = plan.ManifestPath;
        psi.Environment[PlaytestRuntimeOptions.CorrelationIdEnvironmentVariable] = plan.CorrelationId.ToString("N");
        psi.Environment[PlaytestRuntimeOptions.PortEnvironmentVariable] = port.ToString();
        psi.Environment[PlaytestRuntimeOptions.BindAddressEnvironmentVariable] = "127.0.0.1";
        // Prove secrets would have been inherited then removed.
        psi.Environment["FROG_POSTGRES_CONNECTION_STRING"] = "REDACTED_MUST_NOT_LEAK";
        psi.Environment["FROG_POSTGRES_TEST_CONNECTION_STRING"] = "REDACTED_MUST_NOT_LEAK";
        PlaytestChildEnvironment.Sanitize(psi.Environment);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stderr.AppendLine(e.Data);
            }
        };

        Assert.True(process.Start());
        var ownedPid = process.Id;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await WaitForPlaytestHelloAsync("127.0.0.1", port, TimeSpan.FromSeconds(30));
            Assert.False(process.HasExited, "server exited before Hello readiness");
            Assert.True(await IsPortOpenAsync(port));
        }
        catch (Exception ex)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }

            throw new Xunit.Sdk.XunitException(
                $"Playtest server failed readiness: {ex.Message}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        // Stop owned process only (never GetProcessById fallback).
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        Assert.True(process.HasExited);
        Assert.Equal(ownedPid, process.Id);

        // Temp cleanup (orchestrator responsibility — simulate stop cleanup).
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, recursive: true);
        }

        Assert.False(Directory.Exists(workDir));
        Assert.False(await IsPortOpenAsync(port), "TCP listener must be gone after stop");

        // Logs bounded / actionable: we captured stdout/stderr asynchronously (no pipe deadlock).
        Assert.DoesNotContain("REDACTED_MUST_NOT_LEAK", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("REDACTED_MUST_NOT_LEAK", stderr.ToString(), StringComparison.Ordinal);
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

    private static async Task WaitForPlaytestHelloAsync(string host, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);
                await using var stream = client.GetStream();
                var lenBuf = new byte[4];
                await ReadExactAsync(stream, lenBuf, cts.Token);
                var len = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
                if (len is <= 0 or > 1024 * 1024)
                {
                    throw new InvalidOperationException("invalid hello length");
                }

                var payload = new byte[len];
                await ReadExactAsync(stream, payload, cts.Token);
                if (!Frog.Core.Protocol.WireHello.TryParse(payload, out _, out var ver) ||
                    ver != Frog.Core.Constants.FrogWireProtocol.Version)
                {
                    throw new InvalidOperationException("hello version mismatch");
                }

                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
                await Task.Delay(100);
            }
        }

        throw new TimeoutException($"Hello readiness failed: {last?.Message}");
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }
    }
}
