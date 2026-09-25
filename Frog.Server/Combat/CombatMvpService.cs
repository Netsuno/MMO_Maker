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
/// Scaffolding mêlée MVP : mannequin in-memory + range / facing / rate-limit.
/// Réutilise les formules Phase 7. TODO persistance + IA monstre —
/// voir docs/progress/combat/STATUS.md.
/// </summary>
public sealed class CombatMvpService
{
    private readonly object _gate = new();
    private readonly Dictionary<int, DummyState> _dummies = new();

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
            }

            var damage = CombatFormulas.MeleeDamage(attacker.Stats?.Str ?? 10, 0, CombatMvpLimits.DummyVit);
            dummy.Hp = Math.Max(0, dummy.Hp - damage);
            var killed = dummy.Hp <= 0;
            attacker.LastMeleeUtc = now;

            var ev = new DamageEvent(
                attacker.CharacterGuid ?? attacker.Id,
                dummy.Id,
                CombatTargetKind.Dummy,
                CombatMvpLimits.DummyName,
                damage,
                dummy.Hp,
                dummy.MaxHp,
                Hit: true,
                killed,
                Ranged: request.Style == AttackStyle.Ranged);

            if (killed)
            {
                dummy.Respawn();
            }

            return CombatMvpApplyResult.Ok(killed ? "Mannequin vaincu." : "Touche.", ev);
        }
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
}

public readonly record struct CombatMvpApplyResult(bool Success, string Message, DamageEvent? Damage)
{
    public static CombatMvpApplyResult Fail(string message) => new(false, message, null);

    public static CombatMvpApplyResult Ok(string message, DamageEvent damage) => new(true, message, damage);
}
