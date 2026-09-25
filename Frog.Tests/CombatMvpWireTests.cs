using System;
using System.IO;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class CombatMvpWireTests
{
    [Fact]
    public void Protocol_StaysV11_MeleeOpcodesRemain17And18()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(17, (byte)PacketId.MeleeAttackRequest);
        Assert.Equal(18, (byte)PacketId.MeleeAttackResult);
        Assert.Equal(50, (byte)PacketId.CombatState);
        Assert.Equal(63, (byte)PacketId.DeathNotify);
        Assert.Equal(PacketIds.MeleeAttackRequest, (byte)PacketId.MeleeAttackRequest);
        Assert.Equal(92, (byte)PacketId.InstanceHubSnapshot);
        Assert.Equal(89, (byte)PacketId.EconomyHubSnapshot);
        Assert.True(CombatMvpWire.IsKnownKind((byte)CombatTargetKind.Dummy));
        Assert.True(CombatMvpWire.IsKnownKind((byte)CombatTargetKind.Monster));
        Assert.False(CombatMvpWire.IsKnownKind(99));
        Assert.Equal(CombatMvpLimits.DummyName, "Mannequin");
    }

    [Fact]
    public void AttackRequest_RoundTrip_NameOnlyAndExtras()
    {
        var legacy = new byte[] { 5, (byte)'S', (byte)'l', (byte)'i', (byte)'m', (byte)'e' };
        Assert.True(CombatMvpWire.TryParseAttackRequest(legacy, out var parsedLegacy));
        Assert.Equal("Slime", parsedLegacy.TargetName);
        Assert.Equal(CombatTargetKind.None, parsedLegacy.Kind);

        var req = new AttackRequest("Mannequin", CombatTargetKind.Dummy, Direction.Down, CombatMvpLimits.DummyId);
        var body = CombatMvpWire.BuildAttackRequest(req);
        Assert.True(CombatMvpWire.TryParseAttackRequest(body, out var parsed));
        Assert.Equal(req.TargetName, parsed.TargetName);
        Assert.Equal(req.Kind, parsed.Kind);
        Assert.Equal(req.Facing, parsed.Facing);
        Assert.Equal(req.TargetId, parsed.TargetId);
        Assert.Equal(AttackStyle.Melee, parsed.Style);
        Assert.Equal(1 + 9 + CombatMvpLimits.AttackExtrasBytes, body.Length);

        var ranged = req with { Style = AttackStyle.Ranged };
        var rangedBody = CombatMvpWire.BuildAttackRequest(ranged);
        Assert.Equal(body.Length + 1, rangedBody.Length);
        Assert.True(CombatMvpWire.TryParseAttackRequest(rangedBody, out var parsedRanged));
        Assert.Equal(AttackStyle.Ranged, parsedRanged.Style);
        Assert.Equal(ranged.TargetId, parsedRanged.TargetId);

        var explicitMelee = (byte[])rangedBody.Clone();
        explicitMelee[^1] = (byte)AttackStyle.Melee;
        Assert.True(CombatMvpWire.TryParseAttackRequest(explicitMelee, out var parsedExplicit));
        Assert.Equal(AttackStyle.Melee, parsedExplicit.Style);

        explicitMelee[^1] = 9;
        Assert.False(CombatMvpWire.TryParseAttackRequest(explicitMelee, out _));
    }

    [Fact]
    public void DamageEvent_RoundTrip_AndLegacyResultStillParses()
    {
        var ev = new DamageEvent(
            Guid.NewGuid(),
            CombatMvpLimits.DummyId,
            CombatTargetKind.Dummy,
            CombatMvpLimits.DummyName,
            12,
            28,
            40,
            Hit: true,
            Killed: false);
        var trailer = CombatMvpWire.BuildDamageEventTrailer(ev);
        Assert.True(CombatMvpWire.TryParseDamageEventTrailer(trailer, ev.TargetName, out var parsed));
        Assert.Equal(ev.AttackerId, parsed.AttackerId);
        Assert.Equal(ev.Damage, parsed.Damage);
        Assert.Equal(ev.RemainingHp, parsed.RemainingHp);
        Assert.True(parsed.Hit);
        Assert.False(parsed.Killed);
        Assert.False(parsed.Crit);

        var name = System.Text.Encoding.UTF8.GetBytes("Slime");
        var msg = System.Text.Encoding.UTF8.GetBytes("Touche.");
        var legacy = new byte[1 + 1 + name.Length + 2 + msg.Length];
        legacy[0] = 1;
        legacy[1] = (byte)name.Length;
        name.CopyTo(legacy.AsSpan(2));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(legacy.AsSpan(2 + name.Length), (ushort)msg.Length);
        msg.CopyTo(legacy.AsSpan(2 + name.Length + 2));
        Assert.True(CombatMvpWire.TryParseMeleeResult(legacy, out var hit, out var tgt, out var message, out var dmg));
        Assert.True(hit);
        Assert.Equal("Slime", tgt);
        Assert.Equal("Touche.", message);
        Assert.Null(dmg);

        var withTrailer = new byte[legacy.Length + trailer.Length];
        legacy.CopyTo(withTrailer, 0);
        trailer.CopyTo(withTrailer.AsSpan(legacy.Length));
        Assert.True(CombatMvpWire.TryParseMeleeResult(withTrailer, out _, out _, out _, out var dmg2));
        Assert.True(dmg2.HasValue);
        Assert.Equal(12, dmg2.Value.Damage);
        Assert.False(dmg2.Value.Crit);
    }

    [Fact]
    public void FacesTarget_SameCellOrDominantAxis()
    {
        Assert.True(CombatMvpWire.FacesTarget(Direction.Down, 16, 16, 16, 16));
        Assert.True(CombatMvpWire.FacesTarget(Direction.Down, 16, 16, 16, 48));
        Assert.False(CombatMvpWire.FacesTarget(Direction.Left, 16, 16, 16, 48));
        Assert.True(CombatMvpWire.FacesTarget(Direction.Right, 16, 16, 48, 16));
    }

    [Fact]
    public void ClientCombatHud_AppliesFloatAndFlash()
    {
        var hud = new ClientCombatHud();
        var now = DateTime.UtcNow;
        hud.Apply(
            new DamageEvent(Guid.NewGuid(), CombatMvpLimits.DummyId, CombatTargetKind.Dummy, "Mannequin", 9, 31, 40, true, false),
            now);
        Assert.Equal("-9", hud.LastFloatText);
        Assert.True(hud.FlashPending);
        Assert.Single(hud.Floats);
        hud.ClearFlash();
        Assert.False(hud.FlashPending);
        hud.Tick(now.AddMilliseconds(CombatMvpLimits.FloatingNumberLifetimeMs + 10));
        Assert.Empty(hud.Floats);
        Assert.Equal("-9", ClientCombatHud.FormatFloat(hud.LastEvent!.Value));
    }

    [Fact]
    public void Parse_RejectsUnknownKindAndTruncatedPayload()
    {
        Assert.False(CombatMvpWire.TryParseAttackRequest(Array.Empty<byte>(), out _));
        Assert.False(CombatMvpWire.TryParseDamageEventTrailer(Array.Empty<byte>(), "x", out _));
        Assert.False(CombatMvpWire.TryParseMeleeResult([1], out _, out _, out _, out _));
        var bad = CombatMvpWire.BuildAttackRequest(
            new AttackRequest("Mannequin", CombatTargetKind.Dummy, Direction.Down, Guid.Empty));
        bad[1 + 9] = 99;
        Assert.False(CombatMvpWire.TryParseAttackRequest(bad, out _));
    }

    [Fact]
    public void StatusDoc_RecordsCombatMvp()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "combat", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("17", text, StringComparison.Ordinal);
        Assert.Contains("18", text, StringComparison.Ordinal);
        Assert.Contains("Mannequin", text, StringComparison.Ordinal);
        Assert.Contains("DamageEvent", text, StringComparison.Ordinal);
        Assert.Contains("TODO", text, StringComparison.Ordinal);
        Assert.Contains("in-memory", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_WiresMeleeHotbarAndDamageFloats_WithoutExactShaPanelEdits()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var client = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));
        var effect = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Models", "CombatEffect.cs"));
        var hotbar = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudHotbar.cs"));
        var dialogue = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "DialoguePanel.cs"));
        var quest = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "QuestJournalPanel.cs"));
        var env = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "EnvironmentPanel.cs"));

        Assert.Contains("ClientCombatHud _combatHud", shell, StringComparison.Ordinal);
        Assert.Contains("DamageEventReceived += OnDamageEvent", shell, StringComparison.Ordinal);
        Assert.Contains("CombatMvpLimits.DummyName", shell, StringComparison.Ordinal);
        Assert.Contains("Keys.Space", shell, StringComparison.Ordinal);
        Assert.Contains("CombatEffect.Draw", shell, StringComparison.Ordinal);
        Assert.Contains("case 0:", shell, StringComparison.Ordinal);
        Assert.Contains("MeleeAsync()", shell, StringComparison.Ordinal);
        Assert.Contains("RangedAsync()", shell, StringComparison.Ordinal);
        Assert.Contains("case 3:", shell, StringComparison.Ordinal);
        Assert.Contains("Distance", hotbar, StringComparison.Ordinal);
        Assert.Contains("FillSparks", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("HudMenuCommand.Combat", shell, StringComparison.Ordinal);

        Assert.Contains("SendMeleeAttackAsync", client, StringComparison.Ordinal);
        Assert.Contains("DamageEventReceived", client, StringComparison.Ordinal);
        Assert.Contains("CombatMvpWire.TryParseMeleeResult", client, StringComparison.Ordinal);
        Assert.Contains("PacketId.MeleeAttackRequest", client, StringComparison.Ordinal);

        Assert.Contains("FlashMeleeSlot", hotbar, StringComparison.Ordinal);
        Assert.Contains("Mêlée (1)", hotbar, StringComparison.Ordinal);
        Assert.DoesNotContain("DialoguePanel", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestJournalPanel", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentPanel", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("CombatMvp", dialogue, StringComparison.Ordinal);
        Assert.DoesNotContain("CombatMvp", quest, StringComparison.Ordinal);
        Assert.DoesNotContain("CombatMvp", env, StringComparison.Ordinal);
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
