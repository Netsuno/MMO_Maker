using System;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7MonsterKillRewardTests
{
    [Fact]
    public async Task KillReward_SameMonsterInstance_IsIdempotent()
    {
        var chars = new InMemoryCharacterRepository();
        var killRewards = new InMemoryMonsterKillRewardRepository(chars);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, new Phase7PublishedContent())
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        var monsterId = Guid.NewGuid();
        var xp = CombatFormulas.MonsterExperienceReward(1);

        var first = await killRewards.TryGrantKillRewardAsync(
            new MonsterKillRewardRequest(created.Character!.Id, monsterId, xp));
        Assert.True(first.Success);
        Assert.True(first.NewlyGranted);
        Assert.Equal(xp, first.ExperienceGranted);

        var second = await killRewards.TryGrantKillRewardAsync(
            new MonsterKillRewardRequest(created.Character.Id, monsterId, xp));
        Assert.True(second.Success);
        Assert.False(second.NewlyGranted);
        Assert.Equal(first.Experience, second.Experience);
    }

    [Fact]
    public async Task MeleeKill_GrantsExperienceOnce_ThroughLedger()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var killRewards = new InMemoryMonsterKillRewardRepository(chars);
        var combat = CreateCombat(content, chars, killRewards);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, content)
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        var session = NewSession(created.Character!);
        combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 64, 64);

        MeleeCombatResult? kill = null;
        for (var i = 0; i < 12; i++)
        {
            session.LastMeleeUtc = DateTime.MinValue;
            kill = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
            if (kill.MonsterKilled)
            {
                break;
            }
        }

        Assert.NotNull(kill);
        Assert.True(kill.MonsterKilled);
        Assert.Equal(CombatFormulas.MonsterExperienceReward(1), session.Experience);
    }

    [Fact]
    public async Task KillReward_CancellationBeforeCommit_DoesNotGrantExperience()
    {
        var chars = new InMemoryCharacterRepository();
        var killRewards = new InMemoryMonsterKillRewardRepository(chars);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, new Phase7PublishedContent())
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            killRewards.TryGrantKillRewardAsync(
                new MonsterKillRewardRequest(created.Character!.Id, Guid.NewGuid(), 10),
                cts.Token));

        var saved = await chars.FindByIdAsync(created.Character!.Id);
        Assert.Equal(0, saved!.Experience);
    }

    [Fact]
    public async Task MeleeKill_RewardFailure_RestoresMonster_RetryGrantsOnce()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var innerRewards = new InMemoryMonsterKillRewardRepository(chars);
        var failingRewards = new FailingMonsterKillRewardRepository(innerRewards);
        var combatMutations = new CombatMutationRepository();
        var combat = CreateCombat(content, chars, failingRewards, combatMutations);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, content)
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        var session = NewSession(created.Character!);
        var spawned = combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 64, 64);
        Assert.NotNull(spawned);

        MeleeCombatResult? killingBlow = null;
        for (var i = 0; i < 12; i++)
        {
            session.LastMeleeUtc = DateTime.MinValue;
            var result = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
            if (result.MonsterKilled)
            {
                killingBlow = result;
                break;
            }

            if (!result.Success && result.Message == "Recompense non accordee.")
            {
                failingRewards.FailNext = false;
            }
        }

        if (killingBlow is null || !killingBlow.MonsterKilled)
        {
            failingRewards.FailNext = true;
            session.LastMeleeUtc = DateTime.MinValue;
            var failedKill = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
            Assert.False(failedKill.Success);
            Assert.Equal("Recompense non accordee.", failedKill.Message);
            Assert.Contains(combatMutations.ListMonstersOnMap(1), m => m.InstanceId == spawned!.InstanceId && m.Hp > 0);
            Assert.Equal(0, session.Experience);

            failingRewards.FailNext = false;
            session.LastMeleeUtc = DateTime.MinValue;
            killingBlow = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
        }

        Assert.NotNull(killingBlow);
        Assert.True(killingBlow.MonsterKilled);
        Assert.Equal(CombatFormulas.MonsterExperienceReward(1), session.Experience);
        Assert.DoesNotContain(combatMutations.ListMonstersOnMap(1), m => m.InstanceId == spawned!.InstanceId);
    }

    [Fact]
    public async Task MeleeKill_CancelledReward_RestoresMonster_NoPermanentLoss()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var innerRewards = new InMemoryMonsterKillRewardRepository(chars);
        var cancellingRewards = new CancellingMonsterKillRewardRepository(innerRewards);
        var combatMutations = new CombatMutationRepository();
        var combat = CreateCombat(content, chars, cancellingRewards, combatMutations);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, content)
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        var session = NewSession(created.Character!);
        var spawned = combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 64, 64);
        Assert.NotNull(spawned);

        using var cts = new CancellationTokenSource();
        cancellingRewards.CancelOnNext = cts;
        MeleeCombatResult? last = null;
        for (var i = 0; i < 12; i++)
        {
            session.LastMeleeUtc = DateTime.MinValue;
            try
            {
                last = await combat.TryMeleeAttackMonsterAsync(session, "Slime", cts.Token);
                if (last.MonsterKilled)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Contains(combatMutations.ListMonstersOnMap(1), m => m.InstanceId == spawned.InstanceId && m.Hp > 0);
        var saved = await chars.FindByIdAsync(created.Character!.Id);
        Assert.Equal(0, saved!.Experience);
        Assert.NotNull(last);
    }

    private static CombatGameplayService CreateCombat(
        Phase7PublishedContent content,
        InMemoryCharacterRepository chars,
        IMonsterKillRewardRepository killRewards,
        CombatMutationRepository? combatMutations = null)
        => new(
            content,
            content,
            content,
            chars,
            Phase7TestHelpers.CreateCharacterService(chars, content),
            combatMutations ?? new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            killRewards);

    private static Session NewSession(CharacterRecord character)
    {
        var session = new Session { Id = Guid.NewGuid(), Username = "h" };
        session.ApplyFromCharacter(character);
        session.CurrentMapId = 1;
        session.PixelX = 64;
        session.PixelY = 64;
        return session;
    }
}

internal sealed class FailingMonsterKillRewardRepository(IMonsterKillRewardRepository inner) : IMonsterKillRewardRepository
{
    private readonly IMonsterKillRewardRepository _inner = inner;

    public bool FailNext { get; set; }

    public Task<MonsterKillRewardResult> TryGrantKillRewardAsync(
        MonsterKillRewardRequest request,
        CancellationToken cancellationToken = default)
    {
        if (FailNext)
        {
            FailNext = false;
            return Task.FromResult(MonsterKillRewardResult.Fail());
        }

        return _inner.TryGrantKillRewardAsync(request, cancellationToken);
    }
}

internal sealed class CancellingMonsterKillRewardRepository(IMonsterKillRewardRepository inner) : IMonsterKillRewardRepository
{
    private readonly IMonsterKillRewardRepository _inner = inner;

    public CancellationTokenSource? CancelOnNext { get; set; }

    public Task<MonsterKillRewardResult> TryGrantKillRewardAsync(
        MonsterKillRewardRequest request,
        CancellationToken cancellationToken = default)
    {
        CancelOnNext?.Cancel();
        return _inner.TryGrantKillRewardAsync(request, cancellationToken);
    }
}
