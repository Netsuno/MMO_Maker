using System.Security.Cryptography;
using System.Text;

namespace Frog.Application.Playtest;

/// <summary>
/// Racine canonique des workspaces playtest. Seuls les dossiers créés ici avec marqueur d’ownership
/// peuvent être effacés par l’orchestrateur.
/// </summary>
public static class PlaytestWorkspacePaths
{
    public const string OwnershipMarkerFileName = ".frog-playtest-owned";
    public const string RootDirectoryName = "frog-playtest";

    public static string GetCanonicalRoot()
        => Path.GetFullPath(Path.Combine(Path.GetTempPath(), RootDirectoryName));

    /// <summary>Crée un workspace owned sous la racine canonique (jamais un chemin arbitraire).</summary>
    public static string CreateOwnedWorkspace(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("CorrelationId playtest requis.", nameof(correlationId));
        }

        var root = GetCanonicalRoot();
        Directory.CreateDirectory(root);
        var dir = Path.GetFullPath(Path.Combine(root, correlationId.ToString("N")));
        if (!IsStrictlyUnderRoot(dir, root))
        {
            throw new InvalidOperationException("Workspace playtest hors racine canonique.");
        }

        Directory.CreateDirectory(dir);
        var markerPath = Path.Combine(dir, OwnershipMarkerFileName);
        File.WriteAllText(markerPath, correlationId.ToString("N"), Encoding.UTF8);
        return dir;
    }

    public static bool TryValidateOwnedWorkspace(string? path, Guid correlationId, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "WorkDirectory playtest manquant.";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            error = "WorkDirectory playtest invalide: " + ex.Message;
            return false;
        }

        var root = GetCanonicalRoot();
        if (!IsStrictlyUnderRoot(full, root))
        {
            error = "WorkDirectory playtest hors racine canonique — suppression refusée.";
            return false;
        }

        if (IsReparsePoint(full) || IsReparsePoint(root))
        {
            error = "WorkDirectory playtest sur reparse/symlink — suppression refusée.";
            return false;
        }

        var marker = Path.Combine(full, OwnershipMarkerFileName);
        if (!File.Exists(marker))
        {
            error = "Marqueur d’ownership playtest manquant — suppression refusée.";
            return false;
        }

        var claimed = File.ReadAllText(marker).Trim();
        if (!string.Equals(claimed, correlationId.ToString("N"), StringComparison.OrdinalIgnoreCase))
        {
            error = "Marqueur d’ownership playtest ne correspond pas à la corrélation — suppression refusée.";
            return false;
        }

        return true;
    }

    /// <summary>Supprime uniquement un workspace owned vérifié. Ne touche jamais un chemin externe.</summary>
    public static bool TryDeleteOwnedWorkspace(string? path, Guid correlationId, out string? error)
    {
        if (!TryValidateOwnedWorkspace(path, correlationId, out error))
        {
            return false;
        }

        var full = Path.GetFullPath(path!);
        try
        {
            Directory.Delete(full, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            error = "Échec nettoyage workspace playtest: " + ex.Message;
            return false;
        }
    }

    public static bool IsStrictlyUnderRoot(string fullPath, string rootFullPath)
    {
        var path = Path.GetFullPath(fullPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetFullPath(rootFullPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return attrs.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Jeton playtest éphémère (jamais journalisé).</summary>
public static class PlaytestAuthToken
{
    public const string Username = "__frog_playtest__";
    public const string EnvironmentVariable = "FROG_PLAYTEST_AUTH_TOKEN";
    public const string ReadyStdoutPrefix = "FROG_PLAYTEST_READY";

    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    public static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    /// <summary>
    /// Réservé quelle que soit la casse (même règle que comptes/sessions : OrdinalIgnoreCase).
    /// </summary>
    public static bool IsReservedUsername(string? username)
        => Frog.Core.Identity.AccountUsername.Equals(username, Username);
}

/// <summary>Redaction de secrets dans les logs (retire la valeur complète, jamais un suffixe exposé).</summary>
public static class PlaytestLogSanitizer
{
    public static string Sanitize(string? line, IEnumerable<string>? knownSecretValues = null)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line ?? string.Empty;
        }

        var result = line;

        // First: remove any known secret substrings entirely.
        if (knownSecretValues is not null)
        {
            foreach (var secret in knownSecretValues)
            {
                if (string.IsNullOrEmpty(secret) || secret.Length < 4)
                {
                    continue;
                }

                result = result.Replace(secret, "***", StringComparison.Ordinal);
            }
        }

        foreach (var name in PlaytestChildEnvironment.ForbiddenVariableNames)
        {
            result = RedactAssignment(result, name);
        }

        result = RedactAssignment(result, PlaytestAuthToken.EnvironmentVariable);
        result = RedactAssignment(result, "FROG_PLAYTEST_AUTH_TOKEN");

        return result;
    }

    private static string RedactAssignment(string input, string key)
    {
        // KEY=value → KEY=*** (value may contain ';' until whitespace/end)
        var pattern = key + "=";
        var idx = 0;
        var sb = new StringBuilder(input.Length);
        while (idx < input.Length)
        {
            var found = input.IndexOf(pattern, idx, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                sb.Append(input.AsSpan(idx));
                break;
            }

            sb.Append(input.AsSpan(idx, found - idx));
            sb.Append(key);
            sb.Append("=***");
            var valueStart = found + pattern.Length;
            var valueEnd = valueStart;
            while (valueEnd < input.Length && !char.IsWhiteSpace(input[valueEnd]))
            {
                valueEnd++;
            }

            idx = valueEnd;
        }

        return sb.ToString();
    }
}
