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
        var stats = new CharacterStats(10, 10, 10, 10, 10, 10);
        var xp = CombatFormulas.MonsterExperienceReward(1);

        var first = await killRewards.TryGrantKillRewardAsync(
            created.Character!.Id,
            monsterId,
            xp,
            1,
            0,
            stats,
            100,
            50,
            100,
            50);
        Assert.True(first.Success);
        Assert.True(first.NewlyGranted);
        Assert.Equal(xp, first.Experience);

        var second = await killRewards.TryGrantKillRewardAsync(
            created.Character.Id,
            monsterId,
            xp,
            first.Level,
            first.Experience,
            first.Stats!,
            first.MaxHp,
            first.MaxMp,
            first.Hp,
            first.Mp);
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
        var combat = new CombatGameplayService(
            content,
            content,
            content,
            chars,
            Phase7TestHelpers.CreateCharacterService(chars, content),
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            killRewards);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, content)
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "h" };
        session.ApplyFromCharacter(created.Character!);
        session.CurrentMapId = 1;
        session.PixelX = 64;
        session.PixelY = 64;
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
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var killRewards = new InMemoryMonsterKillRewardRepository(chars);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, content)
            .CreateAsync(Guid.NewGuid(), "Hunter", Phase7ContentSeed.DefaultClassId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var stats = new CharacterStats(10, 10, 10, 10, 10, 10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            killRewards.TryGrantKillRewardAsync(
                created.Character!.Id,
                Guid.NewGuid(),
                10,
                1,
                0,
                stats,
                100,
                50,
                100,
                50,
                cts.Token));

        var saved = await chars.FindByIdAsync(created.Character!.Id);
        Assert.Equal(0, saved!.Experience);
    }
}
