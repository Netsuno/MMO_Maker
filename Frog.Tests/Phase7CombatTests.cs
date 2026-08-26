using System;
using System.Threading.Tasks;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7CombatTests
{
    [Fact]
    public async Task Melee_KillsMonster_GrantsXp()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var charSvc = new CharacterGameplayService(chars, content, new InMemoryInventoryRepository());
        var combat = new CombatGameplayService(content, content, content, chars, charSvc);
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Fighter", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "f", CurrentMapId = 1 };
        session.ApplyFromCharacter(created.Character!);
        session.PixelX = 64;
        session.PixelY = 64;
        var spawned = combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 64, 64);
        Assert.NotNull(spawned);
        Assert.Single(combat.ListMonstersOnMap(1));

        MeleeCombatResult? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
            if (last.MonsterKilled)
            {
                break;
            }

            session.LastMeleeUtc = DateTime.MinValue;
        }

        Assert.NotNull(last);
        Assert.True(last.Success);
        Assert.True(last.MonsterKilled);
        Assert.True(last.ExperienceGained > 0);
        Assert.True(session.Experience > 0 || session.Level > 1);
    }

    [Fact]
    public async Task Spell_RespectsCooldownAndMana()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var charSvc = new CharacterGameplayService(chars, content, new InMemoryInventoryRepository());
        var combat = new CombatGameplayService(content, content, content, chars, charSvc);
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Mage", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "m", CurrentMapId = 1 };
        session.ApplyFromCharacter(created.Character!);
        session.PixelX = 64;
        session.PixelY = 64;
        combat.SpawnMonster(1, Phase7ContentSeed.DefaultMonsterId, 64, 64);

        var first = await combat.TryCastSpellAsync(session, Phase7ContentSeed.DefaultSpellId, "Slime");
        Assert.True(first.Success);
        var second = await combat.TryCastSpellAsync(session, Phase7ContentSeed.DefaultSpellId, "Slime");
        Assert.False(second.Success);
    }

    [Fact]
    public void MeleeDamage_IsDeterministic()
    {
        var d1 = CombatFormulas.MeleeDamage(10, 10, 8);
        var d2 = CombatFormulas.MeleeDamage(10, 10, 8);
        Assert.Equal(d1, d2);
        Assert.True(d1 >= 1);
    }
}
