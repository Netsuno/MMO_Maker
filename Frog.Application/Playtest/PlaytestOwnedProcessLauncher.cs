using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Protocol;

namespace Frog.Application.Playtest;

/// <summary>
/// Lanceur production playtest (serveur + client) : sanitize env, drain logs, Hello readiness,
/// attente readiness client (map+spawn exact), kill owned-only. Utilisable hors WPF (tests / éditeur).
/// </summary>
public sealed class PlaytestOwnedProcessLauncher : IPlaytestProcessLauncher
{
    public const string ReadyMarkerPrefix = PlaytestAuthToken.ReadyStdoutPrefix;
    private const int MaxLogChars = 64 * 1024;

    private readonly Dictionary<int, Process> _owned = new();
    private readonly ConcurrentQueue<string> _correlatedLogs = new();
    private readonly List<string> _knownSecrets = new();
    private readonly object _gate = new();
    private int _logChars;

    public TimeSpan ClientReadyTimeout { get; set; } = TimeSpan.FromSeconds(45);
    public TimeSpan ProcessStopTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Test seam: force WaitForExit to time out after Kill (ownership retained).</summary>
    public bool ForceStopWaitTimeout { get; set; }

    public bool HasOwnedProcesses
    {
        get
        {
            lock (_gate)
            {
                return _owned.Count > 0;
            }
        }
    }

    public IReadOnlyList<string> DrainLogsSnapshot()
        => _correlatedLogs.ToArray();

    public void RegisterSecretForRedaction(string? secret)
    {
        if (!string.IsNullOrEmpty(secret) && secret.Length >= 4)
        {
            lock (_gate)
            {
                _knownSecrets.Add(secret);
            }
        }
    }

