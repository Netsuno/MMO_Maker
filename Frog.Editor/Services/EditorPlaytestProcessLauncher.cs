using System.IO;
using System.Net;
using System.Net.Sockets;
using Frog.Application.Playtest;
using Frog.Editor.Config;

namespace Frog.Editor.Services;

/// <summary>Résolution chemins serveur/client + utilitaires TCP playtest.</summary>
public static class EditorFrogServerLauncher
{
    public static bool TryResolveExecutable(out string exePath, out bool useDotnetDll)
        => TryResolveExecutable(AppContext.BaseDirectory, out exePath, out useDotnetDll);

    /// <summary>
    /// P10-3 / P10-6 : même dossier, layouts frères <c>../server-win-x64</c> /
    /// <c>../server-linux-x64</c>, puis <c>bin/Debug|Release</c> du dépôt.
    /// Un second test réutilise ces exécutables sans republier le client.
    /// </summary>
    public static bool TryResolveExecutable(string searchBaseDirectory, out string exePath, out bool useDotnetDll)
    {
        exePath = string.Empty;
        useDotnetDll = false;
        if (IsSameDirectory(searchBaseDirectory, AppContext.BaseDirectory)
            && EditorLocalWorkstate.TryReadServerExePath(out var saved)
            && File.Exists(saved))
        {
            exePath = saved;
            useDotnetDll = saved.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        foreach (var (path, useDll) in EnumerateServerCandidates(searchBaseDirectory))
        {
            if (File.Exists(path))
            {
                exePath = path;
                useDotnetDll = useDll;
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<(string Path, bool UseDotnetDll)> EnumerateServerCandidates(string searchBaseDirectory)
        => PlaytestPublishLayouts.EnumerateServerCandidates(searchBaseDirectory);

    private static bool IsSameDirectory(string a, string b)
        => string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

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
