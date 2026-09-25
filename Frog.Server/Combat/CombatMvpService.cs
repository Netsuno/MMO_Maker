using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Services;

namespace Frog.Server.Combat;

/// <summary>
/// Scaffolding mêlée MVP : mannequin in-memory + range / facing / rate-limit,
/// plus poison (DoT) et étourdissement court. Règle de pile : refresh.
/// Réutilise les formules Phase 7. Pas de table PostgreSQL —
/// voir docs/progress/combat/STATUS.md.
/// </summary>
public sealed class CombatMvpService
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly Dictionary<int, DummyState> _dummies = new();
    private readonly List<ActiveStatus> _effects = new();

    /// <summary>
    /// Sérialise un coup sur le mannequin (calcul + envoi) avec une pulsation DoT,
    /// pour qu'un tic ne dépasse pas le paquet du coup sur la socket.
    /// </summary>
    public async Task RunTurnAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await _turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _turn.Release();
        }
    }

    public CombatTarget EnsureDummy(int mapId, int? pixelX = null, int? pixelY = null, int? hp = null)
    {
        var (spawnX, spawnY) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        lock (_gate)
        {
            if (!_dummies.TryGetValue(mapId, out var dummy) || dummy.Hp <= 0)
            {
                dummy = new DummyState(
                    CombatMvpLimits.DummyId,
                    mapId,
                    pixelX ?? spawnX,
                    pixelY ?? spawnY,
                    hp ?? CombatMvpLimits.DummyMaxHp,
                    CombatMvpLimits.DummyMaxHp);
                _dummies[mapId] = dummy;
            }
            else if (pixelX is int px && pixelY is int py)
            {
                dummy.PixelX = px;
                dummy.PixelY = py;
            }

            return dummy.ToTarget();
        }
    }

    public CombatTarget? FindDummy(int mapId)
    {
        lock (_gate)
        {
            return _dummies.TryGetValue(mapId, out var dummy) ? dummy.ToTarget() : null;
        }
    }

    public bool IsDummyRequest(AttackRequest request)
        => request.Kind == CombatTargetKind.Dummy
           || request.TargetId == CombatMvpLimits.DummyId
           || string.Equals(request.TargetName, CombatMvpLimits.DummyName, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<StatusEffect> Snapshot(int mapId, Guid targetId)
    {
        lock (_gate)
        {
            var list = new List<StatusEffect>();
            for (var i = 0; i < _effects.Count; i++)
            {
                var effect = _effects[i];
                if (effect.MapId == mapId && effect.TargetId == targetId)
                {
                    list.Add(effect.ToEffect());
                }
            }

            return list;
        }
    }

    public bool IsStunned(int mapId, Guid targetId)
    {
        lock (_gate)
        {
            return IsStunnedUnlocked(mapId, targetId);
        }
    }

    /// <summary>Pose ou rafraîchit un effet. Un seul par (carte, cible, kind).</summary>
    public StatusEffectEvent Apply(
        int mapId,
        Guid sourceId,
        Guid targetId,
        StatusEffectKind kind,
        string targetName,
        CombatTargetKind targetKind)
    {
        lock (_gate)
        {
            return ApplyUnlocked(mapId, sourceId, targetId, kind, targetName, targetKind);
        }
    }

    public CombatMvpApplyResult TryMelee(Session attacker, AttackRequest request)
    {
        if (attacker.IsDead)
        {
            return CombatMvpApplyResult.Fail("Personnage mort.");
        }

        if (!attacker.HasActiveCharacter())
        {
            return CombatMvpApplyResult.Fail("Aucun personnage actif.");
        }

        var now = DateTime.UtcNow;
        if ((now - attacker.LastMeleeUtc).TotalMilliseconds < CombatFormulas.BasicAttackCooldownMs)
        {
            return CombatMvpApplyResult.Fail("Attaque en recharge.");
        }

        if (!IsDummyRequest(request))
        {
            return CombatMvpApplyResult.Fail("Cible invalide.");
        }

        lock (_gate)
        {
            if (attacker.CharacterGuid is Guid self && IsStunnedUnlocked(attacker.CurrentMapId, self))
            {
                return CombatMvpApplyResult.Fail("Étourdi.");
            }

            var dummy = EnsureDummyUnlocked(attacker.CurrentMapId);
            if (!MeleeCombat.IsWithinMeleeRange(
                    attacker.PixelX,
                    attacker.PixelY,
                    dummy.PixelX,
                    dummy.PixelY,
                    CombatFormulas.AttackRangePixels(request.Style)))
            {
                return CombatMvpApplyResult.Fail("Hors portee.");
            }

            if (!CombatMvpWire.FacesTarget(
                    attacker.Facing,
                    attacker.PixelX,
                    attacker.PixelY,
                    dummy.PixelX,
                    dummy.PixelY))
            {
                return CombatMvpApplyResult.Fail("Pas en face de la cible.");
            }

            if (dummy.Hp <= 0)
            {
                dummy.Respawn();
                ClearAllUnlocked(attacker.CurrentMapId, dummy.Id, attacker.CharacterGuid ?? attacker.Id);
            }

            var damage = CombatFormulas.MeleeDamage(attacker.Stats?.Str ?? 10, 0, CombatMvpLimits.DummyVit);
            dummy.Hp = Math.Max(0, dummy.Hp - damage);
            var killed = dummy.Hp <= 0;
            attacker.LastMeleeUtc = now;
            var sourceId = attacker.CharacterGuid ?? attacker.Id;

            var ev = new DamageEvent(
                sourceId,
                dummy.Id,
                CombatTargetKind.Dummy,
                CombatMvpLimits.DummyName,
                damage,
                dummy.Hp,
                dummy.MaxHp,
                Hit: true,
                killed,
                Ranged: request.Style == AttackStyle.Ranged);

            StatusEffectEvent? status = null;
            if (killed)
            {
                dummy.Respawn();
                status = ClearAllUnlocked(attacker.CurrentMapId, dummy.Id, sourceId);
            }
            else if (request.ApplyStatus is StatusEffectKind.Poison or StatusEffectKind.Stun)
            {
                status = ApplyUnlocked(
                    attacker.CurrentMapId,
                    sourceId,
                    dummy.Id,
                    request.ApplyStatus,
                    CombatMvpLimits.DummyName,
                    CombatTargetKind.Dummy);
            }

            return CombatMvpApplyResult.Ok(killed ? "Mannequin vaincu." : "Touche.", ev, status);
        }
    }

    /// <summary>Un tic pour chaque effet actif. Le poison blesse le mannequin ; l'étourdissement compte seulement.</summary>
    public IReadOnlyList<StatusPulse> Tick()
    {
        lock (_gate)
        {
            if (_effects.Count == 0)
            {
                return Array.Empty<StatusPulse>();
            }

            var pulses = new List<StatusPulse>();
            var pending = _effects.ToArray();
            var dead = new HashSet<(int MapId, Guid TargetId)>();
            foreach (var effect in pending)
            {
                var key = (effect.MapId, effect.TargetId);
                if (dead.Contains(key) || !_effects.Contains(effect))
                {
                    continue;
                }

                var damage = 0;
                var killed = false;
                var hp = 0;
                var maxHp = 0;
                if (effect.TargetKind == CombatTargetKind.Dummy)
                {
                    var dummy = EnsureDummyUnlocked(effect.MapId);
                    hp = dummy.Hp;
                    maxHp = dummy.MaxHp;
                    if (effect.Kind == StatusEffectKind.Poison && effect.Potency > 0)
                    {
                        damage = effect.Potency;
                        dummy.Hp = Math.Max(0, dummy.Hp - damage);
                        killed = dummy.Hp <= 0;
                        hp = dummy.Hp;
                        if (killed)
                        {
                            dummy.Respawn();
                        }
                    }
                }

                effect.RemainingTicks -= 1;
                if (killed)
                {
                    dead.Add(key);
                    pulses.RemoveAll(pulse => pulse.MapId == effect.MapId && pulse.Status.TargetId == effect.TargetId);
                    _effects.RemoveAll(item => item.MapId == effect.MapId && item.TargetId == effect.TargetId);
                    var clear = new StatusEffectEvent(
                        Guid.Empty,
                        StatusEffectKind.None,
                        effect.SourceId,
                        effect.TargetId,
                        0,
                        effect.Potency,
                        StatusEffectOp.Clear);
                    pulses.Add(new StatusPulse(
                        effect.MapId,
                        effect.TargetName,
                        PulseMessage(effect, StatusEffectOp.Clear, killed: true),
                        PulseDamage(effect, damage, 0, maxHp, killed: true),
                        clear));
                    continue;
                }

                if (effect.RemainingTicks <= 0)
                {
                    _effects.Remove(effect);
                    var clear = ToEvent(effect, StatusEffectOp.Clear);
                    clear = clear with { RemainingTicks = 0 };
                    pulses.Add(new StatusPulse(
                        effect.MapId,
                        effect.TargetName,
                        PulseMessage(effect, StatusEffectOp.Clear, killed: false),
                        PulseDamage(effect, damage, hp, maxHp, killed: false),
                        clear));
                    continue;
                }

                pulses.Add(new StatusPulse(
                    effect.MapId,
                    effect.TargetName,
                    PulseMessage(effect, StatusEffectOp.Tick, killed: false),
                    PulseDamage(effect, damage, hp, maxHp, killed: false),
                    ToEvent(effect, StatusEffectOp.Tick)));
            }

            return pulses;
        }
    }

    private bool IsStunnedUnlocked(int mapId, Guid targetId)
    {
        for (var i = 0; i < _effects.Count; i++)
        {
            var effect = _effects[i];
            if (effect.MapId == mapId
                && effect.TargetId == targetId
                && effect.Kind == StatusEffectKind.Stun
                && effect.RemainingTicks > 0)
            {
                return true;
            }
        }

        return false;
    }

    private StatusEffectEvent ApplyUnlocked(
        int mapId,
        Guid sourceId,
        Guid targetId,
        StatusEffectKind kind,
        string targetName,
        CombatTargetKind targetKind)
    {
        var duration = kind == StatusEffectKind.Stun
            ? StatusEffectLimits.StunTicks
            : StatusEffectLimits.PoisonTicks;
        var potency = kind == StatusEffectKind.Poison ? StatusEffectLimits.PoisonPotency : 0;
        var existing = FindUnlocked(mapId, targetId, kind);
        if (existing is not null)
        {
            existing.SourceId = sourceId;
            existing.RemainingTicks = duration;
            existing.Potency = potency;
            existing.TargetName = targetName;
            existing.TargetKind = targetKind;
            return ToEvent(existing, StatusEffectOp.Apply);
        }

        var created = new ActiveStatus
        {
            EffectId = Guid.NewGuid(),
            Kind = kind,
            SourceId = sourceId,
            TargetId = targetId,
            MapId = mapId,
            RemainingTicks = duration,
            Potency = potency,
            TargetName = targetName,
            TargetKind = targetKind,
        };
        _effects.Add(created);
        return ToEvent(created, StatusEffectOp.Apply);
    }

    private StatusEffectEvent? ClearAllUnlocked(int mapId, Guid targetId, Guid sourceId)
    {
        var any = false;
        for (var i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].MapId == mapId && _effects[i].TargetId == targetId)
            {
                any = true;
                break;
            }
        }

        if (!any)
        {
            return null;
        }

        _effects.RemoveAll(effect => effect.MapId == mapId && effect.TargetId == targetId);
        return new StatusEffectEvent(
            Guid.Empty,
            StatusEffectKind.None,
            sourceId,
            targetId,
            0,
            0,
            StatusEffectOp.Clear);
    }

    private ActiveStatus? FindUnlocked(int mapId, Guid targetId, StatusEffectKind kind)
    {
        for (var i = 0; i < _effects.Count; i++)
        {
            var effect = _effects[i];
            if (effect.MapId == mapId && effect.TargetId == targetId && effect.Kind == kind)
            {
                return effect;
            }
        }

        return null;
    }

    private DummyState EnsureDummyUnlocked(int mapId)
    {
        if (_dummies.TryGetValue(mapId, out var dummy))
        {
            return dummy;
        }

        var (spawnX, spawnY) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        dummy = new DummyState(
            CombatMvpLimits.DummyId,
            mapId,
            spawnX,
            spawnY,
            CombatMvpLimits.DummyMaxHp,
            CombatMvpLimits.DummyMaxHp);
        _dummies[mapId] = dummy;
        return dummy;
    }

    private static StatusEffectEvent ToEvent(ActiveStatus effect, StatusEffectOp op)
        => new(
            effect.EffectId,
            effect.Kind,
            effect.SourceId,
            effect.TargetId,
            effect.RemainingTicks,
            effect.Potency,
            op);

    private static DamageEvent PulseDamage(ActiveStatus effect, int damage, int hp, int maxHp, bool killed)
        => new(
            effect.SourceId,
            effect.TargetId,
            effect.TargetKind,
            effect.TargetName,
            damage,
            hp,
            maxHp,
            Hit: true,
            killed);

    private static string PulseMessage(ActiveStatus effect, StatusEffectOp op, bool killed)
    {
        if (killed)
        {
            return effect.TargetKind == CombatTargetKind.Dummy
                ? "Mannequin vaincu."
                : effect.TargetName + " vaincu.";
        }

        if (op == StatusEffectOp.Clear)
        {
            return effect.Kind switch
            {
                StatusEffectKind.Poison => "Poison dissipé.",
                StatusEffectKind.Stun => "Étourdissement dissipé.",
                _ => "Effets dissipés.",
            };
        }

        return effect.Kind == StatusEffectKind.Stun ? "Étourdissement." : "Poison.";
    }

    private sealed class DummyState(Guid id, int mapId, int pixelX, int pixelY, int hp, int maxHp)
    {
        public Guid Id { get; } = id;
        public int MapId { get; } = mapId;
        public int PixelX { get; set; } = pixelX;
        public int PixelY { get; set; } = pixelY;
        public int Hp { get; set; } = hp;
        public int MaxHp { get; } = maxHp;

        public void Respawn() => Hp = MaxHp;

        public CombatTarget ToTarget()
            => CombatTarget.Dummy(MapId, PixelX, PixelY, Hp, MaxHp);
    }

    private sealed class ActiveStatus
    {
        public Guid EffectId { get; init; }
        public StatusEffectKind Kind { get; init; }
        public Guid SourceId { get; set; }
        public Guid TargetId { get; init; }
        public int MapId { get; init; }
        public int RemainingTicks { get; set; }
        public int Potency { get; set; }
        public string TargetName { get; set; } = string.Empty;
        public CombatTargetKind TargetKind { get; set; }
        public StatusEffect ToEffect()
            => new(EffectId, Kind, SourceId, TargetId, RemainingTicks, Potency);
    }
}

public readonly record struct StatusPulse(
    int MapId,
    string TargetName,
    string Message,
    DamageEvent Damage,
    StatusEffectEvent Status);

public readonly record struct CombatMvpApplyResult(
    bool Success,
    string Message,
    DamageEvent? Damage,
    StatusEffectEvent? Status = null)
{
    public static CombatMvpApplyResult Fail(string message) => new(false, message, null);

    public static CombatMvpApplyResult Ok(string message, DamageEvent damage, StatusEffectEvent? status = null)
        => new(true, message, damage, status);
}
