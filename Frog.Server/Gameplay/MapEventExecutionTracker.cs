using System.Collections.Concurrent;
using Frog.Core.Events;

namespace Frog.Server.Gameplay;

/// <summary>Suivi des exécutions autorun/parallel par personnage (P8-R3).</summary>
public sealed class MapEventExecutionTracker
{
    private readonly ConcurrentDictionary<string, byte> _activeParallel = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _firedAutorun = new(StringComparer.Ordinal);

    public bool TryBeginParallel(Guid characterId, long placementId, Guid eventDefinitionId)
    {
        var key = BuildKey(characterId, placementId, eventDefinitionId, Phase8MapEventTriggerKinds.Parallel);
        return _activeParallel.TryAdd(key, 0);
    }

    public void EndParallel(Guid characterId, long placementId, Guid eventDefinitionId)
    {
        var key = BuildKey(characterId, placementId, eventDefinitionId, Phase8MapEventTriggerKinds.Parallel);
        _activeParallel.TryRemove(key, out _);
    }

    public bool TryFireAutorunOnce(Guid characterId, long placementId, Guid eventDefinitionId, int mapId)
    {
        var key = $"{characterId:N}:{mapId}:{placementId}:{eventDefinitionId:N}:autorun";
        return _firedAutorun.TryAdd(key, 0);
    }

    public void ClearForCharacter(Guid characterId)
    {
        var prefix = characterId.ToString("N") + ":";
        foreach (var key in _activeParallel.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _activeParallel.TryRemove(key, out _);
        }

        foreach (var key in _firedAutorun.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _firedAutorun.TryRemove(key, out _);
        }
    }

    public void ClearAutorunForMap(Guid characterId, int mapId)
    {
        var prefix = $"{characterId:N}:{mapId}:";
        foreach (var key in _firedAutorun.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _firedAutorun.TryRemove(key, out _);
        }
    }

    private static string BuildKey(Guid characterId, long placementId, Guid eventDefinitionId, string trigger) =>
        $"{characterId:N}:{placementId}:{eventDefinitionId:N}:{trigger}";
}
