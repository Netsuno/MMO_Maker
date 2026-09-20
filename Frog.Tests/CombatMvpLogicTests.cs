using System;
using Frog.Application.Gameplay;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Server.Combat;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class CombatMvpLogicTests
{
    [Fact]
    public void Melee_WithoutCharacter_Fails()
    {
        var svc = new CombatMvpService();
        var session = new Session { Id = Guid.NewGuid(), Username = "u" };
        var result = svc.TryMelee(session, DummyAttack());
        Assert.False(result.Success);
        Assert.Contains("Personnage", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Damage);
    }

    [Fact]
    public void Melee_Dead_Fails()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        session.IsDead = true;
        var result = svc.TryMelee(session, DummyAttack());
        Assert.False(result.Success);
        Assert.Equal("Personnage mort.", result.Message);
    }

    [Fact]
    public void Melee_DummyInRange_AppliesDamageAndRateLimit()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        var dummy = svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY);
        Assert.Equal(CombatMvpLimits.DummyName, dummy.Name);
        Assert.Equal(CombatMvpLimits.DummyMaxHp, dummy.Hp);

        var hit = svc.TryMelee(session, DummyAttack());
        Assert.True(hit.Success);
        Assert.NotNull(hit.Damage);
        Assert.True(hit.Damage!.Value.Hit);
        Assert.Equal(CombatTargetKind.Dummy, hit.Damage.Value.TargetKind);
        Assert.Equal(CombatMvpLimits.DummyMaxHp - hit.Damage.Value.Damage, hit.Damage.Value.RemainingHp);
        Assert.Equal("Touche.", hit.Message);

        var retry = svc.TryMelee(session, DummyAttack());
        Assert.False(retry.Success);
        Assert.Equal("Attaque en recharge.", retry.Message);

        session.LastMeleeUtc = DateTime.UtcNow.AddSeconds(-2);
        var after = svc.FindDummy(session.CurrentMapId);
        Assert.NotNull(after);
        Assert.True(after!.Value.Hp < CombatMvpLimits.DummyMaxHp);
    }

    [Fact]
    public void Melee_OutOfRange_Fails()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY + 400);
        var result = svc.TryMelee(session, DummyAttack());
        Assert.False(result.Success);
        Assert.Equal("Hors portee.", result.Message);
    }

    [Fact]
    public void Melee_WrongFacing_Fails()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        session.Facing = Direction.Left;
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY + 32);
        var result = svc.TryMelee(session, DummyAttack());
        Assert.False(result.Success);
        Assert.Equal("Pas en face de la cible.", result.Message);
    }

    [Fact]
    public void Melee_KillsDummy_ThenRespawns()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        session.Stats = new CharacterStats(99, 10, 10, 10, 10, 10);
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY, hp: 1);
        var hit = svc.TryMelee(session, DummyAttack());
        Assert.True(hit.Success);
        Assert.True(hit.Damage!.Value.Killed);
        Assert.Equal(0, hit.Damage.Value.RemainingHp);
        Assert.Equal("Mannequin vaincu.", hit.Message);
        var after = svc.FindDummy(session.CurrentMapId);
        Assert.NotNull(after);
        Assert.Equal(CombatMvpLimits.DummyMaxHp, after!.Value.Hp);
    }

    [Fact]
    public void Melee_UnknownTarget_Fails()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        var result = svc.TryMelee(session, new AttackRequest("Slime", CombatTargetKind.Monster, Direction.Down, Guid.Empty));
        Assert.False(result.Success);
        Assert.Equal("Cible invalide.", result.Message);
    }

    private static AttackRequest DummyAttack()
        => new(CombatMvpLimits.DummyName, CombatTargetKind.Dummy, Direction.Down, CombatMvpLimits.DummyId);

    private static Session SessionOf(Guid characterId)
    {
        var (px, py) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        return new Session
        {
            Id = Guid.NewGuid(),
            Username = "hero",
            CharacterGuid = characterId,
            CurrentMapId = 1,
            PixelX = px,
            PixelY = py,
            Facing = Direction.Down,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        };
    }
}
