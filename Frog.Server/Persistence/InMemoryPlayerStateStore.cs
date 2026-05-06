using System.Collections.Concurrent;

namespace Frog.Server.Persistence;

public sealed class InMemoryPlayerStateStore : IPlayerStateStore
{
    private readonly ConcurrentDictionary<string, PlayerWorldState> _byCharacter =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetForCharacter(string characterId, out PlayerWorldState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        return _byCharacter.TryGetValue(characterId, out state);
    }

    public void UpsertForCharacter(string characterId, int mapId, int x, int y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        _byCharacter[characterId] = new PlayerWorldState(mapId, x, y, characterId);
    }
}
