namespace Frog.Server.Persistence;

public interface IPlayerStateStore
{
    bool TryGet(string username, out PlayerWorldState state);

    void Upsert(string username, int mapId, int x, int y);
}
