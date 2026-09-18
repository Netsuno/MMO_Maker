using System.Diagnostics;
using System.Text;

namespace Frog.Persistence.IntegrationTests.Support;

/// <summary>Invokes repo <c>scripts/postgres-*.sh</c> (or <c>.ps1</c> on Windows).</summary>
internal static class PostgresBackupScripts
{
    public static readonly string[] ProductSchemas = ["auth", "content", "ops", "player", "world"];

    public static string RepoRoot { get; } = FindRepoRoot();

    public static void AssertClientToolsAvailable()
    {
        foreach (var tool in new[] { "pg_dump", "pg_restore", "psql" })
        {
            Assert.True(
                CommandExists(tool),
                tool + " is not on PATH. Install postgresql-client (see BACKUP_RESTORE_RUNBOOK.md).");
        }
    }

    public static Task BackupAsync(string connectionString, string outputPath, CancellationToken cancellationToken = default)
        => RunScriptAsync(
            "postgres-backup",
            ["--connection", connectionString, "--output", outputPath, "--force"],
            cancellationToken);

    public static Task RestoreAsync(string connectionString, string inputPath, CancellationToken cancellationToken = default)
        => RunScriptAsync(
            "postgres-restore",
            ["--connection", connectionString, "--input", inputPath],
            cancellationToken);

    public static Task VerifyAsync(string connectionString, CancellationToken cancellationToken = default)
        => RunScriptAsync(
            "postgres-verify",
            ["--connection", connectionString],
            cancellationToken);

    private static async Task RunScriptAsync(
        string scriptBaseName,
        IReadOnlyList<string> scriptArgs,
        CancellationToken cancellationToken)
    {
        var isWindows = OperatingSystem.IsWindows();
        var script = Path.Combine(RepoRoot, "scripts", scriptBaseName + (isWindows ? ".ps1" : ".sh"));
        Assert.True(File.Exists(script), "missing script: " + script);

        var start = new ProcessStartInfo
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (isWindows)
        {
            start.FileName = ResolveWindowsShell();
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
            if (scriptBaseName == "postgres-restore")
            {
                // PowerShell restore uses -InputPath (reserved $Input).
                var mapped = MapRestoreArgsForPowerShell(scriptArgs);
                foreach (var arg in mapped)
                {
                    start.ArgumentList.Add(arg);
                }
            }
            else
            {
                foreach (var arg in scriptArgs)
                {
                    start.ArgumentList.Add(arg);
                }
            }
        }
        else
        {
            start.FileName = "bash";
            start.ArgumentList.Add(script);
            foreach (var arg in scriptArgs)
            {
                start.ArgumentList.Add(arg);
            }
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("failed to start " + script);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
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
                // best-effort
            }

            throw new TimeoutException(
                "timed out running " + script + Environment.NewLine + stdout + stderr);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                script + " exited " + process.ExitCode + Environment.NewLine
                + stdout + stderr);
        }
    }

    private static IReadOnlyList<string> MapRestoreArgsForPowerShell(IReadOnlyList<string> scriptArgs)
    {
        var mapped = new List<string>(scriptArgs.Count);
        for (var i = 0; i < scriptArgs.Count; i++)
        {
            if (scriptArgs[i] == "--input" && i + 1 < scriptArgs.Count)
            {
                mapped.Add("-InputPath");
                mapped.Add(scriptArgs[i + 1]);
                i++;
                continue;
            }

            if (scriptArgs[i] == "--connection" && i + 1 < scriptArgs.Count)
            {
                mapped.Add("-Connection");
                mapped.Add(scriptArgs[i + 1]);
                i++;
                continue;
            }

            mapped.Add(scriptArgs[i]);
        }

        return mapped;
    }

    private static string ResolveWindowsShell()
    {
        if (CommandExists("pwsh"))
        {
            return "pwsh";
        }

        if (CommandExists("powershell"))
        {
            return "powershell";
        }

        throw new InvalidOperationException("pwsh/powershell not found to run postgres-*.ps1");
    }

    private static bool CommandExists(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".bat", ".cmd", ".com", "" }
            : new[] { "" };
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
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

        throw new InvalidOperationException("Could not locate Frog.Creator.sln.");
    }
}
