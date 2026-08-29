using System.Collections.Concurrent;
using Frog.Application.Events;

namespace Frog.Server.Gameplay;

/// <summary>Interrupteurs et variables perso en mémoire (playtest / sans PostgreSQL).</summary>
public sealed class InMemoryCharacterWorldStateRepository : ICharacterWorldStateRepository
{
    private readonly ConcurrentDictionary<(Guid CharacterId, string SwitchId), bool> _switches = new();
    private readonly ConcurrentDictionary<(Guid CharacterId, string VariableId), int> _variables = new();

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

    public Task<int?> GetVariableAsync(Guid characterId, string variableId, CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(variableId))
        {
            return Task.FromResult<int?>(null);
        }

        return Task.FromResult(
            _variables.TryGetValue((characterId, variableId), out var value) ? value : (int?)null);
    }

    public Task SetVariableAsync(
        Guid characterId,
        string variableId,
        int value,
        CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(variableId))
        {
            return Task.CompletedTask;
        }

        _variables[(characterId, variableId)] = value;
        return Task.CompletedTask;
    }

    public Task AddVariableAsync(
        Guid characterId,
        string variableId,
        int delta,
        CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(variableId))
        {
            return Task.CompletedTask;
        }

        _variables.AddOrUpdate(
            (characterId, variableId),
            delta,
            (_, existing) => existing + delta);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, int>> GetAllVariablesAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty)
        {
            return Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
        }

        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in _variables)
        {
            if (kv.Key.CharacterId == characterId)
            {
                dict[kv.Key.VariableId] = kv.Value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, int>>(dict);
    }
}
