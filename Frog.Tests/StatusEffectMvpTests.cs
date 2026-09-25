using System;
using System.IO;
using Frog.Application.Gameplay;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Server.Combat;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class StatusEffectMvpTests
{
    [Fact]
    public void Protocol_StaysV11_StatusTrailerIsAdditive()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(17, (byte)PacketId.MeleeAttackRequest);
        Assert.Equal(18, (byte)PacketId.MeleeAttackResult);
        Assert.Equal(46, CombatMvpLimits.DamageEventTrailerBytes);
        Assert.Equal(54, CombatMvpLimits.StatusEffectTrailerBytes);
        Assert.Equal(StatusEffectLimits.TrailerBytes, CombatMvpLimits.StatusEffectTrailerBytes);
        Assert.Equal("refresh", StatusEffectLimits.StackRule);
        Assert.Equal(4, StatusEffectLimits.PoisonTicks);
        Assert.Equal(3, StatusEffectLimits.PoisonPotency);
        Assert.Equal(2, StatusEffectLimits.StunTicks);
        Assert.False(CombatFx.ShowSpriteFlash(CombatFxKind.Poison, 0));
        Assert.NotEqual(CombatFx.HitArgb, CombatFx.PoisonArgb);
        Assert.NotEqual(CombatFx.StunArgb, CombatFx.PoisonArgb);
    }

    [Fact]
    public void AttackRequest_StatusByte_IsOptional_UnknownTailIgnored()
    {
        var plain = new AttackRequest(
            CombatMvpLimits.DummyName,
            CombatTargetKind.Dummy,
            Direction.Down,
            CombatMvpLimits.DummyId);
        var plainBody = CombatMvpWire.BuildAttackRequest(plain);
        Assert.True(CombatMvpWire.TryParseAttackRequest(plainBody, out var parsedPlain));
        Assert.Equal(AttackStyle.Melee, parsedPlain.Style);
        Assert.Equal(StatusEffectKind.None, parsedPlain.ApplyStatus);
        Assert.Equal(1 + CombatMvpLimits.DummyName.Length + CombatMvpLimits.AttackExtrasBytes, plainBody.Length);

        var poison = plain with { ApplyStatus = StatusEffectKind.Poison };
        var poisonBody = CombatMvpWire.BuildAttackRequest(poison);
        Assert.Equal(plainBody.Length + 2, poisonBody.Length);
        Assert.True(CombatMvpWire.TryParseAttackRequest(poisonBody, out var parsedPoison));
        Assert.Equal(AttackStyle.Melee, parsedPoison.Style);
        Assert.Equal(StatusEffectKind.Poison, parsedPoison.ApplyStatus);

        var withoutStatus = poisonBody[..^1];
        Assert.True(CombatMvpWire.TryParseAttackRequest(withoutStatus, out var stripped));
        Assert.Equal(AttackStyle.Melee, stripped.Style);
        Assert.Equal(StatusEffectKind.None, stripped.ApplyStatus);

        var unknown = (byte[])poisonBody.Clone();
        unknown[^1] = 9;
        Assert.True(CombatMvpWire.TryParseAttackRequest(unknown, out var ignored));
        Assert.Equal(StatusEffectKind.None, ignored.ApplyStatus);
        Assert.Equal(AttackStyle.Melee, ignored.Style);

        var extra = new byte[poisonBody.Length + 1];
        poisonBody.CopyTo(extra, 0);
        extra[^1] = 0xAB;
        Assert.True(CombatMvpWire.TryParseAttackRequest(extra, out var tailed));
        Assert.Equal(StatusEffectKind.Poison, tailed.ApplyStatus);

        var stun = plain with { Style = AttackStyle.Ranged, ApplyStatus = StatusEffectKind.Stun };
        var stunBody = CombatMvpWire.BuildAttackRequest(stun);
        Assert.True(CombatMvpWire.TryParseAttackRequest(stunBody, out var parsedStun));
        Assert.Equal(AttackStyle.Ranged, parsedStun.Style);
        Assert.Equal(StatusEffectKind.Stun, parsedStun.ApplyStatus);
    }

    [Fact]
    public void MeleeResult_StatusTrailer_RoundTrips_AndLegacyStopsEarly()
    {
        var damage = new DamageEvent(
            Guid.NewGuid(),
            CombatMvpLimits.DummyId,
            CombatTargetKind.Dummy,
            CombatMvpLimits.DummyName,
            10,
            30,
            40,
            Hit: true,
            Killed: false);
        var status = new StatusEffectEvent(
            Guid.NewGuid(),
            StatusEffectKind.Poison,
            damage.AttackerId,
            CombatMvpLimits.DummyId,
            StatusEffectLimits.PoisonTicks,
            StatusEffectLimits.PoisonPotency,
            StatusEffectOp.Apply);
        var body = CombatMvpWire.BuildMeleeResultBody(true, CombatMvpLimits.DummyName, "Touche.", damage, status);
        Assert.True(CombatMvpWire.TryParseMeleeResult(
            body,
            out var hit,
            out var target,
            out var message,
            out var parsedDamage,
            out var parsedStatus));
        Assert.True(hit);
        Assert.Equal(CombatMvpLimits.DummyName, target);
        Assert.Equal("Touche.", message);
        Assert.True(parsedDamage.HasValue);
        Assert.Equal(10, parsedDamage!.Value.Damage);
        Assert.Equal(30, parsedDamage.Value.RemainingHp);
        Assert.True(parsedStatus.HasValue);
        Assert.Equal(status.EffectId, parsedStatus!.Value.EffectId);
        Assert.Equal(StatusEffectKind.Poison, parsedStatus.Value.Kind);
        Assert.Equal(StatusEffectOp.Apply, parsedStatus.Value.Op);
        Assert.Equal(StatusEffectLimits.PoisonTicks, parsedStatus.Value.RemainingTicks);
        Assert.Equal(StatusEffectLimits.PoisonPotency, parsedStatus.Value.Potency);
        Assert.Equal(status.SourceId, parsedStatus.Value.SourceId);

        var legacy = CombatMvpWire.BuildMeleeResultBody(true, CombatMvpLimits.DummyName, "Touche.", damage, null);
        Assert.Equal(body.Length - CombatMvpLimits.StatusEffectTrailerBytes, legacy.Length);
        Assert.True(CombatMvpWire.TryParseMeleeResult(legacy, out _, out _, out _, out var legacyDamage, out var legacyStatus));
        Assert.True(legacyDamage.HasValue);
        Assert.Equal(10, legacyDamage!.Value.Damage);
        Assert.Null(legacyStatus);

        var messageOnly = legacy[..(legacy.Length - CombatMvpLimits.DamageEventTrailerBytes)];
        Assert.True(CombatMvpWire.TryParseMeleeResult(messageOnly, out var hitOnly, out var nameOnly, out var msgOnly, out var noDamage, out var noStatus));
        Assert.True(hitOnly);
        Assert.Equal(CombatMvpLimits.DummyName, nameOnly);
        Assert.Equal("Touche.", msgOnly);
        Assert.Null(noDamage);
        Assert.Null(noStatus);

        var corrupt = (byte[])body.Clone();
        var statusAt = body.Length - CombatMvpLimits.StatusEffectTrailerBytes;
        corrupt[statusAt] = 0;
        Assert.True(CombatMvpWire.TryParseMeleeResult(corrupt, out _, out _, out _, out var kept, out var dropped));
        Assert.True(kept.HasValue);
        Assert.Equal(10, kept!.Value.Damage);
        Assert.Null(dropped);
    }

    [Fact]
    public void Poison_Refresh_DoesNotStack_AndRateLimitDoesNotRefresh()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY);
        var hit = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        Assert.True(hit.Success);
        Assert.NotNull(hit.Status);
        Assert.Equal(StatusEffectOp.Apply, hit.Status!.Value.Op);
        Assert.Equal(StatusEffectLimits.PoisonTicks, hit.Status.Value.RemainingTicks);
        var id = hit.Status.Value.EffectId;
        var hpAfterHit = hit.Damage!.Value.RemainingHp;

        var blocked = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        Assert.False(blocked.Success);
        Assert.Equal("Attaque en recharge.", blocked.Message);
        Assert.Null(blocked.Status);
        var duringCooldown = Assert.Single(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));
        Assert.Equal(id, duringCooldown.EffectId);
        Assert.Equal(StatusEffectLimits.PoisonTicks, duringCooldown.RemainingTicks);

        var firstTick = Assert.Single(svc.Tick());
        Assert.Equal(StatusEffectOp.Tick, firstTick.Status.Op);
        Assert.Equal(StatusEffectLimits.PoisonPotency, firstTick.Damage.Damage);
        Assert.Equal(hpAfterHit - StatusEffectLimits.PoisonPotency, firstTick.Damage.RemainingHp);
        Assert.False(firstTick.Damage.Killed);
        Assert.Equal(StatusEffectLimits.PoisonTicks - 1, firstTick.Status.RemainingTicks);

        session.LastMeleeUtc = DateTime.UtcNow.AddSeconds(-2);
        var refresh = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        Assert.True(refresh.Success);
        Assert.Equal(id, refresh.Status!.Value.EffectId);
        Assert.Equal(StatusEffectLimits.PoisonTicks, refresh.Status.Value.RemainingTicks);
        var only = Assert.Single(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));
        Assert.Equal(StatusEffectLimits.PoisonPotency, only.Potency);
        Assert.Equal(refresh.Damage!.Value.RemainingHp, firstTick.Damage.RemainingHp - refresh.Damage.Value.Damage);

        var afterRefresh = Assert.Single(svc.Tick());
        Assert.Equal(StatusEffectLimits.PoisonPotency, afterRefresh.Damage.Damage);
        Assert.NotEqual(StatusEffectLimits.PoisonPotency * 2, afterRefresh.Damage.Damage);
    }

    [Fact]
    public void Poison_Expires_AfterLastTick_AndDealsThatTick()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY);
        var hit = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        var hp = hit.Damage!.Value.RemainingHp;
        for (var i = 1; i < StatusEffectLimits.PoisonTicks; i++)
        {
            var pulse = Assert.Single(svc.Tick());
            Assert.Equal(StatusEffectOp.Tick, pulse.Status.Op);
            Assert.Equal(StatusEffectKind.Poison, pulse.Status.Kind);
            Assert.Equal(StatusEffectLimits.PoisonTicks - i, pulse.Status.RemainingTicks);
            Assert.Equal("Poison.", pulse.Message);
            hp -= StatusEffectLimits.PoisonPotency;
            Assert.Equal(hp, pulse.Damage.RemainingHp);
            Assert.False(pulse.Damage.Killed);
        }

        var last = Assert.Single(svc.Tick());
        Assert.Equal(StatusEffectOp.Clear, last.Status.Op);
        Assert.Equal(StatusEffectKind.Poison, last.Status.Kind);
        Assert.Equal(0, last.Status.RemainingTicks);
        Assert.Equal(StatusEffectLimits.PoisonPotency, last.Damage.Damage);
        Assert.Equal("Poison dissipé.", last.Message);
        Assert.False(last.Damage.Killed);
        Assert.Empty(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));
        Assert.Empty(svc.Tick());
        Assert.Equal(hp - StatusEffectLimits.PoisonPotency, svc.FindDummy(session.CurrentMapId)!.Value.Hp);
    }

    [Fact]
    public void PoisonTick_Kill_ClearsEffects_AndRespawnsDummy()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        var melee = CombatFormulas.MeleeDamage(10, 0, CombatMvpLimits.DummyVit);
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY, hp: melee + StatusEffectLimits.PoisonPotency);
        var hit = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        Assert.False(hit.Damage!.Value.Killed);
        Assert.Equal(StatusEffectLimits.PoisonPotency, hit.Damage.Value.RemainingHp);

        var pulse = Assert.Single(svc.Tick());
        Assert.True(pulse.Damage.Killed);
        Assert.Equal(0, pulse.Damage.RemainingHp);
        Assert.Equal(StatusEffectLimits.PoisonPotency, pulse.Damage.Damage);
        Assert.Equal(StatusEffectOp.Clear, pulse.Status.Op);
        Assert.Equal(StatusEffectKind.None, pulse.Status.Kind);
        Assert.Equal("Mannequin vaincu.", pulse.Message);
        Assert.Empty(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));
        Assert.Equal(CombatMvpLimits.DummyMaxHp, svc.FindDummy(session.CurrentMapId)!.Value.Hp);
        Assert.Empty(svc.Tick());
    }

    [Fact]
    public void MeleeKill_ClearsPoisonAndStun_AndDoesNotReapply()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY, hp: 1);
        session.Stats = new CharacterStats(99, 10, 10, 10, 10, 10);
        var killing = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        Assert.True(killing.Damage!.Value.Killed);
        Assert.Null(killing.Status);
        Assert.Empty(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));

        session.Stats = new CharacterStats(10, 10, 10, 10, 10, 10);
        session.LastMeleeUtc = DateTime.UtcNow.AddSeconds(-2);
        var poison = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        Assert.False(poison.Damage!.Value.Killed);
        session.LastMeleeUtc = DateTime.UtcNow.AddSeconds(-2);
        var stun = svc.TryMelee(session, DummyAttack(StatusEffectKind.Stun) with { Style = AttackStyle.Ranged });
        Assert.False(stun.Damage!.Value.Killed);
        Assert.Equal(2, svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId).Count);

        session.Stats = new CharacterStats(99, 10, 10, 10, 10, 10);
        session.LastMeleeUtc = DateTime.UtcNow.AddSeconds(-2);
        var dead = svc.TryMelee(session, DummyAttack(StatusEffectKind.Poison));
        Assert.True(dead.Damage!.Value.Killed);
        Assert.Equal(StatusEffectOp.Clear, dead.Status!.Value.Op);
        Assert.Equal(StatusEffectKind.None, dead.Status.Value.Kind);
        Assert.Empty(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));
        Assert.Equal(CombatMvpLimits.DummyMaxHp, svc.FindDummy(session.CurrentMapId)!.Value.Hp);
        Assert.Empty(svc.Tick());
    }

    [Fact]
    public void Stun_TicksWithoutDamage_AndBlocksBearerAttack()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY);
        var hit = svc.TryMelee(session, DummyAttack(StatusEffectKind.Stun) with { Style = AttackStyle.Ranged });
        Assert.True(hit.Damage!.Value.Ranged);
        var hp = hit.Damage.Value.RemainingHp;
        Assert.Equal(StatusEffectKind.Stun, hit.Status!.Value.Kind);
        Assert.Equal(StatusEffectLimits.StunTicks, hit.Status.Value.RemainingTicks);
        Assert.Equal(0, hit.Status.Value.Potency);

        var tick = Assert.Single(svc.Tick());
        Assert.Equal(StatusEffectOp.Tick, tick.Status.Op);
        Assert.Equal(0, tick.Damage.Damage);
        Assert.Equal(hp, tick.Damage.RemainingHp);
        Assert.Equal(1, tick.Status.RemainingTicks);
        Assert.Equal(hp, svc.FindDummy(session.CurrentMapId)!.Value.Hp);

        var done = Assert.Single(svc.Tick());
        Assert.Equal(StatusEffectOp.Clear, done.Status.Op);
        Assert.Equal(StatusEffectKind.Stun, done.Status.Kind);
        Assert.Equal("Étourdissement dissipé.", done.Message);
        Assert.Empty(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));

        var bearer = SessionOf(Guid.NewGuid());
        svc.EnsureDummy(bearer.CurrentMapId, bearer.PixelX, bearer.PixelY);
        var before = svc.FindDummy(bearer.CurrentMapId)!.Value.Hp;
        svc.Apply(
            bearer.CurrentMapId,
            Guid.NewGuid(),
            bearer.CharacterGuid!.Value,
            StatusEffectKind.Stun,
            bearer.Username,
            CombatTargetKind.Player);
        var blocked = svc.TryMelee(bearer, DummyAttack());
        Assert.False(blocked.Success);
        Assert.Equal("Étourdi.", blocked.Message);
        Assert.False(CombatFx.IsSwingMiss(false, blocked.Message));
        Assert.Equal(before, svc.FindDummy(bearer.CurrentMapId)!.Value.Hp);
        Assert.Equal(default, bearer.LastMeleeUtc);

        Assert.Equal(StatusEffectOp.Tick, Assert.Single(svc.Tick()).Status.Op);
        Assert.Equal(StatusEffectOp.Clear, Assert.Single(svc.Tick()).Status.Op);
        bearer.LastMeleeUtc = DateTime.UtcNow.AddSeconds(-2);
        var freed = svc.TryMelee(bearer, DummyAttack());
        Assert.True(freed.Success);
        Assert.Null(freed.Status);
    }

    [Fact]
    public void PlainMelee_DoesNotApplyStatus()
    {
        var svc = new CombatMvpService();
        var session = SessionOf(Guid.NewGuid());
        svc.EnsureDummy(session.CurrentMapId, session.PixelX, session.PixelY);
        var hit = svc.TryMelee(session, DummyAttack());
        Assert.True(hit.Success);
        Assert.Null(hit.Status);
        Assert.Empty(svc.Snapshot(session.CurrentMapId, CombatMvpLimits.DummyId));
        Assert.Empty(svc.Tick());
    }

    [Fact]
    public void ClientHud_ShowsPoisonFloat_StunIcon_AndFrenchTooltip()
    {
        var hud = new ClientCombatHud();
        var now = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        var target = CombatMvpLimits.DummyId;
        var poison = new StatusEffectEvent(
            Guid.NewGuid(),
            StatusEffectKind.Poison,
            Guid.NewGuid(),
            target,
            4,
            3,
            StatusEffectOp.Apply);
        hud.ApplyStatus(poison, now, SampleDamage(10, 30, killed: false));
        Assert.True(hud.HasStatuses);
        Assert.Equal(string.Empty, hud.LastFloatText);
        Assert.Contains("Poison — 4 tic(s), 3 PV", hud.StatusTooltip, StringComparison.Ordinal);

        var tick = poison with { Op = StatusEffectOp.Tick, RemainingTicks = 3 };
        hud.ApplyStatus(tick, now.AddSeconds(1), SampleDamage(3, 27, killed: false));
        Assert.Equal("-3", hud.LastFloatText);
        Assert.Equal(CombatFxKind.Poison, hud.Floats[^1].Kind);
        Assert.False(hud.FlashPending);
        Assert.Equal(3, Assert.Single(hud.Statuses).RemainingTicks);

        var stun = new StatusEffectEvent(
            Guid.NewGuid(),
            StatusEffectKind.Stun,
            poison.SourceId,
            target,
            2,
            0,
            StatusEffectOp.Apply);
        hud.ApplyStatus(stun, now.AddSeconds(1), SampleDamage(0, 27, killed: false));
        Assert.Equal(2, hud.Statuses.Count);
        Assert.Contains("Étourdi — 2 tic(s)", hud.StatusTooltip, StringComparison.Ordinal);
        Assert.Equal("Étourdi → Mannequin (2 tics)", StatusEffectText.Log(stun, SampleDamage(0, 27, killed: false)));

        hud.ApplyStatus(tick with { Op = StatusEffectOp.Clear, Kind = StatusEffectKind.None, RemainingTicks = 0 }, now.AddSeconds(2), SampleDamage(3, 0, killed: true));
        Assert.Empty(hud.Statuses);
        Assert.Equal(string.Empty, hud.StatusTooltip);
        Assert.Equal("Effets dissipés.", StatusEffectText.Log(
            tick with { Op = StatusEffectOp.Clear, Kind = StatusEffectKind.None },
            SampleDamage(3, 0, killed: true)));
    }

    [Fact]
    public void Shell_WiresStatusIcons_WithoutNewOpcode()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var client = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));
        var effect = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Models", "CombatEffect.cs"));
        var hotbar = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudHotbar.cs"));
        var protocol = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Constants", "FrogWireProtocol.cs"));

        Assert.Contains("StatusEffectReceived += OnStatusEffect", shell, StringComparison.Ordinal);
        Assert.Contains("StatusEffectKind.Poison", shell, StringComparison.Ordinal);
        Assert.Contains("StatusEffectKind.Stun", shell, StringComparison.Ordinal);
        Assert.Contains("_combatHud.Statuses", shell, StringComparison.Ordinal);
        Assert.Contains("applyStatus", client, StringComparison.Ordinal);
        Assert.Contains("StatusEffectReceived", client, StringComparison.Ordinal);
        Assert.Contains("DrawStatusMarks", effect, StringComparison.Ordinal);
        Assert.Contains("Mêlée (1) — poison", hotbar, StringComparison.Ordinal);
        Assert.Contains("étourdissement", hotbar, StringComparison.Ordinal);
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId.Social", effect, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsPoisonRefreshAndHello11()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "combat", "STATUS.md"));
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("17", text, StringComparison.Ordinal);
        Assert.Contains("18", text, StringComparison.Ordinal);
        Assert.Contains("Mannequin", text, StringComparison.Ordinal);
        Assert.Contains("DamageEvent", text, StringComparison.Ordinal);
        Assert.Contains("TODO", text, StringComparison.Ordinal);
        Assert.Contains("in-memory", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Poison", text, StringComparison.Ordinal);
        Assert.Contains("refresh", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
    }

    private static AttackRequest DummyAttack(StatusEffectKind apply = StatusEffectKind.None)
        => new(
            CombatMvpLimits.DummyName,
            CombatTargetKind.Dummy,
            Direction.Down,
            CombatMvpLimits.DummyId,
            AttackStyle.Melee,
            apply);

    private static DamageEvent SampleDamage(int damage, int remaining, bool killed)
        => new(
            Guid.NewGuid(),
            CombatMvpLimits.DummyId,
            CombatTargetKind.Dummy,
            CombatMvpLimits.DummyName,
            damage,
            remaining,
            CombatMvpLimits.DummyMaxHp,
            Hit: true,
            killed);

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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
