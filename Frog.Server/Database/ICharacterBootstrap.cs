using Frog.Core.Models;

namespace Frog.Server.Database;

/// <summary>
/// Garantit un personnage jouable minimal par compte (phase 1 : un « Hero » par username).
/// </summary>
public interface ICharacterBootstrap
{
    /// <summary>Identifiant <c>frog_character.id</c> (UUID texte) pour le perso par défaut.</summary>
    string EnsureDefaultHero(string username);

    /// <summary>Personnages du compte (ordre stable, ex. <c>created_at</c> en MariaDB).</summary>
    IReadOnlyList<CharacterSlotInfo> ListCharacters(string username);

    /// <summary><c>true</c> si <paramref name="characterId"/> appartient au compte.</summary>
    bool IsCharacterOwned(string username, string characterId);

    /// <summary>
    /// Crée un perso additionnel (stats JSON par défaut). <paramref name="errorMessage"/> : texte court pour le client.
    /// En succès, <paramref name="characterId"/> = nouvel UUID <c>frog_character.id</c>.
    /// </summary>
    bool TryCreateCharacter(string username, string displayName, out string characterId, out string errorMessage);
}
