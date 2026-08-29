using System.Collections.Concurrent;
using Frog.Application.Events;

namespace Frog.Server.Gameplay;

/// <summary>Interrupteurs perso en mémoire (playtest / sans PostgreSQL).</summary>
public sealed class InMemoryCharacterWorldStateRepository : ICharacterWorldStateRepository
{
    private readonly ConcurrentDictionary<(Guid CharacterId, string SwitchId), bool> _switches = new();

    public Task<bool?> GetSwitchAsync(Guid characterId, string switchId, CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(switchId))
        {
            return Task.FromResult<bool?>(null);
        }

        return Task.FromResult(
            _switches.TryGetValue((characterId, switchId), out var value) ? value : (bool?)null);
    }

    public Task SetSwitchAsync(
        Guid characterId,
        string switchId,
        bool value,
        CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(switchId))
        {
            return Task.CompletedTask;
        }

        _switches[(characterId, switchId)] = value;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, bool>> GetAllSwitchesAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty)
        {
            return Task.FromResult<IReadOnlyDictionary<string, bool>>(new Dictionary<string, bool>());
        }

        var dict = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var kv in _switches)
        {
            if (kv.Key.CharacterId == characterId)
            {
                dict[kv.Key.SwitchId] = kv.Value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, bool>>(dict);
    }
}
