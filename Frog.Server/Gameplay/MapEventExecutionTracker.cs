using System.Collections.Concurrent;
using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Suivi des exécutions autorun/parallel/wait par personnage (P8-R3).</summary>
public sealed class MapEventExecutionTracker
{
    private readonly ConcurrentDictionary<string, byte> _activeParallel = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _firedAutorun = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, List<PendingWaitResume>> _pendingWaits = new();

    public bool TryBeginParallel(Guid characterId, long placementId, Guid eventDefinitionId, int mapId)
    {
        _ = mapId;
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

    public void RegisterWait(Guid characterId, PendingWaitResume resume)
    {
        var list = _pendingWaits.GetOrAdd(characterId, static _ => []);
        lock (list)
        {
            if (list.Count >= MapEventRuntimeLimits.MaxActiveExecutionsPerCharacter)
            {
                list.RemoveAt(0);
            }

            list.Add(resume);
        }
    }

    public IReadOnlyList<PendingWaitResume> TakeReadyWaits(Guid characterId, DateTimeOffset nowUtc)
    {
        if (!_pendingWaits.TryGetValue(characterId, out var list))
        {
            return Array.Empty<PendingWaitResume>();
        }

        lock (list)
        {
            if (list.Count == 0)
            {
                return Array.Empty<PendingWaitResume>();
            }

            var ready = new List<PendingWaitResume>();
            var remaining = new List<PendingWaitResume>();
            foreach (var wait in list)
            {
                if (wait.WaitUntilUtc <= nowUtc)
                {
                    ready.Add(wait);
                }
                else
                {
                    remaining.Add(wait);
                }
            }

            list.Clear();
            list.AddRange(remaining);
            return ready;
        }
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

        _pendingWaits.TryRemove(characterId, out _);
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

/// <summary>Reprise différée après commande wait.</summary>
public sealed record PendingWaitResume(
    DateTimeOffset WaitUntilUtc,
    IReadOnlyList<MapEventCommandDefinition> RemainingCommands,
    string? PlacementLabel = null);
