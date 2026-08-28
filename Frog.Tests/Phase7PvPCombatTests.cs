using System;
using System.Threading.Tasks;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7PvPCombatTests
{
    [Fact]
    public async Task TwoAttackers_ExactlyOneDeathTransition_AuthoritativeState()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var combat = Phase7TestHelpers.CreateCombatService(chars, content);
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content);

        var attackerA = (await charSvc.CreateAsync(Guid.NewGuid(), "A", Phase7ContentSeed.DefaultClassId)).Character!;
        var attackerB = (await charSvc.CreateAsync(Guid.NewGuid(), "B", Phase7ContentSeed.DefaultClassId)).Character!;
        var victim = (await charSvc.CreateAsync(Guid.NewGuid(), "Victim", Phase7ContentSeed.DefaultClassId)).Character!;

        var sessionA = new Session { Id = Guid.NewGuid(), Username = "a", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        sessionA.ApplyFromCharacter(attackerA);
        var sessionB = new Session { Id = Guid.NewGuid(), Username = "b", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        sessionB.ApplyFromCharacter(attackerB);
        var defender = new Session { Id = Guid.NewGuid(), Username = "v", CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        defender.ApplyFromCharacter(victim);

        var deaths = 0;
        for (var i = 0; i < 40; i++)
        {
            sessionA.LastMeleeUtc = DateTime.MinValue;
            sessionB.LastMeleeUtc = DateTime.MinValue;
            var r1 = await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
            var r2 = await combat.TryMeleeAttackPlayerAsync(sessionB, defender);
            if (r1.TargetKilled || r2.TargetKilled)
            {
                deaths++;
            }

            if (defender.IsDead)
            {
                break;
            }
        }

        Assert.Equal(1, deaths);
        Assert.True(defender.IsDead);
        Assert.Equal(0, defender.Hp);

        sessionA.LastMeleeUtc = DateTime.MinValue;
        var afterDeath = await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
        Assert.False(afterDeath.Success);

        var saved = await chars.FindByIdAsync(victim.Id);
        Assert.True(saved!.IsDead);
        Assert.Equal(0, saved.Hp);
    }
}
