namespace Frog.Core.Models;

using Frog.Core.Events;

/// <summary>Condition typée — pas d'expression libre.</summary>
public sealed class MapEventConditionDefinition
{
    public string Kind { get; set; } = string.Empty;
    public string ParameterJson { get; set; } = "{}";

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Kind))
        {
            error = "Kind de condition requis.";
            return false;
        }

        if (ParameterJson.Length > MapEventRuntimeLimits.MaxConditionParameterBytes)
        {
            error = "Paramètres de condition trop volumineux.";
            return false;
        }

        if (!MapEventConditionKinds.IsSupported(Kind))
        {
            error = $"Kind de condition inconnu: {Kind}.";
            return false;
        }

        if (!MapEventConditionParameterValidator.ValidateParameters(this, out error))
        {
            return false;
        }

        error = null;
        return true;
    }
}

public static class MapEventConditionKinds
{
    public const string CharacterSwitch = "character_switch";
    public const string CharacterVariableCompare = "character_variable_compare";
    public const string QuestStatus = "quest_status";
    public const string ItemQuantity = "item_quantity";
    public const string CharacterLevel = "character_level";
    public const string ProfessionLevel = "profession_level";
    public const string MapOrRegion = "map_or_region";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        CharacterSwitch,
        CharacterVariableCompare,
        QuestStatus,
        ItemQuantity,
        CharacterLevel,
        ProfessionLevel,
        MapOrRegion,
    };

    public static bool IsSupported(string kind) => All.Contains(kind);
}
