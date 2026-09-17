using System.IO;
using System.Net;
using System.Net.Sockets;
using Frog.Application.Playtest;
using Frog.Editor.Config;

namespace Frog.Editor.Services;

/// <summary>Résolution chemins serveur/client + utilitaires TCP playtest.</summary>
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

    public static Task WaitForPlaytestHelloAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => PlaytestOwnedProcessLauncher.WaitForPlaytestHelloAsync(host, port, timeout, cancellationToken);
}

/// <summary>Implémentation éditeur : délègue au lanceur production <see cref="PlaytestOwnedProcessLauncher"/>.</summary>
public sealed class EditorPlaytestProcessLauncher : IPlaytestProcessLauncher
{
    private readonly PlaytestOwnedProcessLauncher _inner = new();

    public IReadOnlyList<string> DrainLogsSnapshot() => _inner.DrainLogsSnapshot();

    public bool HasOwnedProcesses => _inner.HasOwnedProcesses;

    public Task<PlaytestProcessHandle> StartServerAsync(
        PlaytestServerStartRequest request,
        CancellationToken cancellationToken = default)
        => _inner.StartServerAsync(request, cancellationToken);

    public Task<PlaytestProcessHandle> StartClientAsync(
        PlaytestClientStartRequest request,
        CancellationToken cancellationToken = default)
        => _inner.StartClientAsync(request, cancellationToken);

    public Task StopAsync(PlaytestProcessHandle handle, CancellationToken cancellationToken = default)
        => _inner.StopAsync(handle, cancellationToken);

    public bool IsRunning(PlaytestProcessHandle handle) => _inner.IsRunning(handle);

    public Task StopAllOwnedAsync(CancellationToken cancellationToken = default)
        => _inner.StopAllOwnedAsync(cancellationToken);
}
