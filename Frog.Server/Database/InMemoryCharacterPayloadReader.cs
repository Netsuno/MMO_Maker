using System.Collections.Concurrent;
using System.Text.Json;
using Frog.Core;

namespace Frog.Server.Database;

/// <summary>Dev sans MariaDB : payload par UUID, défaut stats Hero.</summary>
public sealed class InMemoryCharacterPayloadReader : ICharacterPayloadReader, ICharacterPayloadWriter
{
    private readonly ConcurrentDictionary<string, string> _jsonByCharacterId =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetPayloadJson(string characterId, out string jsonPayload)
    {
        jsonPayload = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        if (_jsonByCharacterId.TryGetValue(characterId, out var stored))
        {
            jsonPayload = stored;
            return true;
        }

        jsonPayload = CharacterPayloadDefaults.NewHeroJson;
        return true;
    }

    public bool TryUpdatePayloadJson(string characterId, string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(jsonPayload))
        {
            return false;
        }

        try
        {
            JsonDocument.Parse(jsonPayload);
        }
        catch (JsonException)
        {
            return false;
        }

        _jsonByCharacterId[characterId] = jsonPayload;
        return true;
    }
}
