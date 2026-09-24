using System;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class CombatFxTests
{
    [Fact]
    public void Map_HitMissCrit_UsesExistingFieldsOnly()
    {
        var hit = Sample(damage: 9, hit: true, killed: false, crit: false);
        var hitCue = CombatFx.Map(hit);
        Assert.Equal(CombatFxKind.Hit, hitCue.Kind);
        Assert.Equal("-9", hitCue.Text);
        Assert.True(hitCue.Flash);
        Assert.Equal(CombatFx.HitEmSize, hitCue.EmSize);

        var miss = Sample(damage: 0, hit: false, killed: false, crit: false);
        var missCue = CombatFx.Map(miss);
        Assert.Equal(CombatFxKind.Miss, missCue.Kind);
        Assert.Equal(CombatFx.MissText, missCue.Text);
        Assert.Equal("Raté", missCue.Text);
        Assert.False(missCue.Flash);
        Assert.True(missCue.EmSize < CombatFx.CritEmSize);

        var crit = Sample(damage: 18, hit: true, killed: false, crit: true);
        var critCue = CombatFx.Map(crit);
        Assert.Equal(CombatFxKind.Crit, critCue.Kind);
        Assert.Equal("-18", critCue.Text);
        Assert.True(critCue.Flash);
        Assert.True(critCue.EmSize > hitCue.EmSize);

        var kill = Sample(damage: 9, hit: true, killed: true, crit: false);
        Assert.Equal(CombatFxKind.Kill, CombatFx.Map(kill).Kind);

        var critKill = Sample(damage: 9, hit: true, killed: true, crit: true);
        Assert.Equal(CombatFxKind.Crit, CombatFx.Map(critKill).Kind);

        Assert.Equal("0", CombatFx.Map(Sample(0, true, false, false)).Text);
    }

    [Fact]
    public void IsSwingMiss_OnlyRangeAndFacing_NotCooldownOrDeath()
    {
        Assert.True(CombatFx.IsSwingMiss(false, "Hors portee."));
        Assert.True(CombatFx.IsSwingMiss(false, "Pas en face de la cible."));
        Assert.True(CombatFx.IsSwingMiss(false, "Monstre hors portee ou introuvable."));
        Assert.True(CombatFx.IsSwingMiss(false, "Hors portée."));
        Assert.True(CombatFx.IsSwingMiss(false, null));
        Assert.True(CombatFx.IsSwingMiss(false, "  "));

        Assert.False(CombatFx.IsSwingMiss(true, "Hors portee."));
        Assert.False(CombatFx.IsSwingMiss(false, "Attaque en recharge."));
        Assert.False(CombatFx.IsSwingMiss(false, "Personnage mort."));
        Assert.False(CombatFx.IsSwingMiss(false, "Aucun personnage actif."));
        Assert.False(CombatFx.IsSwingMiss(false, "Cible invalide."));
        Assert.False(CombatFx.IsSwingMiss(false, "Cible hors ligne."));
        Assert.False(CombatFx.IsSwingMiss(false, "Pas sur la meme carte."));
    }

    [Fact]
    public void Hud_AppliesHitMissCrit_AndDedupesSameCue()
    {
        var hud = new ClientCombatHud();
        var now = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        hud.Apply(Sample(9, true, false, false), now);
        Assert.Equal("-9", hud.LastFloatText);
        Assert.True(hud.FlashPending);
        Assert.False(hud.FlashCrit);
        Assert.Equal(CombatFxKind.Hit, hud.Floats[0].Kind);
        Assert.True(hud.HasFloats);

        hud.Apply(Sample(18, true, false, true), now.AddMilliseconds(50));
        Assert.True(hud.FlashCrit);
        Assert.Equal(CombatFxKind.Crit, hud.Floats[^1].Kind);
        Assert.True(hud.Floats[^1].EmSize > hud.Floats[0].EmSize);

        hud.ClearFlash();
        Assert.False(hud.FlashPending);
        Assert.False(hud.FlashCrit);

        hud.ApplyMiss(now.AddMilliseconds(100));
        Assert.Equal("Raté", hud.LastFloatText);
        Assert.False(hud.FlashPending);
        Assert.Equal(CombatFxKind.Miss, hud.Floats[^1].Kind);

        var before = hud.Floats.Count;
        hud.ApplyMiss(now.AddMilliseconds(110));
        Assert.Equal(before, hud.Floats.Count);

        hud.Tick(now.AddMilliseconds(100 + CombatMvpLimits.FloatingNumberLifetimeMs + 10));
        Assert.Empty(hud.Floats);
        Assert.False(hud.HasFloats);
    }

    [Fact]
    public void CritFlag_RoundTripsOnExistingTrailer_WithoutNewOpcode()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(18, (byte)PacketId.MeleeAttackResult);
        Assert.Equal(4, CombatMvpLimits.DamageFlagCrit);
        Assert.Equal(16 + 16 + 1 + 4 + 4 + 4 + 1, CombatMvpLimits.DamageEventTrailerBytes);

        var ev = Sample(18, hit: true, killed: true, crit: true);
        Assert.Equal(
            (byte)(CombatMvpLimits.DamageFlagHit | CombatMvpLimits.DamageFlagKilled | CombatMvpLimits.DamageFlagCrit),
            ev.Flags);

        var trailer = CombatMvpWire.BuildDamageEventTrailer(ev);
        Assert.Equal(CombatMvpLimits.DamageEventTrailerBytes, trailer.Length);
        Assert.True(CombatMvpWire.TryParseDamageEventTrailer(trailer, ev.TargetName, out var parsed));
        Assert.True(parsed.Crit);
        Assert.True(parsed.Hit);
        Assert.True(parsed.Killed);
        Assert.Equal(CombatFxKind.Crit, CombatFx.Map(parsed).Kind);

        var plain = ev with { Crit = false };
        Assert.True(CombatMvpWire.TryParseDamageEventTrailer(
            CombatMvpWire.BuildDamageEventTrailer(plain),
            plain.TargetName,
            out var parsedPlain));
        Assert.False(parsedPlain.Crit);
        Assert.True(parsedPlain.Killed);
    }

    [Fact]
    public void Place_RisesAndCritSitsHigher()
    {
        var (hitX, hitY) = CombatFx.Place(100, 200, risePixels: 0, CombatFx.HitEmSize, Direction.Down, stackIndex: 0);
        var (_, risen) = CombatFx.Place(100, 200, risePixels: 20, CombatFx.HitEmSize, Direction.Down, stackIndex: 0);
        var (_, critY) = CombatFx.Place(100, 200, risePixels: 0, CombatFx.CritEmSize, Direction.Down, stackIndex: 0);
        var (leftX, _) = CombatFx.Place(100, 200, risePixels: 0, CombatFx.HitEmSize, Direction.Left, stackIndex: 0);
        var (stackedX, _) = CombatFx.Place(100, 200, risePixels: 0, CombatFx.HitEmSize, Direction.Down, stackIndex: 1);

        Assert.True(risen < hitY);
        Assert.True(critY < hitY);
        Assert.True(leftX < hitX);
        Assert.True(stackedX > hitX);
    }

    private static DamageEvent Sample(int damage, bool hit, bool killed, bool crit)
        => new(
            Guid.NewGuid(),
            CombatMvpLimits.DummyId,
            CombatTargetKind.Dummy,
            CombatMvpLimits.DummyName,
            damage,
            40 - damage,
            40,
            hit,
            killed,
            crit);
}
