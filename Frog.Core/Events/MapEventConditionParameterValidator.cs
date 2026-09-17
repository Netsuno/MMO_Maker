using System.Text;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>Validation publish-time des paramètres de conditions événement (P8-G1).</summary>
public static class MapEventConditionParameterValidator
{
    public static bool ValidateParameters(MapEventConditionDefinition condition, out string? error)
    {
        error = null;
        if (Encoding.UTF8.GetByteCount(condition.ParameterJson) > MapEventRuntimeLimits.MaxConditionParameterBytes)
        {
            error = "Paramètres de condition trop volumineux (octets UTF-8).";
            return false;
        }

        var ok = condition.Kind switch
        {
            MapEventConditionKinds.CharacterSwitch =>
                MapEventParameterSchemas.TryParseCharacterSwitchCondition(
                    condition.ParameterJson,
                    out _,
                    out _,
                    out error),
            MapEventConditionKinds.CharacterVariableCompare =>
                MapEventParameterSchemas.TryParseCharacterVariableCompare(
                    condition.ParameterJson,
                    out _,
                    out _,
                    out _,
                    out error),
            MapEventConditionKinds.QuestStatus =>
                MapEventParameterSchemas.TryParseQuestStatusCondition(
                    condition.ParameterJson,
                    out _,
                    out _,
                    out error),
            MapEventConditionKinds.ItemQuantity =>
                MapEventParameterSchemas.TryParseItemQuantity(
                    condition.ParameterJson,
                    out _,
                    out _,
                    out error),
            MapEventConditionKinds.CharacterLevel =>
                MapEventParameterSchemas.TryParseCharacterLevel(
                    condition.ParameterJson,
                    out _,
                    out error),
            MapEventConditionKinds.ProfessionLevel =>
                MapEventParameterSchemas.TryParseProfessionLevel(
                    condition.ParameterJson,
                    out _,
                    out _,
                    out error),
            MapEventConditionKinds.MapOrRegion =>
                MapEventParameterSchemas.TryParseMapOrRegion(
                    condition.ParameterJson,
                    out _,
                    out _,
                    out error),
            _ => false,
        };

        if (!ok && error is null)
        {
            error = $"Paramètres invalides pour la condition {condition.Kind}.";
        }

        return ok;
    }
}
