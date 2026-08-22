using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Editor.Config;

namespace Frog.Editor.Services;

/// <summary>Résolution + utilitaires TCP pour playtest.</summary>
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

    /// <summary>Attente readiness playtest : TCP + Hello protocole (pas seulement un port ouvert).</summary>
    public static async Task WaitForPlaytestHelloAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
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
                cts.CancelAfter(TimeSpan.FromSeconds(2));
                await client.ConnectAsync(IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);
                await using var stream = client.GetStream();
                var lenBuf = new byte[4];
                await ReadExactAsync(stream, lenBuf, cts.Token).ConfigureAwait(false);
                var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
                if (len is <= 0 or > 1024 * 1024)
                {
                    throw new InvalidOperationException("Frame Hello playtest : longueur invalide.");
                }

                var payload = new byte[len];
                await ReadExactAsync(stream, payload, cts.Token).ConfigureAwait(false);
                if (!WireHello.TryParse(payload, out _, out var ver))
                {
                    throw new InvalidOperationException("Réponse serveur playtest : Hello invalide.");
                }

                if (ver != Frog.Core.Constants.FrogWireProtocol.Version)
                {
                    throw new InvalidOperationException(
                        $"Version protocole playtest {ver} ≠ attendue {Frog.Core.Constants.FrogWireProtocol.Version}.");
                }

                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            $"Le serveur playtest n’a pas émis Hello sur 127.0.0.1:{port}. {(last?.Message ?? string.Empty)}");
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }
    }
}

/// <summary>Implémentation éditeur de <see cref="IPlaytestProcessLauncher"/>.</summary>
public sealed class EditorPlaytestProcessLauncher : IPlaytestProcessLauncher
{
    private const int MaxLogChars = 64 * 1024;
    private readonly Dictionary<int, Process> _owned = new();
    private readonly ConcurrentQueue<string> _correlatedLogs = new();
    private readonly object _gate = new();
    private int _logChars;

    public IReadOnlyList<string> DrainLogsSnapshot()
        => _correlatedLogs.ToArray();

    public async Task<PlaytestProcessHandle> StartServerAsync(
        PlaytestServerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = request.Plan;
        var psi = CreateServerStartInfo(request.ExecutablePath, plan, request.Port);
        PlaytestChildEnvironment.Sanitize(psi.Environment);
        var process = StartOwned(psi, plan.CorrelationId, "server");
        var handle = new PlaytestProcessHandle
        {
            ProcessId = process.Id,
            Role = "server",
            ExecutablePath = request.ExecutablePath,
        };

        _ = Task.Run(() => WatchEarlyExitAsync(process, plan.CorrelationId, "server"), CancellationToken.None);

        try
        {
            await EditorFrogServerLauncher.WaitForPlaytestHelloAsync(
                    "127.0.0.1",
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

        if (process.HasExited)
        {
            await StopAsync(handle, CancellationToken.None).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Le serveur playtest s’est arrêté prématurément (code {process.ExitCode}). Voir les logs corrélés.");
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
            $"--playtest --host 127.0.0.1 --port {request.Port} --correlation {request.Plan.CorrelationId:N}";
        var psi = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        PlaytestChildEnvironment.Sanitize(psi.Environment);
        psi.Environment[PlaytestRuntimeOptionsEnv.CorrelationId] = request.Plan.CorrelationId.ToString("N");

        var process = StartOwned(psi, request.Plan.CorrelationId, "client");
        _ = Task.Run(() => WatchEarlyExitAsync(process, request.Plan.CorrelationId, "client"), CancellationToken.None);
        return Task.FromResult(new PlaytestProcessHandle
        {
            ProcessId = process.Id,
            Role = "client",
            ExecutablePath = request.ExecutablePath,
        });
    }

    public async Task StopAsync(PlaytestProcessHandle handle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        Process? process;
        lock (_gate)
        {
            if (!_owned.Remove(handle.ProcessId, out process))
            {
                // Ne jamais tuer un PID non détenu.
                AppendLog(Guid.Empty, $"ignore-stop-unowned pid={handle.ProcessId} role={handle.Role}");
                return;
            }
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                waitCts.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await process.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    AppendLog(Guid.Empty, $"stop-wait-timeout pid={handle.ProcessId} role={handle.Role}");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog(Guid.Empty, $"stop-error pid={handle.ProcessId}: {ex.Message}");
        }
        finally
        {
            try
            {
                process.CancelOutputRead();
                process.CancelErrorRead();
            }
            catch
            {
                // ignore
            }

            process.Dispose();
        }
    }

    public bool IsRunning(PlaytestProcessHandle handle)
    {
        lock (_gate)
        {
            if (!_owned.TryGetValue(handle.ProcessId, out var process))
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
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

    private Process StartOwned(ProcessStartInfo psi, Guid correlationId, string role)
    {
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                AppendLog(correlationId, $"[{role}:out] {e.Data}");
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                AppendLog(correlationId, $"[{role}:err] {e.Data}");
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Impossible de démarrer le processus playtest.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        lock (_gate)
        {
            _owned[process.Id] = process;
        }

        AppendLog(correlationId, $"started role={role} pid={process.Id}");
        return process;
    }

    private async Task WatchEarlyExitAsync(Process process, Guid correlationId, string role)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            AppendLog(correlationId, $"exited role={role} pid={process.Id} code={process.ExitCode}");
        }
        catch
        {
            // ignore
        }
    }

    private void AppendLog(Guid correlationId, string line)
    {
        var prefix = correlationId == Guid.Empty ? "" : $"[{correlationId:N}] ";
        var entry = prefix + line;
        if (entry.Length > 2000)
        {
            entry = entry[..2000] + "…";
        }

        // Never echo secrets if somehow present.
        foreach (var forbidden in PlaytestChildEnvironment.ForbiddenVariableNames)
        {
            if (entry.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                entry = entry.Replace(forbidden, forbidden + "=***", StringComparison.OrdinalIgnoreCase);
            }
        }

        lock (_gate)
        {
            if (_logChars > MaxLogChars)
            {
                return;
            }

            _logChars += entry.Length;
            _correlatedLogs.Enqueue(entry);
        }
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
            };
        }

        psi.Environment[PlaytestRuntimeOptionsEnv.ManifestPath] = plan.ManifestPath;
        psi.Environment[PlaytestRuntimeOptionsEnv.CorrelationId] = plan.CorrelationId.ToString("N");
        psi.Environment[PlaytestRuntimeOptionsEnv.Port] = port.ToString();
        psi.Environment[PlaytestRuntimeOptionsEnv.BindAddress] = "127.0.0.1";
        return psi;
    }
}

internal static class PlaytestRuntimeOptionsEnv
{
    public const string ManifestPath = "FROG_PLAYTEST_MANIFEST_PATH";
    public const string CorrelationId = "FROG_PLAYTEST_CORRELATION_ID";
    public const string Port = "FROG_PLAYTEST_PORT";
    public const string BindAddress = "FROG_PLAYTEST_BIND_ADDRESS";
}
