using Frog.Core;

namespace Frog.Server.Database;

/// <summary>Dev sans MariaDB : renvoie le schéma stats par défaut.</summary>
public sealed class InMemoryCharacterPayloadReader : ICharacterPayloadReader
{
    public bool TryGetPayloadJson(string characterId, out string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            jsonPayload = string.Empty;
            return false;
        }

        jsonPayload = CharacterPayloadDefaults.NewHeroJson;
        return true;
    }
}
