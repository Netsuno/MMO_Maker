using System.Text.Json;

namespace Frog.Core.Events;

internal static class MapEventParameterJsonStrict
{
    private static readonly HashSet<string> EmptyAllowed = new(StringComparer.Ordinal);

    public static bool HasOnlyAllowedProperties(JsonElement root, IReadOnlySet<string> allowed, out string? error)
    {
        error = null;
        foreach (var prop in root.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
            {
                error = $"Propriété JSON inconnue: '{prop.Name}'.";
                return false;
            }
        }

        return true;
    }

    public static bool HasOnlyAllowedProperties(JsonElement root, params string[] allowed) =>
        HasOnlyAllowedProperties(root, new HashSet<string>(allowed, StringComparer.Ordinal), out _);

    public static bool ValidateRoot(
        JsonElement root,
        IReadOnlySet<string> allowed,
        out string? error) =>
        HasOnlyAllowedProperties(root, allowed, out error);
}
