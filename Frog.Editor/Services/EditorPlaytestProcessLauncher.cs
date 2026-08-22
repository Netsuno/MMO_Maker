using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Frog.Application.Playtest;
using Frog.Editor.Config;

namespace Frog.Editor.Services;

/// <summary>Résolution + lancement de <c>Frog.Server</c> pour playtest.</summary>
public static class EditorFrogServerLauncher
{
    private const string ServerExeFileName = "Frog.Server.exe";
    private const string ServerDllFileName = "Frog.Server.dll";

    public static bool TryResolveExecutable(out string exePath, out bool useDotnetDll)
    {
        exePath = string.Empty;
        useDotnetDll = false;
        if (EditorLocalWorkstate.TryReadServerExePath(out var saved) && File.Exists(saved))
        {
            exePath = saved;
            useDotnetDll = saved.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var exe = Path.GetFullPath(
                Path.Combine(baseDir, "..", "..", "..", "..", "Frog.Server", "bin", cfg, "net8.0", ServerExeFileName));
            if (File.Exists(exe))
            {
                exePath = exe;
                return true;
            }

            var dll = Path.GetFullPath(
                Path.Combine(baseDir, "..", "..", "..", "..", "Frog.Server", "bin", cfg, "net8.0", ServerDllFileName));
            if (File.Exists(dll))
            {
                exePath = dll;
                useDotnetDll = true;
                return true;
            }
        }

        return false;
    }

    public static int FindFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static async Task WaitForPortAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(500));
                await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            $"Le serveur playtest n’écoute pas sur {host}:{port} dans les délais. {(last?.Message ?? string.Empty)}");
    }
}

/// <summary>Implémentation éditeur de <see cref="IPlaytestProcessLauncher"/>.</summary>
public sealed class EditorPlaytestProcessLauncher : IPlaytestProcessLauncher
{
    private readonly Dictionary<int, Process> _owned = new();
    private readonly object _gate = new();

    public async Task<PlaytestProcessHandle> StartServerAsync(
        PlaytestServerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = request.Plan;
        var psi = CreateServerStartInfo(request.ExecutablePath, plan, request.Port);
        var process = StartOwned(psi);
        var handle = new PlaytestProcessHandle
        {
            ProcessId = process.Id,
            Role = "server",
            ExecutablePath = request.ExecutablePath,
        };

        try
        {
            await EditorFrogServerLauncher.WaitForPortAsync(
                    plan.Host,
                    request.Port,
                    request.ReadyTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await StopAsync(handle, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return handle;
    }

    public Task<PlaytestProcessHandle> StartClientAsync(
        PlaytestClientStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var args =
            $"--playtest --host {request.Host} --port {request.Port} --correlation {request.Plan.CorrelationId:N}";
        var psi = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        psi.Environment["FROG_PLAYTEST_CORRELATION_ID"] = request.Plan.CorrelationId.ToString("N");
        var process = StartOwned(psi);
        return Task.FromResult(new PlaytestProcessHandle
        {
            ProcessId = process.Id,
            Role = "client",
            ExecutablePath = request.ExecutablePath,
        });
    }

    public Task StopAsync(PlaytestProcessHandle handle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        Process? process;
        lock (_gate)
        {
            _owned.Remove(handle.ProcessId, out process);
        }

        if (process is null)
        {
            try
            {
                process = Process.GetProcessById(handle.ProcessId);
            }
            catch
            {
                return Task.CompletedTask;
            }
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // best-effort
        }
        finally
        {
            process.Dispose();
        }

        return Task.CompletedTask;
    }

    public bool IsRunning(PlaytestProcessHandle handle)
    {
        try
        {
            var p = Process.GetProcessById(handle.ProcessId);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public async Task StopAllOwnedAsync(CancellationToken cancellationToken = default)
    {
        List<PlaytestProcessHandle> handles;
        lock (_gate)
        {
            handles = _owned.Keys
                .Select(pid => new PlaytestProcessHandle
                {
                    ProcessId = pid,
                    Role = "owned",
                    ExecutablePath = string.Empty,
                })
                .ToList();
        }

        foreach (var h in handles)
        {
            await StopAsync(h, cancellationToken).ConfigureAwait(false);
        }
    }

    private Process StartOwned(ProcessStartInfo psi)
    {
        var process = Process.Start(psi)
                      ?? throw new InvalidOperationException("Impossible de démarrer le processus playtest.");
        lock (_gate)
        {
            _owned[process.Id] = process;
        }

        return process;
    }

    private static ProcessStartInfo CreateServerStartInfo(string executablePath, PlaytestLaunchPlan plan, int port)
    {
        ProcessStartInfo psi;
        if (executablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{executablePath}\"",
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        }

        psi.Environment[PlaytestRuntimeOptionsEnv.ManifestPath] = plan.ManifestPath;
        psi.Environment[PlaytestRuntimeOptionsEnv.CorrelationId] = plan.CorrelationId.ToString("N");
        psi.Environment[PlaytestRuntimeOptionsEnv.Port] = port.ToString();
        // Jamais transmettre de chaîne PostgreSQL au serveur playtest.
        psi.Environment.Remove("FROG_POSTGRES_CONNECTION_STRING");
        psi.Environment.Remove("FROG_POSTGRES_TEST_CONNECTION_STRING");
        return psi;
    }
}

/// <summary>Noms d’env partagés (évite une ref Server depuis Editor pour les constantes).</summary>
internal static class PlaytestRuntimeOptionsEnv
{
    public const string ManifestPath = "FROG_PLAYTEST_MANIFEST_PATH";
    public const string CorrelationId = "FROG_PLAYTEST_CORRELATION_ID";
    public const string Port = "FROG_PLAYTEST_PORT";
}
