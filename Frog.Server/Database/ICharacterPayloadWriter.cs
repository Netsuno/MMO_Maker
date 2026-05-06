namespace Frog.Server.Database;

/// <summary>Écriture du JSON <c>frog_character.payload</c> (stats, etc.).</summary>
public interface ICharacterPayloadWriter
{
    /// <summary>Met à jour le payload complet (JSON UTF-8 valide).</summary>
    bool TryUpdatePayloadJson(string characterId, string jsonPayload);
}
