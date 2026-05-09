namespace Frog.Server.Persistence;

public interface IPlayerStateStore
{
    /// <summary>Charge carte + position en <b>pixels monde</b> (centre) pour le personnage (<c>frog_character.id</c>) — même unité que `PositionUpdate` lorsque <see cref="Frog.Core.Constants.FrogWireProtocol"/> ≥ 7.</summary>
    bool TryGetForCharacter(string characterId, out PlayerWorldState state);

    /// <summary>Persiste <c>mapId</c> + centre joueur (<paramref name="x"/>, <paramref name="y"/> en pixels).</summary>
    void UpsertForCharacter(string characterId, int mapId, int x, int y);
}
