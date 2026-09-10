using System.Text.Json;
using Frog.Core.Character;
using Frog.Core.Models;

namespace Frog.Core.Events;

public static class MapEventParameterSchemas
{
    public static bool TryParseShowText(string parameterJson, out string text, out string? error)
    {
        text = string.Empty;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
            {
                error = "show_text: propriété 'text' (string) requise.";
                return false;
            }

            text = textEl.GetString()?.Trim() ?? string.Empty;
            if (text.Length is 0 or > 512)
            {
                error = "show_text: texte vide ou trop long (max 512).";
                return false;
            }

            if (!MapEventParameterJsonStrict.ValidateRoot(
                    doc.RootElement,
                    new HashSet<string>(StringComparer.Ordinal) { "text" },
                    out error))
            {
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "show_text: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseSetSwitch(string parameterJson, out string switchId, out bool value, out string? error)
    {
        switchId = string.Empty;
        value = false;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("switchId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            {
                error = "set_switch: propriété 'switchId' (string) requise.";
                return false;
            }

            switchId = idEl.GetString()?.Trim() ?? string.Empty;
            if (!TryValidateSwitchKey(switchId, out error))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("value", out var valueEl)
                || valueEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = "set_switch: propriété 'value' (bool) requise.";
                return false;
            }

            value = valueEl.GetBoolean();
            return true;
        }
        catch (JsonException ex)
        {
            error = "set_switch: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseCharacterSwitchCondition(
        string parameterJson,
        out string switchId,
        out bool expectedValue,
        out string? error)
    {
        switchId = string.Empty;
        expectedValue = false;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("switchId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            {
                error = "character_switch: propriété 'switchId' requise.";
                return false;
            }

            switchId = idEl.GetString()?.Trim() ?? string.Empty;
            if (!TryValidateSwitchKey(switchId, out error))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("value", out var valueEl)
                || valueEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = "character_switch: propriété 'value' (bool) requise.";
                return false;
            }

            expectedValue = valueEl.GetBoolean();
            return true;
        }
        catch (JsonException ex)
        {
            error = "character_switch: JSON invalide: " + ex.Message;
            return false;
        }
    }

    private static bool TryValidateSwitchKey(string key, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(key))
        {
            error = "switchId vide.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(key) > CharacterPayloadWorldFlags.MaxKeyUtf8Bytes)
        {
            error = "switchId trop long.";
            return false;
        }

        foreach (var ch in key)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            error = "switchId: caractères autorisés [A-Za-z0-9_].";
            return false;
        }

        return true;
    }

    private static bool TryValidateVariableKey(string key, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(key))
        {
            error = "variableId vide.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(key) > CharacterPayloadWorldFlags.MaxKeyUtf8Bytes)
        {
            error = "variableId trop long.";
            return false;
        }

        foreach (var ch in key)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            error = "variableId: caractères autorisés [A-Za-z0-9_].";
            return false;
        }

        return true;
    }

    public static bool TryParseSetVariable(string parameterJson, out string variableId, out int value, out string? error)
        => TryParseVariableMutation(parameterJson, "set_variable", out variableId, out value, out error);

    public static bool TryParseAddVariable(string parameterJson, out string variableId, out int delta, out string? error)
        => TryParseVariableMutation(parameterJson, "add_variable", out variableId, out delta, out error, "delta");

    public static bool TryParseSubVariable(string parameterJson, out string variableId, out int delta, out string? error)
        => TryParseVariableMutation(parameterJson, "sub_variable", out variableId, out delta, out error, "delta");

    private static bool TryParseVariableMutation(
        string parameterJson,
        string label,
        out string variableId,
        out int value,
        out string? error,
        string valueProperty = "value")
    {
        variableId = string.Empty;
        value = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("variableId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            {
                error = $"{label}: propriété 'variableId' requise.";
                return false;
            }

            variableId = idEl.GetString()?.Trim() ?? string.Empty;
            if (!TryValidateVariableKey(variableId, out error))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty(valueProperty, out var valueEl) || !valueEl.TryGetInt32(out value))
            {
                error = $"{label}: propriété '{valueProperty}' (int) requise.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"{label}: JSON invalide: {ex.Message}";
            return false;
        }
    }

    public static bool TryParseCharacterVariableCompare(
        string parameterJson,
        out string variableId,
        out string op,
        out int compareValue,
        out string? error)
    {
        variableId = string.Empty;
        op = string.Empty;
        compareValue = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("variableId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            {
                error = "character_variable_compare: variableId requis.";
                return false;
            }

            variableId = idEl.GetString()?.Trim() ?? string.Empty;
            if (!TryValidateVariableKey(variableId, out error))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("op", out var opEl) || opEl.ValueKind != JsonValueKind.String)
            {
                error = "character_variable_compare: op requis (eq|ne|lt|lte|gt|gte).";
                return false;
            }

            op = opEl.GetString()?.Trim() ?? string.Empty;
            if (op is not ("eq" or "ne" or "lt" or "lte" or "gt" or "gte"))
            {
                error = "character_variable_compare: op invalide.";
                return false;
            }

            if (!doc.RootElement.TryGetProperty("value", out var valueEl) || !valueEl.TryGetInt32(out compareValue))
            {
                error = "character_variable_compare: value (int) requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "character_variable_compare: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseItemQuantity(string parameterJson, out Guid itemId, out int quantity, out string? error)
    {
        itemId = Guid.Empty;
        quantity = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "itemId", out itemId, out error))
            {
                error = "item_quantity: " + error;
                return false;
            }

            if (!doc.RootElement.TryGetProperty("quantity", out var qtyEl) || !qtyEl.TryGetInt32(out quantity) || quantity < 0)
            {
                error = "item_quantity: quantity (int >= 0) requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "item_quantity: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseItemMutation(
        string parameterJson,
        out Guid itemId,
        out int quantity,
        out string? onceKey,
        out string? error)
    {
        itemId = Guid.Empty;
        quantity = 0;
        onceKey = null;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "itemId", out itemId, out error))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("quantity", out var qtyEl) || !qtyEl.TryGetInt32(out quantity) || quantity <= 0)
            {
                error = "quantity (int > 0) requis.";
                return false;
            }

            if (doc.RootElement.TryGetProperty("onceKey", out var onceEl))
            {
                if (onceEl.ValueKind != JsonValueKind.String)
                {
                    error = "onceKey (string) invalide.";
                    return false;
                }

                onceKey = onceEl.GetString()?.Trim();
                if (string.IsNullOrEmpty(onceKey) || onceKey.Length > 64)
                {
                    error = "onceKey vide ou trop long (max 64).";
                    return false;
                }
            }

            if (!MapEventParameterJsonStrict.ValidateRoot(
                    doc.RootElement,
                    new HashSet<string>(StringComparer.Ordinal) { "itemId", "quantity", "onceKey" },
                    out error))
            {
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseGoldMutation(
        string parameterJson,
        out int amount,
        out string? onceKey,
        out string? error)
    {
        amount = 0;
        onceKey = null;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("amount", out var amtEl) || !amtEl.TryGetInt32(out amount) || amount <= 0)
            {
                error = "amount (int > 0) requis.";
                return false;
            }

            if (doc.RootElement.TryGetProperty("onceKey", out var onceEl))
            {
                if (onceEl.ValueKind != JsonValueKind.String)
                {
                    error = "onceKey (string) invalide.";
                    return false;
                }

                onceKey = onceEl.GetString()?.Trim();
                if (string.IsNullOrEmpty(onceKey) || onceKey.Length > 64)
                {
                    error = "onceKey vide ou trop long (max 64).";
                    return false;
                }
            }

            if (!MapEventParameterJsonStrict.ValidateRoot(
                    doc.RootElement,
                    new HashSet<string>(StringComparer.Ordinal) { "amount", "onceKey" },
                    out error))
            {
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseItemMutation(string parameterJson, out Guid itemId, out int quantity, out string? error) =>
        TryParseItemMutation(parameterJson, out itemId, out quantity, out _, out error);

    public static bool TryParseGoldMutation(string parameterJson, out int amount, out string? error) =>
        TryParseGoldMutation(parameterJson, out amount, out _, out error);

    public static bool TryParseTeleport(string parameterJson, out int mapId, out int tileX, out int tileY, out string? error)
    {
        mapId = 0;
        tileX = 0;
        tileY = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("mapId", out var mapEl) || !mapEl.TryGetInt32(out mapId))
            {
                error = "teleport: mapId requis.";
                return false;
            }

            if (!doc.RootElement.TryGetProperty("tileX", out var xEl) || !xEl.TryGetInt32(out tileX))
            {
                error = "teleport: tileX requis.";
                return false;
            }

            if (!doc.RootElement.TryGetProperty("tileY", out var yEl) || !yEl.TryGetInt32(out tileY))
            {
                error = "teleport: tileY requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "teleport: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseWait(string parameterJson, out int milliseconds, out string? error)
    {
        milliseconds = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("milliseconds", out var msEl)
                || !msEl.TryGetInt32(out milliseconds)
                || milliseconds is < 0 or > 60_000)
            {
                error = "wait: milliseconds (0–60000) requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "wait: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseStartDialogue(string parameterJson, out Guid dialogueId, out string? error)
    {
        dialogueId = Guid.Empty;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "dialogueId", out dialogueId, out error))
            {
                error = "start_dialogue: " + error;
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "start_dialogue: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseQuestId(string parameterJson, out Guid questId, out string? error)
    {
        questId = Guid.Empty;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "questId", out questId, out error))
            {
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseLearnProfession(string parameterJson, out Guid professionId, out string? error)
    {
        professionId = Guid.Empty;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "professionId", out professionId, out error))
            {
                error = "learn_profession: " + (error ?? "professionId requis.");
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "learn_profession: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseAdvanceQuest(
        string parameterJson,
        out Guid questId,
        out int stageIndex,
        out string? error)
    {
        questId = Guid.Empty;
        stageIndex = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "questId", out questId, out error))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("stageIndex", out var stageEl) || !stageEl.TryGetInt32(out stageIndex) || stageIndex < 0)
            {
                error = "advance_quest: stageIndex (int >= 0) requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "advance_quest: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseCallCommonEvent(
        string parameterJson,
        out Guid commonEventId,
        out int? editorAliasId,
        out string? error)
    {
        commonEventId = Guid.Empty;
        editorAliasId = null;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (doc.RootElement.TryGetProperty("commonEventId", out var idEl)
                && idEl.ValueKind == JsonValueKind.String
                && Guid.TryParse(idEl.GetString(), out commonEventId)
                && commonEventId != Guid.Empty)
            {
                return true;
            }

            if (doc.RootElement.TryGetProperty("editorAliasId", out var aliasEl) && aliasEl.TryGetInt32(out var alias) && alias > 0)
            {
                editorAliasId = alias;
                return true;
            }

            error = "call_common_event: commonEventId ou editorAliasId requis.";
            return false;
        }
        catch (JsonException ex)
        {
            error = "call_common_event: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseQuestStatusCondition(
        string parameterJson,
        out Guid questId,
        out string status,
        out string? error)
    {
        questId = Guid.Empty;
        status = string.Empty;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "questId", out questId, out error))
            {
                error = "quest_status: " + error;
                return false;
            }

            if (!doc.RootElement.TryGetProperty("status", out var statusEl) || statusEl.ValueKind != JsonValueKind.String)
            {
                error = "quest_status: status requis (not_started|active|ready|completed).";
                return false;
            }

            status = statusEl.GetString()?.Trim() ?? string.Empty;
            if (status is not ("not_started" or "active" or "ready" or "completed"))
            {
                error = "quest_status: status invalide.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "quest_status: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseCharacterLevel(string parameterJson, out int minLevel, out string? error)
    {
        minLevel = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!doc.RootElement.TryGetProperty("minLevel", out var lvlEl) || !lvlEl.TryGetInt32(out minLevel) || minLevel < 1)
            {
                error = "character_level: minLevel (int >= 1) requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "character_level: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseProfessionLevel(
        string parameterJson,
        out Guid professionId,
        out int minLevel,
        out string? error)
    {
        professionId = Guid.Empty;
        minLevel = 0;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (!TryParseGuidProperty(doc.RootElement, "professionId", out professionId, out error))
            {
                error = "profession_level: " + error;
                return false;
            }

            if (!doc.RootElement.TryGetProperty("minLevel", out var lvlEl) || !lvlEl.TryGetInt32(out minLevel) || minLevel < 1)
            {
                error = "profession_level: minLevel requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "profession_level: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseMapOrRegion(
        string parameterJson,
        out int? mapId,
        out Guid? regionId,
        out string? error)
    {
        mapId = null;
        regionId = null;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            if (doc.RootElement.TryGetProperty("mapId", out var mapEl) && mapEl.TryGetInt32(out var mid))
            {
                mapId = mid;
            }

            if (doc.RootElement.TryGetProperty("regionId", out var regionEl)
                && regionEl.ValueKind == JsonValueKind.String
                && Guid.TryParse(regionEl.GetString(), out var rid)
                && rid != Guid.Empty)
            {
                regionId = rid;
            }

            if (mapId is null && regionId is null)
            {
                error = "map_or_region: mapId ou regionId requis.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "map_or_region: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool TryParseBranch(
        string parameterJson,
        out MapEventConditionDefinition condition,
        out IReadOnlyList<MapEventCommandDefinition> thenCommands,
        out IReadOnlyList<MapEventCommandDefinition> elseCommands,
        out string? error)
    {
        condition = new MapEventConditionDefinition();
        thenCommands = Array.Empty<MapEventCommandDefinition>();
        elseCommands = Array.Empty<MapEventCommandDefinition>();
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(parameterJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("conditionKind", out var kindEl) || kindEl.ValueKind != JsonValueKind.String)
            {
                error = "branch: conditionKind requis.";
                return false;
            }

            var paramJson = root.TryGetProperty("conditionParameterJson", out var paramEl)
                ? paramEl.GetString() ?? "{}"
                : "{}";
            condition = new MapEventConditionDefinition
            {
                Kind = kindEl.GetString()?.Trim() ?? string.Empty,
                ParameterJson = paramJson,
            };

            if (!TryParseCommandArray(root, "thenCommands", out thenCommands, out error))
            {
                error = "branch then: " + error;
                return false;
            }

            if (root.TryGetProperty("elseCommands", out _))
            {
                if (!TryParseCommandArray(root, "elseCommands", out elseCommands, out error))
                {
                    error = "branch else: " + error;
                    return false;
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = "branch: JSON invalide: " + ex.Message;
            return false;
        }
    }

    public static bool EvaluateVariableCompare(int actual, string op, int expected) =>
        op switch
        {
            "eq" => actual == expected,
            "ne" => actual != expected,
            "lt" => actual < expected,
            "lte" => actual <= expected,
            "gt" => actual > expected,
            "gte" => actual >= expected,
            _ => false,
        };

    private static bool TryParseCommandArray(
        JsonElement root,
        string propertyName,
        out IReadOnlyList<MapEventCommandDefinition> commands,
        out string? error)
    {
        commands = Array.Empty<MapEventCommandDefinition>();
        error = null;
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            error = $"propriété '{propertyName}' (array) requise.";
            return false;
        }

        var list = new List<MapEventCommandDefinition>();
        foreach (var el in arr.EnumerateArray())
        {
            if (!el.TryGetProperty("discriminator", out var discEl) || discEl.ValueKind != JsonValueKind.String)
            {
                error = "discriminator requis.";
                return false;
            }

            var paramJson = el.TryGetProperty("parameterJson", out var paramEl)
                ? paramEl.GetString() ?? "{}"
                : "{}";
            var cmd = new MapEventCommandDefinition
            {
                Discriminator = discEl.GetString()?.Trim() ?? string.Empty,
                ParameterJson = paramJson,
            };
            if (!cmd.Validate(out error))
            {
                return false;
            }

            list.Add(cmd);
        }

        commands = list;
        return true;
    }

    private static bool TryParseGuidProperty(JsonElement root, string name, out Guid value, out string? error)
    {
        value = Guid.Empty;
        error = null;
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            error = $"propriété '{name}' (guid) requise.";
            return false;
        }

        if (!Guid.TryParse(el.GetString(), out value) || value == Guid.Empty)
        {
            error = $"propriété '{name}' guid invalide.";
            return false;
        }

        return true;
    }
}
