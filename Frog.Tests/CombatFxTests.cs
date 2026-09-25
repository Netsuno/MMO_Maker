using System;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
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
    public void ClassicTopDown_YellowNumber_RedCrit_GreyMiss_BriefWhiteFlash()
    {
        Assert.Equal(CombatFx.HitArgb, CombatFx.ArgbFor(CombatFxKind.Hit));
        Assert.Equal(CombatFx.CritArgb, CombatFx.ArgbFor(CombatFxKind.Crit));
        Assert.Equal(CombatFx.KillArgb, CombatFx.ArgbFor(CombatFxKind.Kill));
        Assert.Equal(CombatFx.MissArgb, CombatFx.ArgbFor(CombatFxKind.Miss));
        Assert.Equal(0xFF, (CombatFx.HitArgb >> 16) & 0xFF);
        Assert.Equal(0xFF, (CombatFx.HitArgb >> 8) & 0xFF);
        Assert.Equal(0x00, CombatFx.HitArgb & 0xFF);
        Assert.True(((CombatFx.CritArgb >> 16) & 0xFF) > ((CombatFx.CritArgb >> 8) & 0xFF));
        Assert.Equal("Raté", CombatFx.MissText);

        Assert.True(CombatFx.ShowSpriteFlash(CombatFxKind.Hit, 0));
        Assert.True(CombatFx.ShowSpriteFlash(CombatFxKind.Crit, CombatFx.SpriteFlashMs - 1));
        Assert.True(CombatFx.ShowSpriteFlash(CombatFxKind.Kill, 40));
        Assert.False(CombatFx.ShowSpriteFlash(CombatFxKind.Hit, CombatFx.SpriteFlashMs));
        Assert.False(CombatFx.ShowSpriteFlash(CombatFxKind.Miss, 0));

        var rect = CombatFx.SpriteFlashRect(48.2f, 80.6f);
        Assert.Equal(CombatFx.SpriteSizePx, rect.Size);
        Assert.Equal(32, rect.X);
        Assert.Equal(50, rect.Y);
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

        var ranged = ev with { Ranged = true };
        Assert.Equal((byte)(ev.Flags | CombatMvpLimits.DamageFlagRanged), ranged.Flags);
        Assert.True(CombatMvpWire.TryParseDamageEventTrailer(
            CombatMvpWire.BuildDamageEventTrailer(ranged),
            ranged.TargetName,
            out var parsedRanged));
        Assert.True(parsedRanged.Ranged);
        Assert.True(parsedRanged.Crit);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);

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
    public void Sparks_MeleeCrossThenOpen_RangedBoltThenImpact_YellowNotGold()
    {
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(96, CombatFormulas.RangedAttackRangePixels);
        Assert.Equal(CombatFormulas.BasicAttackRangePixels, CombatFormulas.AttackRangePixels(AttackStyle.Melee));
        Assert.Equal(CombatFormulas.RangedAttackRangePixels, CombatFormulas.AttackRangePixels(AttackStyle.Ranged));
        Assert.True(CombatFormulas.RangedAttackRangePixels > CombatFormulas.BasicAttackRangePixels);
        Assert.Equal(CombatFx.HitArgb, CombatFx.SparkYellowArgb);
        Assert.Equal(unchecked((int)0xFFFFFF00), CombatFx.SparkYellowArgb);
        Assert.Equal(unchecked((int)0xFFFFFFFF), CombatFx.SparkWhiteArgb);

        var meleeAnchor = CombatFx.SparkAnchor(100f, 200f, Direction.Right, AttackStyle.Melee);
        var rangedAnchor = CombatFx.SparkAnchor(100f, 200f, Direction.Right, AttackStyle.Ranged);
        Assert.True(rangedAnchor.X > meleeAnchor.X);

        Span<SparkPixel> pixels = stackalloc SparkPixel[9];
        var tight = CombatFx.FillSparks(100f, 200f, Direction.Right, AttackStyle.Melee, 0, pixels);
        Assert.Equal(5, tight);
        Assert.Equal(2, pixels[0].Size);
        Assert.Equal(CombatFx.SparkWhiteArgb, pixels[0].Argb);
        Assert.Equal(CombatFx.SparkYellowArgb, pixels[1].Argb);
        Assert.Equal(meleeAnchor.X, pixels[0].X);
        Assert.Equal(meleeAnchor.Y, pixels[0].Y);

        var opened = CombatFx.FillSparks(100f, 200f, Direction.Down, AttackStyle.Melee, CombatFx.SparkTravelMs, pixels);
        Assert.Equal(9, opened);
        Assert.Equal(1, pixels[0].Size);

        var bolt = CombatFx.FillSparks(100f, 200f, Direction.Right, AttackStyle.Ranged, 0, pixels);
        Assert.Equal(3, bolt);
        Assert.Equal(rangedAnchor.X, pixels[2].X);
        Assert.Equal(rangedAnchor.Y, pixels[2].Y);
        Assert.Equal(CombatFx.SparkWhiteArgb, pixels[2].Argb);
        Assert.True(pixels[0].X < pixels[2].X);

        var impact = CombatFx.FillSparks(100f, 200f, Direction.Right, AttackStyle.Ranged, CombatFx.SparkTravelMs, pixels);
        Assert.Equal(9, impact);
        Assert.Equal(rangedAnchor.X, pixels[0].X);

        Assert.Equal(0, CombatFx.FillSparks(100f, 200f, Direction.Right, AttackStyle.Melee, -1, pixels));
        Assert.Equal(0, CombatFx.FillSparks(100f, 200f, Direction.Right, AttackStyle.Melee, CombatFx.SparkLifetimeMs, pixels));
    }

    [Fact]
    public void Hud_HitStartsSparks_MissDoesNot_RangedFlagSelectsBolt()
    {
        var hud = new ClientCombatHud();
        var now = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        hud.Apply(Sample(4, true, false, false), now, Direction.Left);
        Assert.True(hud.SparksVisible(now));
        Assert.Equal(AttackStyle.Melee, hud.Sparks!.Value.Style);
        Assert.Equal(Direction.Left, hud.Sparks.Value.Facing);

        hud.Apply(Sample(4, true, false, false) with { Ranged = true }, now.AddMilliseconds(10), Direction.Up);
        Assert.Equal(AttackStyle.Ranged, hud.Sparks!.Value.Style);
        Assert.Equal(Direction.Up, hud.Sparks.Value.Facing);

        hud.ApplyMiss(now.AddMilliseconds(20));
        Assert.Equal(AttackStyle.Ranged, hud.Sparks!.Value.Style);
        Assert.False(hud.SparksVisible(now.AddMilliseconds(20 + CombatFx.SparkLifetimeMs)));
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
