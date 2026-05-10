using System.Text;

namespace Frog.Core.Protocol;

/// <summary>Clé optionnelle <c>frog_event_catalog.script_key</c> (réservée exécution scripts Phase 7).</summary>
public static class MapEventScriptKeyNormalization
{
    public const int MaxUtf8Bytes = 128;

    /// <summary>Vide → <c>null</c> (effacer). Sinon trim + contrôle caractères + longueur UTF-8.</summary>
    public static bool TryNormalize(string? raw, out string? key, out string errorMessage)
    {
        key = null;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var t = raw.Trim();
        if (t.Length > 512)
        {
            errorMessage = "script_key trop long (pré-trim).";
            return false;
        }

        foreach (var ch in t)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '.' or '-' or ':' or '/')
            {
                continue;
            }

            errorMessage = "script_key : caractères autorisés [A-Za-z0-9._:/-].";
            return false;
        }

        var bytes = Encoding.UTF8.GetByteCount(t);
        if (bytes > MaxUtf8Bytes)
        {
            errorMessage = $"script_key : max {MaxUtf8Bytes} octets UTF-8.";
            return false;
        }

        key = t;
        return true;
    }
}
