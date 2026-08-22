using System.Collections;
using System.Diagnostics;

namespace Frog.Application.Playtest;

/// <summary>
/// Sanitize l’environnement des processus enfants playtest.
/// Ne journalise jamais les valeurs secrètes — uniquement les noms de variables.
/// </summary>
public static class PlaytestChildEnvironment
{
    public static readonly string[] ForbiddenVariableNames =
    [
        "FROG_POSTGRES_CONNECTION_STRING",
        "FROG_POSTGRES_TEST_CONNECTION_STRING",
        "POSTGRES_PASSWORD",
        "PGPASSWORD",
        "ConnectionStrings__PostgreSql",
        "ConnectionStrings__DefaultConnection",
    ];

    /// <summary>Retire les variables base de données / éditeur des variables d’environnement du processus enfant.</summary>
    public static void Sanitize(IDictionary<string, string?> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);
        foreach (var name in ForbiddenVariableNames)
        {
            environmentVariables.Remove(name);
        }

        foreach (var key in environmentVariables.Keys.ToArray())
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (IsForbiddenKey(key))
            {
                environmentVariables.Remove(key);
            }
        }
    }

    /// <summary>Surcharge pour <see cref="System.Collections.Specialized.StringDictionary"/> / <see cref="IDictionary"/>.</summary>
    public static void Sanitize(IDictionary environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);
        foreach (var name in ForbiddenVariableNames)
        {
            if (environmentVariables.Contains(name))
            {
                environmentVariables.Remove(name);
            }
        }

        var keys = environmentVariables.Keys.Cast<object>().Select(k => k?.ToString() ?? string.Empty).ToArray();
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (IsForbiddenKey(key))
            {
                environmentVariables.Remove(key);
            }
        }
    }

    public static bool IsForbiddenKey(string key)
    {
        if (ForbiddenVariableNames.Any(f => string.Equals(f, key, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (key.Contains("FROG_POSTGRES", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return key.Contains("POSTGRES", StringComparison.OrdinalIgnoreCase)
               && key.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lance un processus enfant (rôle serveur ou client) qui n’imprime que les <b>noms</b>
    /// de variables d’environnement après sanitization. Retourne les noms interdits encore présents.
    /// </summary>
    public static Task<IReadOnlyList<string>> ProbeForbiddenKeysInChildAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        var psi = CreateEnvNameListStartInfo();
        foreach (var name in ForbiddenVariableNames)
        {
            psi.Environment[name] = "REDACTED_MUST_NOT_LEAK";
        }

        // Simule aussi une variante éditeur fréquente.
        psi.Environment["FROG_POSTGRES_CONNECTION_STRING_EXTRA"] = "REDACTED_MUST_NOT_LEAK";
        Sanitize(psi.Environment);
        return ProbeStartedChildAsync(psi, role, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ProbeStartedChildAsync(
        ProcessStartInfo psi,
        string role,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Impossible de démarrer la sonde d’environnement playtest ({role}).");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        _ = await stderrTask.ConfigureAwait(false);

        // Ne jamais conserver/imprimer les valeurs — uniquement les noms.
        var names = stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var eq = line.IndexOf('=');
                return eq < 0 ? line : line[..eq];
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ForbiddenVariableNames
            .Where(f => names.Contains(f))
            .ToArray();
    }

    private static ProcessStartInfo CreateEnvNameListStartInfo()
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c set",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
        }

        return new ProcessStartInfo
        {
            FileName = "/usr/bin/env",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
    }
}
