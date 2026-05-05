namespace Frog.Server.Database;

/// <summary>
/// Garantit un personnage jouable minimal par compte (phase 1 : un « Hero » par username).
/// </summary>
public interface ICharacterBootstrap
{
    /// <summary>Identifiant <c>frog_character.id</c> (UUID texte) pour le perso par défaut.</summary>
    string EnsureDefaultHero(string username);
}
