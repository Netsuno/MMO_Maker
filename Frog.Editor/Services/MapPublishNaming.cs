using System.Text;

namespace Frog.Editor.Services;

public static class MapPublishNaming
{
    /// <summary>Clé stable pour <c>frog_map.map_key</c> (lettres minuscules, chiffres, tirets et soulignés).</summary>
    public static string SlugFromName(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return "map";
        }

        var sb = new StringBuilder();
        foreach (var c in mapName.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (c is ' ' or '-' or '_' or '.')
            {
                sb.Append('_');
            }
        }

        while (sb.Length > 0 && sb[0] == '_')
        {
            sb.Remove(0, 1);
        }

        while (sb.Length > 0 && sb[^1] == '_')
        {
            sb.Length--;
        }

        var s = sb.ToString();
        if (string.IsNullOrEmpty(s))
        {
            return "map";
        }

        return TruncateUtf8Bytes(s, 255);
    }

    public static string ClampDisplayName(string? name, int maxUtf8Bytes = 512)
    {
        var n = string.IsNullOrWhiteSpace(name) ? "Carte" : name.Trim();
        return TruncateUtf8Bytes(n, maxUtf8Bytes);
    }

    private static string TruncateUtf8Bytes(string text, int maxBytes)
    {
        var enc = Encoding.UTF8;
        var bytes = enc.GetBytes(text);
        if (bytes.Length <= maxBytes)
        {
            return text;
        }

        var len = maxBytes;
        while (len > 0 && (bytes[len - 1] & 0b1100_0000) == 0b1000_0000)
        {
            len--;
        }

        return enc.GetString(bytes.AsSpan(0, len));
    }
}
