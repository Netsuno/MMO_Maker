using System.Collections.Concurrent;

namespace Frog.Server.Persistence;

public sealed class InMemoryPlayerStateStore : IPlayerStateStore
{
    private readonly ConcurrentDictionary<string, PlayerWorldState> _byUser = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string username, out PlayerWorldState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return _byUser.TryGetValue(username, out state);
    }

    public void Upsert(string username, int mapId, int x, int y, string? characterId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        _byUser[username] = new PlayerWorldState(mapId, x, y, characterId);
    }
}
