namespace Frog.Application.Playtest;

/// <summary>
/// Chemins relatifs documentés du paquet PC : dossiers frères
/// <c>editor-win-x64</c>, <c>client-win-x64</c> et <c>server-win-x64</c>
/// (souvent sous Frog-private-testing). Le playtest réutilise ces binaires ;
/// il ne lance pas <c>dotnet publish</c>.
/// </summary>
public static class PlaytestPublishLayouts
{
    public const string ClientWinFolder = "client-win-x64";
    public const string ServerWinFolder = "server-win-x64";
    public const string ServerLinuxFolder = "server-linux-x64";
    public const string EditorWinFolder = "editor-win-x64";
    public const string ClientExeFileName = "Frog.Client.exe";
    public const string ServerExeFileName = "Frog.Server.exe";
    public const string ServerDllFileName = "Frog.Server.dll";
    public const string ServerLinuxBinary = "Frog.Server";

    /// <summary>Même dossier, puis <c>../client-win-x64</c>, puis <c>bin/Debug|Release</c> du dépôt.</summary>
    public static IEnumerable<string> EnumerateClientCandidates(string editorDirectory)
    {
        var baseDir = TrimDirectory(editorDirectory);
        yield return Path.Combine(baseDir, ClientExeFileName);
        yield return Path.GetFullPath(Path.Combine(baseDir, "..", ClientWinFolder, ClientExeFileName));
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            yield return Path.GetFullPath(
                Path.Combine(baseDir, "..", "..", "..", "..", "Frog.Client", "bin", cfg, "net8.0-windows", ClientExeFileName));
        }
    }

    /// <summary>
    /// Même dossier, layouts frères <c>../server-win-x64</c> / <c>../server-linux-x64</c>,
    /// puis <c>bin/Debug|Release</c> du dépôt.
    /// </summary>
    public static IEnumerable<(string Path, bool UseDotnetDll)> EnumerateServerCandidates(string editorDirectory)
    {
        var baseDir = TrimDirectory(editorDirectory);
        yield return (Path.Combine(baseDir, ServerExeFileName), false);
        yield return (Path.Combine(baseDir, ServerDllFileName), true);
        yield return (Path.Combine(baseDir, ServerLinuxBinary), false);
        yield return (Path.GetFullPath(Path.Combine(baseDir, "..", ServerWinFolder, ServerExeFileName)), false);
        yield return (Path.GetFullPath(Path.Combine(baseDir, "..", ServerLinuxFolder, ServerLinuxBinary)), false);
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            yield return (Path.GetFullPath(
                Path.Combine(baseDir, "..", "..", "..", "..", "Frog.Server", "bin", cfg, "net8.0", ServerExeFileName)), false);
            yield return (Path.GetFullPath(
                Path.Combine(baseDir, "..", "..", "..", "..", "Frog.Server", "bin", cfg, "net8.0", ServerDllFileName)), true);
        }
    }

    /// <summary>
    /// Réutilise une paire déjà résolue si les deux fichiers existent encore.
    /// Sinon cherche les chemins relatifs documentés à partir du dossier éditeur.
    /// </summary>
    public static bool TryResolvePair(
        string editorDirectory,
        string? reuseClientExe,
        string? reuseServerExe,
        out string clientExe,
        out string serverExe,
        out bool serverUsesDotnetDll)
    {
        if (TryReusePair(reuseClientExe, reuseServerExe, out clientExe, out serverExe))
        {
            serverUsesDotnetDll = serverExe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        clientExe = string.Empty;
        serverExe = string.Empty;
        serverUsesDotnetDll = false;
        string? foundClient = null;
        foreach (var candidate in EnumerateClientCandidates(editorDirectory))
        {
            if (File.Exists(candidate))
            {
                foundClient = candidate;
                break;
            }
        }

        string? foundServer = null;
        var serverDll = false;
        foreach (var (path, useDll) in EnumerateServerCandidates(editorDirectory))
        {
            if (File.Exists(path))
            {
                foundServer = path;
                serverDll = useDll;
                break;
            }
        }

        if (foundClient is null || foundServer is null)
        {
            return false;
        }

        clientExe = Path.GetFullPath(foundClient);
        serverExe = Path.GetFullPath(foundServer);
        serverUsesDotnetDll = serverDll;
        return true;
    }

    public static bool TryReusePair(string? clientExe, string? serverExe, out string clientFullPath, out string serverFullPath)
    {
        clientFullPath = string.Empty;
        serverFullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(clientExe) || string.IsNullOrWhiteSpace(serverExe))
        {
            return false;
        }

        if (!File.Exists(clientExe) || !File.Exists(serverExe))
        {
            return false;
        }

        clientFullPath = Path.GetFullPath(clientExe);
        serverFullPath = Path.GetFullPath(serverExe);
        return true;
    }

    private static string TrimDirectory(string editorDirectory)
        => (editorDirectory ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
