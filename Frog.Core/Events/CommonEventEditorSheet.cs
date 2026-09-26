using System.Globalization;
using System.Text;
using System.Text.Json;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Projection éditeur d'un <see cref="CommonEventDefinition"/> existant.
/// Le déclencheur et l'interrupteur sont ceux de la page (pas un second schéma) :
/// <see cref="MapEventPageDefinition.TriggerKind"/> et une condition
/// <see cref="MapEventConditionKinds.CharacterSwitch"/>.
/// </summary>
public static class CommonEventEditorSheet
{
    public static readonly IReadOnlyList<string> TriggerOrder =
    [
        Phase8MapEventTriggerKinds.Action,
        Phase8MapEventTriggerKinds.PlayerContact,
        Phase8MapEventTriggerKinds.Autorun,
        Phase8MapEventTriggerKinds.Parallel,
    ];

    public static string FormatListLine(int? alias, string? name, string? statusLabel)
    {
        var number = alias is > 0
            ? alias.Value.ToString("000", CultureInfo.InvariantCulture)
            : "—";
        var label = string.IsNullOrWhiteSpace(name) ? "(sans nom)" : name.Trim();
        var status = string.IsNullOrWhiteSpace(statusLabel) ? "Brouillon" : statusLabel.Trim();
        return $"{number}  {label}  ·  {status}";
    }

    public static int NextAlias(IEnumerable<int?> used)
    {
        var taken = new HashSet<int>();
        foreach (var alias in used)
        {
            if (alias is > 0)
            {
                taken.Add(alias.Value);
            }
        }

        var next = 1;
        while (taken.Contains(next))
        {
            next++;
        }

        return next;
    }

    public static bool IsAliasTaken(IEnumerable<(Guid Id, int? Alias)> rows, Guid currentId, int alias)
    {
        if (alias <= 0)
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (row.Id != currentId && row.Alias == alias)
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<MapEventPageDefinition> CreateDefaultPages() =>
        [CreateBlankPage(0)];

    public static bool TryReadPage(
        IReadOnlyList<MapEventPageDefinition> pages,
        int pageIndex,
        out string triggerKind,
        out string? switchId,
        out bool switchValue)
    {
        triggerKind = Phase8MapEventTriggerKinds.Action;
        switchId = null;
        switchValue = true;
        if (pages.Count == 0)
        {
            return false;
        }

        if (pageIndex < 0 || pageIndex >= pages.Count)
        {
            pageIndex = 0;
        }

        var page = pages[pageIndex];
        triggerKind = string.IsNullOrWhiteSpace(page.TriggerKind)
            ? Phase8MapEventTriggerKinds.Action
            : page.TriggerKind.Trim();

        foreach (var condition in page.Conditions)
        {
            if (condition.Kind != MapEventConditionKinds.CharacterSwitch)
            {
                continue;
            }

            if (MapEventParameterSchemas.TryParseCharacterSwitchCondition(
                    condition.ParameterJson,
                    out var id,
                    out var value,
                    out _))
            {
                switchId = id;
                switchValue = value;
                break;
            }
        }

        return true;
    }

    /// <summary>
    /// Écrit le déclencheur et le premier interrupteur de la page visée.
    /// Les commandes et les autres conditions restent. Une liste vide gagne une page.
    /// </summary>
    public static bool TryApplyToPage(
        IReadOnlyList<MapEventPageDefinition> pages,
        int pageIndex,
        string triggerKind,
        string? switchId,
        bool switchValue,
        out IReadOnlyList<MapEventPageDefinition> updated,
        out string? error)
    {
        updated = Array.Empty<MapEventPageDefinition>();
        error = null;
        if (!Phase8MapEventTriggerKinds.IsSupported(triggerKind))
        {
            error = $"Déclencheur inconnu: {triggerKind}.";
            return false;
        }

        var normalizedSwitch = string.IsNullOrWhiteSpace(switchId) ? null : switchId.Trim();
        if (normalizedSwitch is not null && !TryAcceptSwitchId(normalizedSwitch, out error))
        {
            return false;
        }

        var copy = pages.Select(ClonePage).ToList();
        if (copy.Count == 0)
        {
            copy.Add(CreateBlankPage(0));
            pageIndex = 0;
        }
        else if (pageIndex < 0 || pageIndex >= copy.Count)
        {
            pageIndex = 0;
        }

        var page = copy[pageIndex];
        page.TriggerKind = triggerKind.Trim();
        page.Conditions = RewriteSwitch(page.Conditions, normalizedSwitch, switchValue);
        updated = copy;
        return true;
    }

    public static bool TryCompose(
        Guid id,
        string? name,
        int? editorAliasId,
        IReadOnlyList<MapEventPageDefinition> pages,
        int pageIndex,
        string triggerKind,
        string? switchId,
        bool switchValue,
        out CommonEventDefinition? definition,
        out string? error)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Nom requis.";
            return false;
        }

