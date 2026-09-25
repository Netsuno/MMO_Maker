using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Server.Combat;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MonsterAiTests
{
    private static readonly DateTime T0 = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Protocol_Stays11_AndAggroUsesContentTiles()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(5 * TileAssetMetrics.TargetTileSizePixels, MonsterAiLimits.AggroRadiusPixels);
        Assert.Equal(8 * TileAssetMetrics.TargetTileSizePixels, MonsterAiLimits.LeashRadiusPixels);
        Assert.Equal(TileAssetMetrics.TargetTileSizePixels, MonsterAiLimits.ChaseStepPixels);
        Assert.Equal(800, CombatFormulas.BasicAttackCooldownMs);
        Assert.Equal(56, CombatFormulas.BasicAttackRangePixels);
        Assert.Equal(96, CombatFormulas.RangedAttackRangePixels);
    }

    [Fact]
    public void PositionUpdate_LegacyBody_ParsesAsPlayer_MonsterTrailer_IsAdditive()
    {
        var legacy = ManualPositionBody("hero", 3, 10, 20);
        Assert.True(Phase7PacketCodec.TryParsePositionUpdate(legacy, out var player));
        Assert.Equal("hero", player.Username);
        Assert.Equal(3, player.MapId);
        Assert.Equal(10, player.PixelX);
        Assert.Equal(20, player.PixelY);
        Assert.Equal(CombatTargetKind.Player, player.Kind);

        var built = Phase7PacketCodec.BuildPositionUpdateBody("hero", 3, 10, 20);
        Assert.Equal(legacy.Length, built.Length);

        var monster = Phase7PacketCodec.BuildPositionUpdateBody("Slime", 1, 48, 96, CombatTargetKind.Monster);
        Assert.Equal(legacy.Length - "hero".Length + "Slime".Length + 1, monster.Length);
        Assert.Equal((byte)CombatTargetKind.Monster, monster[^1]);
        Assert.True(Phase7PacketCodec.TryParsePositionUpdate(monster, out var slime));
        Assert.Equal(CombatTargetKind.Monster, slime.Kind);
        Assert.Equal(48, slime.PixelX);

        var withoutTrailer = monster[..^1];
        Assert.True(Phase7PacketCodec.TryParsePositionUpdate(withoutTrailer, out var chopped));
        Assert.Equal(CombatTargetKind.Player, chopped.Kind);
        Assert.Equal("Slime", chopped.Username);

        var dummy = Phase7PacketCodec.BuildPositionUpdateBody(
            CombatMvpLimits.DummyName,
            1,
            8,
            8,
            CombatTargetKind.Dummy);
        Assert.True(Phase7PacketCodec.TryParsePositionUpdate(dummy, out var mannequin));
        Assert.Equal(CombatTargetKind.Dummy, mannequin.Kind);

        var tooLong = new byte[monster.Length + 1];
        monster.CopyTo(tooLong, 0);
        Assert.False(Phase7PacketCodec.TryParsePositionUpdate(tooLong, out _));
    }

    [Fact]
    public void Client_RoutesMonsterKindOntoWorldSprite()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var client = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));
        Assert.Contains("MonsterAi.TracksAsMonsterSprite", shell, StringComparison.Ordinal);
        Assert.Contains("NoteMonsterPosition", shell, StringComparison.Ordinal);
        Assert.Contains("_worldMonsters.Clear()", shell, StringComparison.Ordinal);
        Assert.Contains("Phase7PacketCodec.TryParsePositionUpdate", client, StringComparison.Ordinal);
        Assert.True(MonsterAi.TracksAsMonsterSprite(CombatTargetKind.Monster));
        Assert.True(MonsterAi.TracksAsMonsterSprite(CombatTargetKind.Dummy));
        Assert.False(MonsterAi.TracksAsMonsterSprite(CombatTargetKind.Player));
    }

    [Fact]
    public void Brain_AcquiresNearest_KeepsPrimaryTarget()
    {
        var memory = new MonsterAiMemory();
        var primary = Guid.NewGuid();
        var other = Guid.NewGuid();
        var actors = new List<MonsterAiActor>
        {
            new(primary, 1, 540, 500, true),
            new(other, 1, 300, 500, true),
        };
        var first = MonsterAi.Decide(1, 500, 500, 20, memory, actors, T0, 4000, 4000, static (_, _) => false);
        Assert.Equal(MonsterAiOrder.Attack, first.Order);
        Assert.Equal(primary, first.TargetId);
        Assert.Equal(500, first.X);

        actors[0] = new MonsterAiActor(primary, 1, 800, 500, true);
        actors[1] = new MonsterAiActor(other, 1, 500, 470, true);
        var second = MonsterAi.Decide(1, 500, 500, 20, memory, actors, T0, 4000, 4000, static (_, _) => false);
        Assert.Equal(primary, second.TargetId);
        Assert.Equal(MonsterAiOrder.Chase, second.Order);
        Assert.True(second.X > 500);
    }

    [Fact]
    public void Brain_ChaseStopsAtBlock_AndClampsToMap()
    {
        var memory = new MonsterAiMemory();
        var player = Guid.NewGuid();
        var actors = new[] { new MonsterAiActor(player, 1, 200, 0, true) };
        var blocked = MonsterAi.Decide(
            1,
            10,
            0,
            20,
            memory,
            actors,
            T0,
            4000,
            4000,
            static (x, _) => x >= 40);
        Assert.Equal(player, blocked.TargetId);
        Assert.Equal(MonsterAiOrder.Hold, blocked.Order);
        Assert.Equal(10, blocked.X);

        var open = new MonsterAiMemory();
        var clamped = MonsterAi.Decide(
            1,
            50,
            0,
            20,
            open,
            actors,
            T0,
            80,
            4000,
            static (_, _) => false);
        Assert.Equal(MonsterAiOrder.Chase, clamped.Order);
        Assert.Equal(79, clamped.X);
        Assert.Equal(player, clamped.TargetId);
    }

    [Fact]
    public void Brain_DropsOnMapChange_Leash_Timeout_AndDeath()
    {
        var id = Guid.NewGuid();
        var memory = new MonsterAiMemory();
        var near = new[] { new MonsterAiActor(id, 1, 40, 0, true) };
        var acquired = MonsterAi.Decide(1, 0, 0, 20, memory, near, T0, 4000, 4000, static (_, _) => false);
        Assert.Equal(id, acquired.TargetId);

        var otherMap = new[] { new MonsterAiActor(id, 2, 40, 0, true) };
        var leftMap = MonsterAi.Decide(1, 0, 0, 20, memory, otherMap, T0, 4000, 4000, static (_, _) => false);
        Assert.Null(leftMap.TargetId);
        Assert.Null(memory.TargetId);

        memory = new MonsterAiMemory();
        MonsterAi.Decide(1, 0, 0, 20, memory, near, T0, 4000, 4000, static (_, _) => false);
        var far = new[] { new MonsterAiActor(id, 1, 500, 0, true) };
        var leashed = MonsterAi.Decide(1, 0, 0, 20, memory, far, T0, 4000, 4000, static (_, _) => false);
        Assert.Null(leashed.TargetId);

        memory = new MonsterAiMemory();
        MonsterAi.Decide(1, 0, 0, 20, memory, near, T0, 4000, 4000, static (_, _) => false);
        var outside = new[] { new MonsterAiActor(id, 1, 300, 0, true) };
        var still = MonsterAi.Decide(1, 0, 0, 20, memory, outside, T0, 4000, 4000, static (_, _) => false);
        Assert.Equal(id, still.TargetId);
        var timedOut = MonsterAi.Decide(
            1,
            0,
            0,
            20,
            memory,
            outside,
            T0.AddMilliseconds(MonsterAiLimits.AggroTimeoutMs),
            4000,
            4000,
            static (_, _) => false);
        Assert.Null(timedOut.TargetId);

        memory = new MonsterAiMemory { TargetId = id, HomeSet = true };
        var deadMonster = MonsterAi.Decide(1, 0, 0, 0, memory, near, T0, 4000, 4000, static (_, _) => false);
        Assert.Equal(MonsterAiOrder.Hold, deadMonster.Order);
        Assert.Null(memory.TargetId);

        memory = new MonsterAiMemory();
        MonsterAi.Decide(1, 0, 0, 20, memory, near, T0, 4000, 4000, static (_, _) => false);
        var deadPlayer = new[] { new MonsterAiActor(id, 1, 40, 0, false) };
        var dropped = MonsterAi.Decide(1, 0, 0, 20, memory, deadPlayer, T0, 4000, 4000, static (_, _) => false);
        Assert.Null(dropped.TargetId);
    }

    [Fact]
    public void Brain_WandersThenHoldsUntilInterval()
    {
        var memory = new MonsterAiMemory();
        var first = MonsterAi.Decide(1, 100, 100, 20, memory, Array.Empty<MonsterAiActor>(), T0, 4000, 4000, static (_, _) => false);
        Assert.Equal(MonsterAiOrder.Wander, first.Order);
        Assert.Equal(100 + MonsterAiLimits.WanderStepPixels, first.X);
        Assert.Equal(100, first.Y);

        var second = MonsterAi.Decide(1, first.X, first.Y, 20, memory, Array.Empty<MonsterAiActor>(), T0, 4000, 4000, static (_, _) => false);
        Assert.Equal(MonsterAiOrder.Hold, second.Order);
        Assert.Equal(first.X, second.X);
    }

    [Fact]
    public async Task Service_AcquireChase_Attack_RateLimit_Deaggro_DeathClears()
    {
        var (combat, session, chars) = await CreateWorldAsync();
        var sessions = new List<Session> { session };
        var ai = new MonsterAiService(combat, () => sessions, MonsterAiWorld.Open(4000, 4000));
        var spawned = combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 0, 0);
        Assert.NotNull(spawned);

        session.PixelX = 200;
        session.PixelY = 0;
        var chased = await ai.TickAsync(T0);
        Assert.Single(chased);
        Assert.Equal(MonsterAiLimits.ChaseStepPixels, chased[0].PixelX);
        Assert.Equal(session.Id, ai.TargetOf(1, spawned!.InstanceId));
        Assert.Null(chased[0].Strike);
        var moved = Assert.Single(combat.ListMonstersOnMap(1));
        Assert.Equal(MonsterAiLimits.ChaseStepPixels, moved.PixelX);

        session.PixelX = 0;
        session.PixelY = 0;
        await combat.TryMoveMonsterAsync(1, spawned.InstanceId, 0, 0);
        var hp0 = session.Hp;
        var vit = (await chars.FindByIdAsync(session.CharacterGuid!.Value))!.Stats.Vit;
        var expected = CombatFormulas.MeleeDamage(spawned.Level * 2, 0, vit);
        var hit = await ai.TickAsync(T0.AddSeconds(2));
        var strike = hit[0].Strike;
        Assert.NotNull(strike);
        Assert.True(strike!.Success);
        Assert.Equal(AttackStyle.Melee, hit[0].Style);
        Assert.Equal(expected, strike.Damage);
        Assert.Equal(hp0 - expected, session.Hp);

        var hp1 = session.Hp;
        var cooled = await ai.TickAsync(T0.AddSeconds(2).AddMilliseconds(100));
        Assert.Equal(hp1, session.Hp);
        Assert.Null(cooled[0].Strike);

        var again = await ai.TickAsync(T0.AddSeconds(2).AddMilliseconds(CombatFormulas.BasicAttackCooldownMs));
        Assert.NotNull(again[0].Strike);
        Assert.True(again[0].Strike!.Success);
        Assert.Equal(hp1 - expected, session.Hp);

        session.PixelX = 80;
        session.PixelY = 0;
        await combat.TryMoveMonsterAsync(1, spawned.InstanceId, 0, 0);
        var ranged = await ai.TickAsync(T0.AddSeconds(4));
        Assert.Equal(AttackStyle.Ranged, ranged[0].Style);
        Assert.Equal(0, ranged[0].PixelX);
        Assert.NotNull(ranged[0].Strike);
        Assert.True(ranged[0].Strike!.Success);

        session.CurrentMapId = 9;
        var left = await ai.TickAsync(T0.AddSeconds(6));
        Assert.Null(ai.TargetOf(1, spawned.InstanceId));
        Assert.Null(left[0].Strike);

        session.CurrentMapId = 1;
        session.PixelX = 0;
        session.PixelY = 0;
        await combat.TryMoveMonsterAsync(1, spawned.InstanceId, 0, 0);
        await ai.TickAsync(T0.AddSeconds(8));
        Assert.Equal(session.Id, ai.TargetOf(1, spawned.InstanceId));

        session.Stats = new CharacterStats(99, 10, 10, 10, 10, 10);
        session.LastMeleeUtc = DateTime.MinValue;
        var killed = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
        Assert.True(killed.MonsterKilled);
        await ai.TickAsync(T0.AddSeconds(10));
        Assert.Null(ai.TargetOf(1, spawned.InstanceId));
        Assert.Empty(combat.ListMonstersOnMap(1));
    }

    [Fact]
    public async Task Service_PlayerDeath_ClearsTarget()
    {
        var (combat, session, chars) = await CreateWorldAsync();
        var sessions = new List<Session> { session };
        var ai = new MonsterAiService(combat, () => sessions, MonsterAiWorld.Open(4000, 4000));
        var spawned = combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 0, 0);
        session.PixelX = 0;
        session.PixelY = 0;
        var record = (await chars.FindByIdAsync(session.CharacterGuid!.Value))!;
        await chars.SaveAsync(record with { Hp = 1, IsDead = false });
        session.Hp = 1;
        session.IsDead = false;

        var hit = await ai.TickAsync(T0);
        Assert.True(hit[0].Strike!.TargetKilled);
        Assert.True(session.IsDead);
        Assert.Equal(0, session.Hp);
        Assert.Null(ai.TargetOf(1, spawned!.InstanceId));
    }

    [Fact]
    public async Task Service_DummyUsesSameStrikePath_StunSkipsAttack()
    {
        var (combat, session, chars) = await CreateWorldAsync();
        var sessions = new List<Session> { session };
        var dummies = new CombatMvpService();
        var ai = new MonsterAiService(combat, () => sessions, MonsterAiWorld.Open(4000, 4000), dummies);
        session.PixelX = 0;
        session.PixelY = 0;
        dummies.EnsureDummy(1, 200, 0);
        var chased = await ai.TickAsync(T0);
        Assert.Equal(CombatTargetKind.Dummy, chased[0].Kind);
        Assert.Equal(200 - MonsterAiLimits.ChaseStepPixels, chased[0].PixelX);
        Assert.Equal(200 - MonsterAiLimits.ChaseStepPixels, dummies.FindDummy(1)!.Value.PixelX);

        dummies.EnsureDummy(1, 0, 0);
        var vit = (await chars.FindByIdAsync(session.CharacterGuid!.Value))!.Stats.Vit;
        var expected = CombatFormulas.MeleeDamage(CombatMvpLimits.DummyLevel * 2, 0, vit);
        var hp0 = session.Hp;
        var hit = await ai.TickAsync(T0.AddSeconds(2));
        Assert.Equal(AttackStyle.Melee, hit[0].Style);
        Assert.Equal(expected, hit[0].Strike!.Damage);
        Assert.Equal(hp0 - expected, session.Hp);

        dummies.Apply(
            1,
            Guid.NewGuid(),
            CombatMvpLimits.DummyId,
            StatusEffectKind.Stun,
            CombatMvpLimits.DummyName,
            CombatTargetKind.Dummy);
        var hp1 = session.Hp;
        var stunned = await ai.TickAsync(T0.AddSeconds(4));
        Assert.Null(stunned[0].Strike);
        Assert.Equal(hp1, session.Hp);
    }

    private static async Task<(CombatGameplayService Combat, Session Session, InMemoryCharacterRepository Characters)> CreateWorldAsync()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content);
        var combat = Phase7TestHelpers.CreateCombatService(chars, content);
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Fighter", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "f", CurrentMapId = 1 };
        session.ApplyFromCharacter(created.Character!);
        session.CurrentMapId = 1;
        session.PixelX = 0;
        session.PixelY = 0;
        return (combat, session, chars);
    }

    private static byte[] ManualPositionBody(string username, int mapId, int x, int y)
    {
        var name = Encoding.UTF8.GetBytes(username);
        var body = new byte[1 + name.Length + 12];
        body[0] = (byte)name.Length;
        name.CopyTo(body, 1);
        var o = 1 + name.Length;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(o), mapId);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(o + 4), x);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(o + 8), y);
        return body;
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

        throw new InvalidOperationException("Dépôt introuvable.");
    }
}