    public async Task<PlaytestProcessHandle> StartServerAsync(
        PlaytestServerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = request.Plan;
        RegisterSecretForRedaction(plan.AuthToken);
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
            await WaitForPlaytestHelloAsync("127.0.0.1", request.Port, request.ReadyTimeout, cancellationToken)
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
                "Le serveur playtest s’est arrêté prématurément. " + FormatRecentLogs());
        }

        return handle;
    }

    public async Task<PlaytestProcessHandle> StartClientAsync(
        PlaytestClientStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        RegisterSecretForRedaction(request.Plan.AuthToken);

        var args = BuildClientArguments(request.Plan.CorrelationId, request.Port);
        if (!string.IsNullOrEmpty(request.Plan.AuthToken)
            && args.Contains(request.Plan.AuthToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Le jeton playtest ne doit jamais figurer dans la ligne de commande.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (request.ExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "dotnet";
            psi.Arguments = $"\"{request.ExecutablePath}\" {args}";
            psi.WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath) ?? Environment.CurrentDirectory;
        }

        PlaytestChildEnvironment.Sanitize(psi.Environment);
        psi.Environment[PlaytestRuntimeEnv.CorrelationId] = request.Plan.CorrelationId.ToString("N");
        if (!string.IsNullOrEmpty(request.Plan.AuthToken))
        {
            // Env only — never command line.
            psi.Environment[PlaytestAuthToken.EnvironmentVariable] = request.Plan.AuthToken;
        }

        var process = StartOwned(psi, request.Plan.CorrelationId, "client");
        var handle = new PlaytestProcessHandle
        {
            ProcessId = process.Id,
            Role = "client",
            ExecutablePath = request.ExecutablePath,
        };

        _ = Task.Run(() => WatchEarlyExitAsync(process, request.Plan.CorrelationId, "client"), CancellationToken.None);

        try
        {
            await WaitForClientReadyAsync(
                    process,
                    request.Plan,
                    ClientReadyTimeout,
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

    /// <summary>Arguments client playtest — sans jeton (env uniquement).</summary>
    public static string BuildClientArguments(Guid correlationId, int port)
        => $"--playtest --host 127.0.0.1 --port {port} --correlation {correlationId:N}";

    public async Task StopAsync(PlaytestProcessHandle handle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        Process? process;
        lock (_gate)
        {
            if (!_owned.TryGetValue(handle.ProcessId, out process))
            {
                AppendLog(Guid.Empty, $"ignore-stop-unowned pid={handle.ProcessId} role={handle.Role}");
                return;
            }
        }

        var terminated = false;
        try
        {
            if (process.HasExited)
            {
                terminated = true;
            }
            else
            {
                process.Kill(entireProcessTree: true);
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                waitCts.CancelAfter(ProcessStopTimeout);
                try
                {
                    if (ForceStopWaitTimeout)
                    {
                        // Test seam: always surface stop failure and retain ownership.
                        AppendLog(Guid.Empty, $"stop-wait-timeout pid={handle.ProcessId} role={handle.Role}");
                        throw new InvalidOperationException(
                            $"Échec arrêt processus playtest pid={handle.ProcessId} role={handle.Role} (ownership conservée). "
                            + FormatRecentLogs());
                    }

                    await process.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
                    terminated = process.HasExited;
                }
                catch (OperationCanceledException)
                {
                    AppendLog(Guid.Empty, $"stop-wait-timeout pid={handle.ProcessId} role={handle.Role}");
                    terminated = process.HasExited;
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog(Guid.Empty, $"stop-error pid={handle.ProcessId}: {ex.Message}");
            terminated = false;
        }

        if (!terminated)
        {
            // Retain ownership for retry — do not dispose / remove.
            throw new InvalidOperationException(
                $"Échec arrêt processus playtest pid={handle.ProcessId} role={handle.Role} (ownership conservée). "
                + FormatRecentLogs());
        }

        lock (_gate)
        {
            _owned.Remove(handle.ProcessId);
        }

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

        Exception? first = null;
        foreach (var h in handles)
        {
            try
            {
                await StopAsync(h, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                first ??= ex;
            }
        }

        if (first is not null)
        {
            throw first;
        }
    }

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

                if (ver != FrogWireProtocol.Version)
                {
                    throw new InvalidOperationException(
                        $"Version protocole playtest {ver} ≠ attendue {FrogWireProtocol.Version}.");
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

    private async Task WaitForClientReadyAsync(
        Process process,
        PlaytestLaunchPlan plan,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? lastReject = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    "Le client playtest s’est arrêté avant la readiness. " + FormatRecentLogs());
            }

            foreach (var line in DrainLogsSnapshot())
            {
                if (!line.Contains(ReadyMarkerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (PlaytestReadyMarker.TryValidateAgainstPlan(
                        line,
                        plan.CorrelationId,
                        plan.Spawn,
                        out var values,
                        out var error))
                {
                    AppendLog(
                        plan.CorrelationId,
                        $"client-ready-observed map={values.RuntimeMapId} tile=({values.TileX},{values.TileY}) pixel=({values.PixelX},{values.PixelY})");
                    return;
                }

                lastReject = error;
                AppendLog(plan.CorrelationId, "client-ready-rejected: " + (error ?? "invalid"));
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "Délai dépassé : client playtest non prêt (auth + carte + spawn exact). "
            + (lastReject is null ? "" : "Dernier rejet: " + lastReject + " ")
            + FormatRecentLogs());
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
        string[] secrets;
        lock (_gate)
        {
            secrets = _knownSecrets.ToArray();
        }

        var prefix = correlationId == Guid.Empty ? "" : $"[{correlationId:N}] ";
        var entry = PlaytestLogSanitizer.Sanitize(prefix + line, secrets);
        if (entry.Length > 2000)
        {
            entry = entry[..2000] + "…";
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

    private string FormatRecentLogs()
    {
        var lines = DrainLogsSnapshot();
        if (lines.Count == 0)
        {
            return "(aucun log corrélé)";
        }

        var take = Math.Min(40, lines.Count);
        return "Logs:\n" + string.Join('\n', lines.Skip(lines.Count - take));
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

        psi.Environment[PlaytestRuntimeEnv.ManifestPath] = plan.ManifestPath;
        psi.Environment[PlaytestRuntimeEnv.CorrelationId] = plan.CorrelationId.ToString("N");
        psi.Environment[PlaytestRuntimeEnv.Port] = port.ToString();
        psi.Environment[PlaytestRuntimeEnv.BindAddress] = "127.0.0.1";
        if (!string.IsNullOrEmpty(plan.AuthToken))
        {
            psi.Environment[PlaytestAuthToken.EnvironmentVariable] = plan.AuthToken;
        }

        return psi;
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

/// <summary>Noms d’env runtime playtest (serveur).</summary>
public static class PlaytestRuntimeEnv
{
    public const string ManifestPath = "FROG_PLAYTEST_MANIFEST_PATH";
    public const string CorrelationId = "FROG_PLAYTEST_CORRELATION_ID";
    public const string Port = "FROG_PLAYTEST_PORT";
    public const string BindAddress = "FROG_PLAYTEST_BIND_ADDRESS";
}
