using System.Text.Json;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Palette MVP : texte, interrupteur, variable, branche (interrupteur ou variable).
/// Les discriminators et le JSON restent ceux du catalogue Phase 8 (Postgres inchangé).
/// </summary>
public static class MapEventCommandPalette
{
    public const string ShowTextId = "show_text";
    public const string SetSwitchId = "set_switch";
    public const string SetVariableId = "set_variable";
    public const string AddVariableId = "add_variable";
    public const string SubVariableId = "sub_variable";
    public const string BranchSwitchId = "branch_switch";
    public const string BranchVariableId = "branch_variable";

    public const string DefaultSwitchId = "interrupteur_1";
    public const string DefaultVariableId = "variable_1";

    public static readonly IReadOnlyList<Entry> Entries = new Entry[]
    {
        new(ShowTextId, "Texte"),
        new(SetSwitchId, "Interrupteur"),
        new(SetVariableId, "Variable ="),
        new(AddVariableId, "Variable +"),
        new(SubVariableId, "Variable −"),
        new(BranchSwitchId, "Si interrupteur"),
        new(BranchVariableId, "Si variable"),
    };

    public readonly record struct Entry(string Id, string Label);

    public static bool TryCreate(string? id, out MapEventCommandDefinition command)
    {
        command = new MapEventCommandDefinition();
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var created = id.Trim() switch
        {
            ShowTextId => Command(
                MapEventCommandDiscriminators.ShowText,
                new { text = "Bonjour." }),
            SetSwitchId => Command(
                MapEventCommandDiscriminators.SetSwitch,
                new { switchId = DefaultSwitchId, value = true }),
            SetVariableId => Command(
                MapEventCommandDiscriminators.SetVariable,
                new { variableId = DefaultVariableId, value = 0 }),
            AddVariableId => Command(
                MapEventCommandDiscriminators.AddVariable,
                new { variableId = DefaultVariableId, delta = 1 }),
            SubVariableId => Command(
                MapEventCommandDiscriminators.SubVariable,
                new { variableId = DefaultVariableId, delta = 1 }),
            BranchSwitchId => Command(
                MapEventCommandDiscriminators.Branch,
                BranchBody(
                    MapEventConditionKinds.CharacterSwitch,
                    JsonSerializer.Serialize(new { switchId = DefaultSwitchId, value = true }))),
            BranchVariableId => Command(
                MapEventCommandDiscriminators.Branch,
                BranchBody(
                    MapEventConditionKinds.CharacterVariableCompare,
                    JsonSerializer.Serialize(new { variableId = DefaultVariableId, op = "gte", value = 1 }))),
            _ => null,
        };

        if (created is null)
        {
            return false;
        }

        command = created;
        return true;
    }

    private static object BranchBody(string conditionKind, string conditionParameterJson) =>
        new
        {
            conditionKind,
            conditionParameterJson,
            thenCommands = Array.Empty<object>(),
            elseCommands = Array.Empty<object>(),
        };

    private static MapEventCommandDefinition Command(string discriminator, object body) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = JsonSerializer.Serialize(body),
        };
}
