using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frog.Core.Character;

/// <summary>Fusion contrôlée du bloc <c>worldFlags</c> du JSON perso (objet booléen par clé) ; sous MariaDB persistance dans <c>character_world_flag</c>.</summary>
public static class CharacterPayloadWorldFlags
{
    public const int MaxPatchUtf8Bytes = 2048;

    public const int MaxPatchKeys = 24;

    public const int MaxMergedWorldFlagKeys = 64;

    public const int MaxKeyUtf8Bytes = 64;

    public const int MaxMergedPayloadUtf8Bytes = 16384;

    /// <summary>Applique <paramref name="patchJson"/> (objet racine) sur <c>worldFlags</c> du JSON perso.</summary>
    public static bool TryMergeWorldFlags(string? existingJson, string patchJson, out string mergedJson, out string errorMessage)
    {
        mergedJson = string.Empty;
        errorMessage = string.Empty;
        var patchBytes = Encoding.UTF8.GetByteCount(patchJson);
        if (patchBytes is 0 or > MaxPatchUtf8Bytes)
        {
            errorMessage = "Patch worldFlags: taille UTF-8 invalide.";
            return false;
        }

        JsonObject patch;
        try
        {
            patch = JsonNode.Parse(patchJson) as JsonObject
                ?? throw new JsonException("Objet JSON attendu.");
        }
        catch (JsonException ex)
        {
            errorMessage = "Patch worldFlags JSON illisible: " + ex.Message;
            return false;
        }

        if (patch.Count == 0)
        {
            errorMessage = "Patch worldFlags: objet non vide requis.";
            return false;
        }

        if (patch.Count > MaxPatchKeys)
        {
            errorMessage = "Patch worldFlags: trop de cles.";
            return false;
        }

        JsonObject root;
        try
        {
            root = string.IsNullOrWhiteSpace(existingJson)
                ? new JsonObject()
                : (JsonNode.Parse(existingJson) as JsonObject ?? new JsonObject());
        }
        catch (JsonException ex)
        {
            errorMessage = "Payload JSON perso illisible: " + ex.Message;
            return false;
        }

        var existingWf = root["worldFlags"];
        if (existingWf is not null && existingWf is not JsonObject)
        {
            errorMessage = "worldFlags existant: objet attendu.";
            return false;
        }

        var worldFlags = (root["worldFlags"] as JsonObject) ?? new JsonObject();
        foreach (var kv in patch)
        {
            if (!TryValidateFlagKey(kv.Key, out var keyErr))
            {
                errorMessage = keyErr;
                return false;
            }

            if (kv.Value is null)
            {
                errorMessage = $"Cle '{kv.Key}': valeur absente.";
                return false;
            }

            if (kv.Value is not JsonValue jv)
            {
                errorMessage = $"Cle '{kv.Key}': valeur booleenne uniquement.";
                return false;
            }

            var kind = jv.GetValueKind();
            if (kind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errorMessage = $"Cle '{kv.Key}': valeur booleenne uniquement.";
                return false;
            }

            worldFlags[kv.Key] = JsonValue.Create(jv.GetValue<bool>());
        }

        if (worldFlags.Count > MaxMergedWorldFlagKeys)
        {
            errorMessage = "worldFlags: trop de cles apres fusion.";
            return false;
        }

        root["worldFlags"] = worldFlags;
        mergedJson = root.ToJsonString();
        if (Encoding.UTF8.GetByteCount(mergedJson) > MaxMergedPayloadUtf8Bytes)
        {
            errorMessage = "Payload perso trop volumineux apres fusion.";
            return false;
        }

        return true;
    }

    private static bool TryValidateFlagKey(string key, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrEmpty(key))
        {
            errorMessage = "Cle worldFlags vide.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(key) > MaxKeyUtf8Bytes)
        {
            errorMessage = "Cle worldFlags trop longue.";
            return false;
        }

        foreach (var ch in key)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            errorMessage = "Cle worldFlags: caracteres autorises [A-Za-z0-9_].";
            return false;
        }

        return true;
    }
}