        if (!TryApplyToPage(pages, pageIndex, triggerKind, switchId, switchValue, out var updated, out error))
        {
            return false;
        }

        definition = new CommonEventDefinition
        {
            Id = id,
            Name = name.Trim(),
            EditorAliasId = editorAliasId is > 0 ? editorAliasId : null,
            Pages = updated,
        };
        return definition.Validate(out error);
    }

    private static bool TryAcceptSwitchId(string switchId, out string? error)
    {
        var probe = SwitchParameterJson(switchId, true);
        return MapEventParameterSchemas.TryParseCharacterSwitchCondition(probe, out _, out _, out error);
    }

    private static string SwitchParameterJson(string switchId, bool value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("switchId", switchId);
            writer.WriteBoolean("value", value);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static List<MapEventConditionDefinition> RewriteSwitch(
        IReadOnlyList<MapEventConditionDefinition> conditions,
        string? switchId,
        bool switchValue)
    {
        var list = conditions
            .Select(c => new MapEventConditionDefinition
            {
                Kind = c.Kind,
                ParameterJson = c.ParameterJson,
            })
            .ToList();

        var index = -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Kind == MapEventConditionKinds.CharacterSwitch)
            {
                index = i;
                break;
            }
        }

        if (switchId is null)
        {
            if (index >= 0)
            {
                list.RemoveAt(index);
            }

            return list;
        }

        var condition = new MapEventConditionDefinition
        {
            Kind = MapEventConditionKinds.CharacterSwitch,
            ParameterJson = SwitchParameterJson(switchId, switchValue),
        };
        if (index >= 0)
        {
            list[index] = condition;
        }
        else
        {
            list.Insert(0, condition);
        }

        return list;
    }

    private static MapEventPageDefinition CreateBlankPage(int pageOrder) => new()
    {
        PageOrder = pageOrder,
        Priority = 0,
        TriggerKind = Phase8MapEventTriggerKinds.Action,
        MovementKind = MapEventMovementKinds.Fixed,
        BlocksCollision = true,
        Conditions = Array.Empty<MapEventConditionDefinition>(),
        Commands = Array.Empty<MapEventCommandDefinition>(),
    };

    private static MapEventPageDefinition ClonePage(MapEventPageDefinition page) => new()
    {
        PageOrder = page.PageOrder,
        Priority = page.Priority,
        TriggerKind = page.TriggerKind,
        MovementKind = page.MovementKind,
        RouteWaypoints = page.RouteWaypoints.Select(w => w.Copy()).ToList(),
        RouteRepeat = page.RouteRepeat,
        RouteSkipIfBlocked = page.RouteSkipIfBlocked,
        AppearanceGraphicId = page.AppearanceGraphicId,
        AppearanceDirection = page.AppearanceDirection,
        BlocksCollision = page.BlocksCollision,
        Conditions = page.Conditions
            .Select(c => new MapEventConditionDefinition
            {
                Kind = c.Kind,
                ParameterJson = c.ParameterJson,
            })
            .ToList(),
        Commands = page.Commands
            .Select(c => new MapEventCommandDefinition
            {
                Discriminator = c.Discriminator,
                SchemaVersion = c.SchemaVersion,
                ParameterJson = c.ParameterJson,
            })
            .ToList(),
    };
}
