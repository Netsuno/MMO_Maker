using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Config;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Microsoft.Extensions.Options;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresPvPCombatTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresPvPCombatTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task TwoAttackers_Concurrent_ExactlyOneDeathTransition()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var chars = new PostgresCharacterRepository(gate);
        var combat = CreateCombat(gate, chars);
        var (attackerA, attackerB, victim) = await CreateThreeCharactersAsync(gate, seed);
        var sessionA = NewSession(attackerA, "a");
        var sessionB = NewSession(attackerB, "b");
        var defender = NewSession(victim, "v");

        var results = await Task.WhenAll(
            Task.Run(() => AttackUntilDeadAsync(combat, sessionA, defender)),
            Task.Run(() => AttackUntilDeadAsync(combat, sessionB, defender)));

        Assert.Contains(results, r => r);
        Assert.True(defender.IsDead);
        Assert.Equal(0, defender.Hp);

        var saved = await chars.FindByIdAsync(victim.Id);
        Assert.True(saved!.IsDead);
        Assert.Equal(0, saved.Hp);
        Assert.Equal(saved.Hp, defender.Hp);
        Assert.Equal(saved.IsDead, defender.IsDead);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task LethalSaveFailure_RestoresSessionFromDatabase_RetryPersistsDeath()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var chars = new PostgresCharacterRepository(gate);
        var combat = CreateCombat(gate, chars);
        var attacker = await CreateCharacterAsync(gate, seed, "A");
        var victim = await CreateCharacterAsync(gate, seed, "Victim");
        var sessionA = NewSession(attacker, "a");
        var defender = NewSession(victim, "v");

        var failLethal = true;
        chars.TestBeforeCommitAsync = (record, _) =>
        {
            if (failLethal && record.IsDead && record.Hp == 0)
            {
                failLethal = false;
                throw new IOException("injected lethal save failure");
            }

            return Task.CompletedTask;
        };

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
                var saved = await chars.FindByIdAsync(victim.Id);
                Assert.NotNull(saved);
                Assert.False(saved.IsDead);
                Assert.True(saved.Hp > 0);
                Assert.Equal(saved.Hp, defender.Hp);
                Assert.Equal(saved.IsDead, defender.IsDead);
                break;
            }
        }

        Assert.True(sawLethalIOException);
        chars.TestBeforeCommitAsync = null;
        sessionA.LastMeleeUtc = DateTime.MinValue;
        var retry = await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
        Assert.True(retry.Success);
        Assert.True(retry.TargetKilled);
        Assert.True(defender.IsDead);
        Assert.Equal(0, defender.Hp);

        var final = await chars.FindByIdAsync(victim.Id);
        Assert.True(final!.IsDead);
        Assert.Equal(0, final.Hp);
        Assert.Equal(final.Hp, defender.Hp);
        Assert.Equal(final.IsDead, defender.IsDead);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task LethalSaveCancellation_KeepsSessionAlignedWithDatabase()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var chars = new PostgresCharacterRepository(gate);
        var combat = CreateCombat(gate, chars);
        var attacker = await CreateCharacterAsync(gate, seed, "A");
        var victim = await CreateCharacterAsync(gate, seed, "Victim");
        var sessionA = NewSession(attacker, "a");
        var defender = NewSession(victim, "v");
        using var cts = new CancellationTokenSource();

        chars.TestBeforeCommitAsync = (record, _) =>
        {
            if (record.IsDead && record.Hp == 0)
            {
                cts.Cancel();
            }

            return Task.CompletedTask;
        };

        while (defender.Hp > 8)
        {
            sessionA.LastMeleeUtc = DateTime.MinValue;
            await combat.TryMeleeAttackPlayerAsync(sessionA, defender);
        }

        sessionA.LastMeleeUtc = DateTime.MinValue;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            combat.TryMeleeAttackPlayerAsync(sessionA, defender, cts.Token));

        var saved = await chars.FindByIdAsync(victim.Id);
        Assert.NotNull(saved);
        Assert.Equal(saved.Hp, defender.Hp);
        Assert.Equal(saved.IsDead, defender.IsDead);
        Assert.False(saved.IsDead);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static CombatGameplayService CreateCombat(FrogDbContextGate gate, PostgresCharacterRepository chars)
    {
        var classes = new PostgresClassRepository(gate, new PostgresSpellRepository(gate));
        var charSvc = new CharacterGameplayService(
            chars,
            classes,
            new PostgresInventoryRepository(gate),
            new PostgresPublishedWorldCatalog(gate),
            Options.Create(new Phase7ContentOptions { RequirePublishedWorld = true }));
        return new CombatGameplayService(
            new PostgresNpcRepository(gate),
            new PostgresSpellRepository(gate),
            new PostgresItemRepository(gate),
            chars,
            charSvc,
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            new InMemoryMonsterKillRewardRepository(chars));
    }

    private static Session NewSession(CharacterRecord character, string username)
    {
        var session = new Session { Id = Guid.NewGuid(), Username = username, CurrentMapId = 1, PixelX = 64, PixelY = 64 };
        session.ApplyFromCharacter(character);
        return session;
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

    private static async Task<(CharacterRecord AttackerA, CharacterRecord AttackerB, CharacterRecord Victim)>
        CreateThreeCharactersAsync(FrogDbContextGate gate, Phase7PostgresContentSeedResult seed)
    {
        var a = await CreateCharacterAsync(gate, seed, "A");
        var b = await CreateCharacterAsync(gate, seed, "B");
        var v = await CreateCharacterAsync(gate, seed, "Victim");
        return (a, b, v);
    }

    private static async Task<CharacterRecord> CreateCharacterAsync(
        FrogDbContextGate gate,
        Phase7PostgresContentSeedResult seed,
        string name)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"pvp-{Guid.NewGuid():N}"[..16], "password12345");
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
            32,
            48);
        Assert.Equal(CharacterCreateStatus.Created, character.Status);
        return character.Character!;
    }
}
