namespace Frog.Server.Database;

/// <summary>
/// Lecture du JSON « perso » pour le réseau post-login : sous MariaDB, assemblage depuis
/// <c>character_stat</c>, <c>character_world_flag</c>, <c>character_payload_kv</c> (valeurs LONGTEXT UTF-8) ; plus de colonne JSON perso après migration **v10**.
/// </summary>
public interface ICharacterPayloadReader
{
    bool TryGetPayloadJson(string characterId, out string jsonPayload);
}
