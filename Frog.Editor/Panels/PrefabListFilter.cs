using System.Globalization;
using System.Text;

namespace Frog.Editor.Panels;

/// <summary>Filtre la palette prefab par nom affiché ou identifiant (insensible aux accents).</summary>
internal static class PrefabListFilter
{
    public static bool Matches(string? filter, string? id, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var query = filter.Trim();
        return Contains(id, query) || Contains(displayName, query);
    }

    private static bool Contains(string? haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack))
        {
            return false;
        }

        if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return StripDiacritics(haystack).Contains(StripDiacritics(needle), StringComparison.OrdinalIgnoreCase);
    }

    private static string StripDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
