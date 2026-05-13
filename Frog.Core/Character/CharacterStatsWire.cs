using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frog.Core.Character;

/// <summary>Bloc stats perso (6 attributs) ; wire JSON <c>stats</c> — sous MariaDB persistance dans <c>character_stat</c>.</summary>
public static class CharacterStatsWire
{
    public const int PackedByteCount = 6;

    public const byte MinStat = 1;

    public const byte MaxStat = 99;

    private static readonly string[] StatKeys = ["STR", "AGI", "DEX", "INT", "VIT", "LUCK"];

    /// <summary>Valide 6 octets STR…LUCK dans <see cref="MinStat"/>..<see cref="MaxStat"/>.</summary>
    public static bool TryValidatePacked(ReadOnlySpan<byte> packed, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (packed.Length != PackedByteCount)
        {
            errorMessage = "Stats: 6 octets attendus.";
            return false;
        }

        for (var i = 0; i < packed.Length; i++)
        {
            if (packed[i] < MinStat || packed[i] > MaxStat)
            {
                errorMessage = $"Stats: valeur hors plage pour {StatKeys[i]}.";
                return false;
            }
        }

        return true;
    }

    /// <summary>Fusionne <paramref name="packed"/> dans le JSON existant (crée <c>stats</c> si absent).</summary>
    public static bool TryMergeIntoPayload(string? existingJson, ReadOnlySpan<byte> packed, out string newJson, out string errorMessage)
    {
        newJson = string.Empty;
        errorMessage = string.Empty;
        if (!TryValidatePacked(packed, out errorMessage))
        {
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

        var stats = new JsonObject();
        for (var i = 0; i < StatKeys.Length; i++)
        {
            stats[StatKeys[i]] = packed[i];
        }

        root["stats"] = stats;
        newJson = root.ToJsonString();
        return true;
    }
}
