namespace Frog.Core.Models;

/// <summary>Commande typée data-driven (payload JSON validé côté Core).</summary>
public sealed class MapEventCommandDefinition
{
    public string Discriminator { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public string ParameterJson { get; set; } = "{}";

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Discriminator))
        {
            error = "Discriminator de commande requis.";
            return false;
        }

        if (SchemaVersion < 1)
        {
            error = "SchemaVersion doit être >= 1.";
            return false;
        }

        if (ParameterJson.Length > MapEventRuntimeLimits.MaxCommandParameterBytes)
        {
            error = "Paramètres de commande trop volumineux.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(ParameterJson) > MapEventRuntimeLimits.MaxCommandParameterBytes)
        {
            error = "Paramètres de commande trop volumineux (octets UTF-8).";
            return false;
        }

        if (!MapEventCommandDiscriminators.IsKnown(Discriminator))
        {
            error = $"Commande inconnue: {Discriminator}.";
            return false;
        }

        error = null;
        return true;
    }
}

public static class MapEventCommandDiscriminators
{
    public const string ShowText = "show_text";
    public const string StartDialogue = "start_dialogue";
    public const string Branch = "branch";
    public const string SetSwitch = "set_switch";
    public const string SetVariable = "set_variable";
    public const string AddVariable = "add_variable";
    public const string SubVariable = "sub_variable";
    public const string GiveItem = "give_item";
    public const string TakeItem = "take_item";
    public const string GiveGold = "give_gold";
    public const string TakeGold = "take_gold";
    public const string StartQuest = "start_quest";
    public const string AdvanceQuest = "advance_quest";
    public const string TurnInQuest = "turn_in_quest";
    public const string Teleport = "teleport";
    public const string Wait = "wait";
    public const string CallCommonEvent = "call_common_event";
    public const string LearnProfession = "learn_profession";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ShowText, StartDialogue, Branch, SetSwitch, SetVariable, AddVariable, SubVariable,
        GiveItem, TakeItem, GiveGold, TakeGold, StartQuest, AdvanceQuest, TurnInQuest,
        Teleport, Wait, CallCommonEvent, LearnProfession,
    };

    public static bool IsKnown(string discriminator) => All.Contains(discriminator);
}
