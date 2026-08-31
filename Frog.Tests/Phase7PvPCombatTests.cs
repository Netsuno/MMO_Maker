using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7PvPCombatTests
{
    [Fact]
    public async Task TwoAttackers_Concurrent_ExactlyOneDeathTransition()
    {
        var content = new Phase7PublishedContent();
        var innerChars = new InMemoryCharacterRepository();
        var chars = new BlockableCharacterRepository(innerChars);
        var combat = new CombatGameplayService(
            content,
            content,
            content,
            chars,
            Phase7TestHelpers.CreateCharacterService(innerChars, content),
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            new InMemoryMonsterKillRewardRepository(innerChars));
        var charSvc = Phase7TestHelpers.CreateCharacterService(innerChars, content);

        var attackerA = (await charSvc.CreateAsync(Guid.NewGuid(), "A", Phase7ContentSeed.DefaultClassId)).Character!;
        var attackerB = (await charSvc.CreateAsync(Guid.NewGuid(), "B", Phase7ContentSeed.DefaultClassId)).Character!;
        var victim = (await charSvc.CreateAsync(Guid.NewGuid(), "Victim", Phase7ContentSeed.DefaultClassId)).Character!;

        var sessionA = new Session { Id = Guid.NewGuid(), Username = "a", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        sessionA.ApplyFromCharacter(attackerA);
        var sessionB = new Session { Id = Guid.NewGuid(), Username = "b", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        sessionB.ApplyFromCharacter(attackerB);
        var defender = new Session { Id = Guid.NewGuid(), Username = "v", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        defender.ApplyFromCharacter(victim);

        var results = await Task.WhenAll(
            Task.Run(() => AttackUntilDeadAsync(combat, sessionA, defender)),
            Task.Run(() => AttackUntilDeadAsync(combat, sessionB, defender)));

        Assert.Contains(results, r => r);
        Assert.True(defender.IsDead);
        Assert.Equal(0, defender.Hp);

        var saved = await innerChars.FindByIdAsync(victim.Id);
        Assert.True(saved!.IsDead);
        Assert.Equal(0, saved.Hp);

        sessionA.LastMeleeUtc = DateTime.MinValue;
        var afterDeath = await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
        Assert.False(afterDeath.Success);
    }

    [Fact]
    public async Task LethalSaveFailure_RestoresSessionFromDatabase_RetryPersistsDeath()
    {
        var content = new Phase7PublishedContent();
        var innerChars = new InMemoryCharacterRepository();
        var chars = new BlockableCharacterRepository(innerChars);
        var combat = new CombatGameplayService(
            content,
            content,
            content,
            chars,
            Phase7TestHelpers.CreateCharacterService(innerChars, content),
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            new InMemoryMonsterKillRewardRepository(innerChars));
        var charSvc = Phase7TestHelpers.CreateCharacterService(innerChars, content);

        var attacker = (await charSvc.CreateAsync(Guid.NewGuid(), "A", Phase7ContentSeed.DefaultClassId)).Character!;
        var victim = (await charSvc.CreateAsync(Guid.NewGuid(), "Victim", Phase7ContentSeed.DefaultClassId)).Character!;
        var sessionA = new Session { Id = Guid.NewGuid(), Username = "a", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        sessionA.ApplyFromCharacter(attacker);
        var defender = new Session { Id = Guid.NewGuid(), Username = "v", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        defender.ApplyFromCharacter(victim);

        var failLethal = true;
        chars.ShouldFailSave = record => failLethal && record.IsDead && record.Hp == 0;

        var sawLethalIOException = false;
        for (var i = 0; i < 20; i++)
        {
            sessionA.LastMeleeUtc = DateTime.MinValue;
            try
            {
                var result = await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
                if (result.TargetKilled)
                {
                    Assert.Fail("Lethal save should have failed before reporting death.");
                }
            }
            catch (IOException)
            {
                sawLethalIOException = true;
                var saved = await innerChars.FindByIdAsync(victim.Id);
                Assert.NotNull(saved);
                Assert.False(saved.IsDead);
                Assert.True(saved.Hp > 0);
                Assert.Equal(saved.Hp, defender.Hp);
                Assert.Equal(saved.IsDead, defender.IsDead);
                failLethal = false;
                break;
            }
        }

        Assert.True(sawLethalIOException);
        chars.ShouldFailSave = null;
        sessionA.LastMeleeUtc = DateTime.MinValue;
        var retry = await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
        Assert.True(retry.Success);
        Assert.True(retry.TargetKilled);
        Assert.True(defender.IsDead);
        Assert.Equal(0, defender.Hp);
        var final = await innerChars.FindByIdAsync(victim.Id);
        Assert.True(final!.IsDead);
        Assert.Equal(0, final.Hp);
    }

    [Fact]
    public async Task LethalSaveCancellation_KeepsSessionAlignedWithDatabase()
    {
        var content = new Phase7PublishedContent();
        var innerChars = new InMemoryCharacterRepository();
        var chars = new BlockableCharacterRepository(innerChars);
        var combat = new CombatGameplayService(
            content,
            content,
            content,
            chars,
            Phase7TestHelpers.CreateCharacterService(innerChars, content),
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            new InMemoryMonsterKillRewardRepository(innerChars));
        var charSvc = Phase7TestHelpers.CreateCharacterService(innerChars, content);
        var attacker = (await charSvc.CreateAsync(Guid.NewGuid(), "A", Phase7ContentSeed.DefaultClassId)).Character!;
        var victim = (await charSvc.CreateAsync(Guid.NewGuid(), "Victim", Phase7ContentSeed.DefaultClassId)).Character!;
        var sessionA = new Session { Id = Guid.NewGuid(), Username = "a", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        sessionA.ApplyFromCharacter(attacker);
        var defender = new Session { Id = Guid.NewGuid(), Username = "v", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        defender.ApplyFromCharacter(victim);

        using var cts = new CancellationTokenSource();
        chars.ShouldFailSave = _ =>
        {
            cts.Cancel();
            return false;
        };

        while (defender.Hp > 8)
        {
            sessionA.LastMeleeUtc = DateTime.MinValue;
            await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
        }

        sessionA.LastMeleeUtc = DateTime.MinValue;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            combat.TryMeleeAttackPlayerAsync(sessionA, defender, cts.Token));

        var saved = await innerChars.FindByIdAsync(victim.Id);
        Assert.NotNull(saved);
        Assert.Equal(saved.Hp, defender.Hp);
        Assert.Equal(saved.IsDead, defender.IsDead);
        Assert.False(saved.IsDead);
    }

    private static async Task<bool> AttackUntilDeadAsync(
        CombatGameplayService combat,
        Session attacker,
        Session defender)
    {
        for (var i = 0; i < 20; i++)
        {
            attacker.LastMeleeUtc = DateTime.MinValue;
            var result = await combat.TryMeleeAttackPlayerAsync(attacker, defender);
            if (result.TargetKilled)
            {
                return true;
            }

            await Task.Delay(5);
        }

        return defender.IsDead;
    }
}
