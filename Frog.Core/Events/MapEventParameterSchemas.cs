using System.Text.Json;
using Frog.Core.Character;

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
}
