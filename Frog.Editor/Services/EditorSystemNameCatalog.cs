using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>
/// Libellés français du document Système, pour les champs switchId / variableId des événements.
/// Mis à jour au chargement et à l’enregistrement de la fiche Données de jeu.
/// </summary>
internal static class EditorSystemNameCatalog
{
    private static readonly object Gate = new();
    private static Dictionary<string, string> _switches = new(StringComparer.Ordinal);
    private static Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    public static void Replace(SystemDefinition? definition)
    {
        var switches = ToMap(definition?.Switches);
        var variables = ToMap(definition?.Variables);
        lock (Gate)
        {
            _switches = switches;
            _variables = variables;
        }
    }

    public static void Reset() => Replace(null);

    public static string FormatSwitch(string id) => Format(_switches, id);

    public static string FormatVariable(string id) => Format(_variables, id);

    private static string Format(Dictionary<string, string> map, string id)
    {
        id = id.Trim();
        if (id.Length == 0)
        {
            return string.Empty;
        }

        lock (Gate)
        {
            return map.TryGetValue(id, out var label) ? label : string.Empty;
        }
    }

    private static Dictionary<string, string> ToMap<T>(IEnumerable<T>? entries)
        where T : SystemCatalogEntry
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entries is null)
        {
            return map;
        }

        foreach (var entry in entries)
        {
            var id = entry.Id?.Trim() ?? string.Empty;
            var label = entry.Label?.Trim() ?? string.Empty;
            if (id.Length == 0 || label.Length == 0 || map.ContainsKey(id))
            {
                continue;
            }

            map[id] = label;
        }

        return map;
    }
}
