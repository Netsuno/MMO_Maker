namespace Frog.Server.Persistence;

public interface IPlayerStateStore
{
    /// <summary>Charge carte + tuile pour le personnage (<c>frog_character.id</c>).</summary>
    bool TryGetForCharacter(string characterId, out PlayerWorldState state);

    /// <summary>Persiste la position monde pour ce personnage (une ligne par perso, base multi-slots).</summary>
    void UpsertForCharacter(string characterId, int mapId, int x, int y);
}
