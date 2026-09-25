using System.Collections.Concurrent;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Server.Combat;
using Frog.Server.Models;
using Frog.Server.Services;

namespace Frog.Server.Gameplay;

public sealed record MonsterAiPulse(
    CombatTargetKind Kind,
    Guid ActorId,
    string Name,
    int MapId,
    int PixelX,
    int PixelY,
    AttackStyle Style,
    Session? Victim,
    PlayerMeleeCombatResult? Strike);

/// <summary>
/// Tic in-memory : monstres vivants (et mannequin déjà posé) aggro, poursuivent, frappent.
/// Aucune table PostgreSQL.
/// </summary>
public sealed class MonsterAiService
{
    private readonly CombatGameplayService _combat;
    private readonly Func<IReadOnlyCollection<Session>> _sessions;
    private readonly MonsterAiWorld _world;
    private readonly CombatMvpService? _dummies;
    private readonly ConcurrentDictionary<(int MapId, Guid ActorId), MonsterAiMemory> _memory = new();

    public MonsterAiService(
        CombatGameplayService combat,
        Func<IReadOnlyCollection<Session>> sessions,
        MonsterAiWorld world,
        CombatMvpService? dummies = null)
    {
        _combat = combat;
        _sessions = sessions;
        _world = world;
        _dummies = dummies;
    }

    public MonsterAiService(
        CombatGameplayService combat,
        ConnectionManager connections,
        MapService maps,
        CombatMvpService dummies)
        : this(combat, connections.GetActiveSessions, MonsterAiWorld.FromMaps(maps), dummies)
    {
    }

    public Guid? TargetOf(int mapId, Guid actorId)
        => _memory.TryGetValue((mapId, actorId), out var memory) ? memory.TargetId : null;

    public async Task<IReadOnlyList<MonsterAiPulse>> TickAsync(DateTime utcNow, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var actors = BuildActors();
        var pulses = new List<MonsterAiPulse>();
        var live = new HashSet<(int MapId, Guid ActorId)>();

        foreach (var mapId in _combat.ListMonsterMapIds())
        {
            foreach (var monster in _combat.ListMonstersOnMap(mapId))
            {
                ct.ThrowIfCancellationRequested();
                var key = (monster.MapId, monster.InstanceId);
                live.Add(key);
                var pulse = await TickMonsterAsync(monster, actors, utcNow, ct).ConfigureAwait(false);
                if (pulse is not null)
                {
                    pulses.Add(pulse);
                }
            }
        }

        if (_dummies is not null)
        {
            foreach (var dummy in _dummies.ListLiveDummies())
            {
                ct.ThrowIfCancellationRequested();
                var key = (dummy.MapId, dummy.TargetId);
                live.Add(key);
                var pulse = await TickDummyAsync(dummy, actors, utcNow, ct).ConfigureAwait(false);
                if (pulse is not null)
                {
                    pulses.Add(pulse);
                }
            }
        }

        foreach (var key in _memory.Keys)
        {
            if (!live.Contains(key))
            {
                _memory.TryRemove(key, out _);
            }
        }

        return pulses;
    }

    private IReadOnlyList<MonsterAiActor> BuildActors()
    {
        var sessions = _sessions();
        var actors = new List<MonsterAiActor>(sessions.Count);
        foreach (var session in sessions)
        {
            actors.Add(new MonsterAiActor(
                session.Id,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY,
                session.HasActiveCharacter() && !session.IsDead && session.Hp > 0));
        }

        return actors;
    }

    private async Task<MonsterAiPulse?> TickMonsterAsync(
        MonsterInstance monster,
        IReadOnlyList<MonsterAiActor> actors,
        DateTime utcNow,
        CancellationToken ct)
    {
        var memory = _memory.GetOrAdd((monster.MapId, monster.InstanceId), _ => new MonsterAiMemory());
        _world.Query(monster.MapId, out var width, out var height, out var blocked);
        var intent = MonsterAi.Decide(
            monster.MapId,
            monster.PixelX,
            monster.PixelY,
            monster.Hp,
            memory,
            actors,
            utcNow,
            width,
            height,
            blocked);
        var x = monster.PixelX;
        var y = monster.PixelY;
        if (intent.X != x || intent.Y != y)
        {
            if (!await _combat.TryMoveMonsterAsync(monster.MapId, monster.InstanceId, intent.X, intent.Y, ct)
                    .ConfigureAwait(false))
            {
                return null;
            }

            x = intent.X;
            y = intent.Y;
        }

        return await FinishPulseAsync(
            CombatTargetKind.Monster,
            monster.InstanceId,
            monster.Name,
            monster.MapId,
            x,
            y,
            Math.Max(1, monster.Level * 2),
            intent,
            memory,
            utcNow,
            ct).ConfigureAwait(false);
    }

    private async Task<MonsterAiPulse?> TickDummyAsync(
        CombatTarget dummy,
        IReadOnlyList<MonsterAiActor> actors,
        DateTime utcNow,
        CancellationToken ct)
    {
        var memory = _memory.GetOrAdd((dummy.MapId, dummy.TargetId), _ => new MonsterAiMemory());
        _world.Query(dummy.MapId, out var width, out var height, out var blocked);
        var intent = MonsterAi.Decide(
            dummy.MapId,
            dummy.PixelX,
            dummy.PixelY,
            dummy.Hp,
            memory,
            actors,
            utcNow,
            width,
            height,
            blocked);
        if (intent.Order == MonsterAiOrder.Attack
            && _dummies is not null
            && _dummies.IsStunned(dummy.MapId, dummy.TargetId))
        {
            intent = intent with { Order = MonsterAiOrder.Hold };
        }

        var x = dummy.PixelX;
        var y = dummy.PixelY;
        if ((intent.X != x || intent.Y != y) && _dummies is not null && _dummies.TryMoveDummy(dummy.MapId, intent.X, intent.Y))
        {
            x = intent.X;
            y = intent.Y;
        }

        return await FinishPulseAsync(
            CombatTargetKind.Dummy,
            dummy.TargetId,
            dummy.Name,
            dummy.MapId,
            x,
            y,
            Math.Max(1, CombatMvpLimits.DummyLevel * 2),
            intent,
            memory,
            utcNow,
            ct).ConfigureAwait(false);
    }

    private async Task<MonsterAiPulse> FinishPulseAsync(
        CombatTargetKind kind,
        Guid actorId,
        string name,
        int mapId,
        int x,
        int y,
        int attackerStr,
        MonsterAiIntent intent,
        MonsterAiMemory memory,
        DateTime utcNow,
        CancellationToken ct)
    {
        Session? victim = null;
        PlayerMeleeCombatResult? strike = null;
        if (intent.Order == MonsterAiOrder.Attack && intent.TargetId is Guid targetId)
        {
            victim = FindSession(targetId);
            if (victim is null)
            {
                memory.TargetId = null;
            }
            else
            {
                var range = CombatFormulas.AttackRangePixels(intent.Style);
                strike = await _combat.TryApplyCreatureStrikeAsync(
                    victim,
                    mapId,
                    x,
                    y,
                    attackerStr,
                    range,
                    ct).ConfigureAwait(false);
                if (strike.Success)
                {
                    memory.LastAttackUtc = utcNow;
                    if (strike.TargetKilled)
                    {
                        memory.TargetId = null;
                    }
                }
            }
        }

        return new MonsterAiPulse(kind, actorId, name, mapId, x, y, intent.Style, victim, strike);
    }

    private Session? FindSession(Guid sessionId)
    {
        foreach (var session in _sessions())
        {
            if (session.Id == sessionId)
            {
                return session;
            }
        }

        return null;
    }
}
