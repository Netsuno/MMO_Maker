using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Constants;

namespace Frog.Server.Gameplay;

/// <summary>Etat monstre en memoire avec verrou par instance (RMW atomique + vainc unique).</summary>
public sealed class CombatMutationRepository : ICombatMutationRepository
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, CombatMonsterSnapshot>> _monstersByMap = new();
    private readonly ConcurrentDictionary<Guid, object> _monsterLocks = new();

    public Task<CombatMonsterSnapshot?> SpawnMonsterAsync(
        int mapId,
        Guid npcDefinitionId,
        string name,
        int level,
        int pixelX,
        int pixelY,
        int maxHp,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instance = new CombatMonsterSnapshot(
            Guid.NewGuid(),
            npcDefinitionId,
            name,
            mapId,
            pixelX,
            pixelY,
            maxHp,
            maxHp,
            level);
        var map = _monstersByMap.GetOrAdd(mapId, static _ => new ConcurrentDictionary<Guid, CombatMonsterSnapshot>());
        map[instance.InstanceId] = instance;
        _monsterLocks.TryAdd(instance.InstanceId, new object());
        return Task.FromResult<CombatMonsterSnapshot?>(instance);
    }

    public IReadOnlyList<CombatMonsterSnapshot> ListMonstersOnMap(int mapId)
    {
        if (!_monstersByMap.TryGetValue(mapId, out var map))
        {
            return Array.Empty<CombatMonsterSnapshot>();
        }

        return map.Values.ToArray();
    }

    public Task<CombatMonsterDamageAttemptResult> TryApplyDamageToNamedTargetAsync(
        int mapId,
        string targetName,
        int attackerPixelX,
        int attackerPixelY,
        int rangePixels,
        int damage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_monstersByMap.TryGetValue(mapId, out var map))
        {
            return Task.FromResult(CombatMonsterDamageAttemptResult.Fail("Aucun monstre."));
        }

        var candidate = FindMonsterInRange(map.Values, targetName, attackerPixelX, attackerPixelY, rangePixels);
        if (candidate is null)
        {
            return Task.FromResult(CombatMonsterDamageAttemptResult.Fail("Monstre hors portee ou introuvable."));
        }

        var gate = _monsterLocks.GetOrAdd(candidate.InstanceId, static _ => new object());
        lock (gate)
        {
            if (!map.TryGetValue(candidate.InstanceId, out var monster))
            {
                return Task.FromResult(CombatMonsterDamageAttemptResult.Fail("Monstre deja vaincu."));
            }

            if (monster.Hp <= 0)
            {
                map.TryRemove(monster.InstanceId, out _);
                _monsterLocks.TryRemove(monster.InstanceId, out _);
                return Task.FromResult(CombatMonsterDamageAttemptResult.Fail("Monstre deja vaincu."));
            }

            var newHp = Math.Max(0, monster.Hp - damage);
            if (newHp <= 0)
            {
                if (!map.TryRemove(monster.InstanceId, out var removed))
                {
                    return Task.FromResult(CombatMonsterDamageAttemptResult.Fail("Monstre deja vaincu."));
                }

                _monsterLocks.TryRemove(removed.InstanceId, out _);
                return Task.FromResult(
                    CombatMonsterDamageAttemptResult.Killed(removed with { Hp = 0 }, damage));
            }

            var updated = monster with { Hp = newHp };
            map[monster.InstanceId] = updated;
            return Task.FromResult(CombatMonsterDamageAttemptResult.Hit(updated, damage));
        }
    }

    public Task<bool> TryRestoreMonsterAsync(
        CombatMonsterSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.Hp <= 0)
        {
            return Task.FromResult(false);
        }

        var map = _monstersByMap.GetOrAdd(snapshot.MapId, static _ => new ConcurrentDictionary<Guid, CombatMonsterSnapshot>());
        if (map.ContainsKey(snapshot.InstanceId))
        {
            return Task.FromResult(false);
        }

        map[snapshot.InstanceId] = snapshot;
        _monsterLocks.TryAdd(snapshot.InstanceId, new object());
        return Task.FromResult(true);
    }

    private static CombatMonsterSnapshot? FindMonsterInRange(
        IEnumerable<CombatMonsterSnapshot> monsters,
        string targetName,
        int attackerX,
        int attackerY,
        int rangePixels)
    {
        CombatMonsterSnapshot? best = null;
        long bestDist = long.MaxValue;
        foreach (var monster in monsters)
        {
            if (monster.Hp <= 0
                || !string.Equals(monster.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dist = WorldMetrics.DistanceSquaredPixels(attackerX, attackerY, monster.PixelX, monster.PixelY);
            if (dist <= (long)rangePixels * rangePixels && dist < bestDist)
            {
                best = monster;
                bestDist = dist;
            }
        }

        return best;
    }
}
