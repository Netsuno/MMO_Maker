using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Config;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresMonsterKillCombatTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMonsterKillCombatTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task FinalHit_RewardFailureBeforeCommit_RestoresMonster_RetryGrantsOnce()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var combat = CreateCombat(gate, out var chars, out var rewards);
        var created = await CreateCharacterAsync(gate, seed, "Hunter");
        var session = NewSession(created);
        var spawned = combat.SpawnMonster(1, seed.MonsterId, 64, 64);
        Assert.NotNull(spawned);

        var failOnce = true;
        rewards.TestBeforeCommitAsync = _ =>
        {
            if (!failOnce)
            {
                return Task.CompletedTask;
            }

            failOnce = false;
            throw new InvalidOperationException("injected pre-commit reward failure");
        };

        MeleeCombatResult? failedKill = null;
        for (var i = 0; i < 12; i++)
        {
            session.LastMeleeUtc = DateTime.MinValue;
            var result = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
            if (!result.Success && result.Message == "Recompense non accordee.")
            {
                failedKill = result;
                break;
            }
        }

        Assert.NotNull(failedKill);
        Assert.False(failedKill!.Success);
        Assert.Contains(combat.ListMonstersOnMap(1), m => m.InstanceId == spawned!.InstanceId && m.Hp > 0);
        Assert.Equal(0, (await chars.FindByIdAsync(created.Id))!.Experience);
        Assert.Equal(0, await CountLedgerAsync(gate, created.Id, spawned.InstanceId));

        rewards.TestBeforeCommitAsync = null;
        session.LastMeleeUtc = DateTime.MinValue;
        var retry = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
        while (!retry.MonsterKilled && retry.Success)
        {
            session.LastMeleeUtc = DateTime.MinValue;
            retry = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
        }

        Assert.True(retry.MonsterKilled);
        Assert.Equal(CombatFormulas.MonsterExperienceReward(1), session.Experience);
        Assert.Equal(1, await CountLedgerAsync(gate, created.Id, spawned.InstanceId));
        Assert.DoesNotContain(combat.ListMonstersOnMap(1), m => m.InstanceId == spawned.InstanceId);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task FinalHit_RewardCancellationBeforeCommit_RestoresMonster_NoGrant()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var combat = CreateCombat(gate, out var chars, out var rewards);
        var created = await CreateCharacterAsync(gate, seed, "Hunter");
        var session = NewSession(created);
        var spawned = combat.SpawnMonster(1, seed.MonsterId, 64, 64);
        Assert.NotNull(spawned);

        using var cts = new CancellationTokenSource();
        rewards.TestBeforeCommitAsync = _ =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        for (var i = 0; i < 12; i++)
        {
            session.LastMeleeUtc = DateTime.MinValue;
            try
            {
                await combat.TryMeleeAttackMonsterAsync(session, "Slime", cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Contains(combat.ListMonstersOnMap(1), m => m.InstanceId == spawned!.InstanceId && m.Hp > 0);
        Assert.Equal(0, (await chars.FindByIdAsync(created.Id))!.Experience);
        Assert.Equal(0, await CountLedgerAsync(gate, created.Id, spawned.InstanceId));
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static CombatGameplayService CreateCombat(
        FrogDbContextGate gate,
        out PostgresCharacterRepository chars,
        out PostgresMonsterKillRewardRepository rewards)
    {
        var spells = new PostgresSpellRepository(gate);
        var classes = new PostgresClassRepository(gate, spells);
        chars = new PostgresCharacterRepository(gate);
        var charSvc = new CharacterGameplayService(
            chars,
            classes,
            new PostgresInventoryRepository(gate),
            new PostgresPublishedWorldCatalog(gate),
            Options.Create(new Phase7ContentOptions { RequirePublishedWorld = true }));
        rewards = new PostgresMonsterKillRewardRepository(gate);
        return new CombatGameplayService(
            new PostgresNpcRepository(gate),
            spells,
            new PostgresItemRepository(gate),
            chars,
            charSvc,
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            rewards);
    }

    private static Session NewSession(CharacterRecord character)
    {
        var session = new Session { Id = Guid.NewGuid(), Username = "h" };
        session.ApplyFromCharacter(character);
        session.CurrentMapId = 1;
        session.PixelX = 64;
        session.PixelY = 64;
        return session;
    }

    private static async Task<CharacterRecord> CreateCharacterAsync(
        FrogDbContextGate gate,
        Phase7PostgresContentSeedResult seed,
        string name)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"mk-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var character = await chars.CreateAsync(
            created.AccountId!.Value,
            name,
            seed.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            seed.SpellId,
            1,
            64,
            64);
        Assert.Equal(CharacterCreateStatus.Created, character.Status);
        return character.Character!;
    }

    private static async Task<int> CountLedgerAsync(FrogDbContextGate gate, Guid characterId, Guid monsterInstanceId)
        => await gate.ExecuteAsync(async (db, ct) =>
            await db.PlayerMonsterKillRewards.CountAsync(
                r => r.CharacterId == characterId && r.MonsterInstanceId == monsterInstanceId,
                ct));
}
