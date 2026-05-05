namespace Frog.Server.Database;

/// <summary>Lecture du JSON persisté (<c>frog_character.payload</c>) pour le réseau post-login.</summary>
public interface ICharacterPayloadReader
{
    bool TryGetPayloadJson(string characterId, out string jsonPayload);
}
