using System.Text;

namespace Frog.Core.Protocol;

/// <summary>Normalisation slug / libellé pour <c>frog_event_catalog</c> (client, éditeur, outils).</summary>
public static class MapEventCatalogNormalization
{
    public const int MaxSlugLength = 64;

    public const int MaxDisplayNameLength = 255;

    /// <summary>Retourne un slug non vide ou <c>null</c> si invalide.</summary>
    public static string? TryNormalizeSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var sb = new StringBuilder(Math.Min(raw.Length, MaxSlugLength));
        foreach (var ch in raw.Trim().ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_')
            {
                sb.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '.')
            {
                if (sb.Length > 0 && sb[^1] != '_')
                {
                    sb.Append('_');
                }
            }

            if (sb.Length >= MaxSlugLength)
            {
                break;
            }
        }

        while (sb.Length > 0 && sb[^1] == '_')
        {
            sb.Length--;
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    public static string? TryNormalizeDisplayName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var t = raw.Trim();
        return t.Length > MaxDisplayNameLength ? t[..MaxDisplayNameLength] : t;
    }
}
