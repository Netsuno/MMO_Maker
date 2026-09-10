using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresMonsterKillRewardTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMonsterKillRewardTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Grant_Succeeds_AndPersistsProgressionFromAuthoritativeRow()
    {
        using var gate = CreateGate();
        var (characterId, _) = await SeedAsync(gate);
        var monsterId = Guid.NewGuid();
        var xp = CombatFormulas.MonsterExperienceReward(1);
        var rewards = new PostgresMonsterKillRewardRepository(gate);

        var result = await rewards.TryGrantKillRewardAsync(
            new MonsterKillRewardRequest(characterId, monsterId, xp));
        Assert.True(result.Success);
        Assert.True(result.NewlyGranted);
        Assert.Equal(xp, result.ExperienceGranted);

        var saved = await new PostgresCharacterRepository(gate).FindByIdAsync(characterId);
        Assert.Equal(xp, saved!.Experience);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task DuplicateReplay_ReturnsCachedOutcomeWithoutDoubleGrant()
    {
        using var gate = CreateGate();
        var (characterId, _) = await SeedAsync(gate);
        var monsterId = Guid.NewGuid();
        var xp = CombatFormulas.MonsterExperienceReward(1);
        var rewards = new PostgresMonsterKillRewardRepository(gate);

        var first = await rewards.TryGrantKillRewardAsync(
            new MonsterKillRewardRequest(characterId, monsterId, xp));
        var second = await rewards.TryGrantKillRewardAsync(
            new MonsterKillRewardRequest(characterId, monsterId, xp));
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(first.NewlyGranted);
        Assert.False(second.NewlyGranted);
        Assert.Equal(first.Experience, second.Experience);

        var ledgerCount = await CountLedgerRowsAsync(gate, characterId, monsterId);
        Assert.Equal(1, ledgerCount);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentGates_RaceSameLedgerKey_ExactlyOneGrant()
    {
        var (characterId, monsterId, xp) = await SeedIsolatedAsync();
        using var gateA = CreateGate();
        using var gateB = CreateGate();
        var rewardsA = new PostgresMonsterKillRewardRepository(gateA);
        var rewardsB = new PostgresMonsterKillRewardRepository(gateB);
        var request = new MonsterKillRewardRequest(characterId, monsterId, xp);

        var results = await Task.WhenAll(
            rewardsA.TryGrantKillRewardAsync(request),
            rewardsB.TryGrantKillRewardAsync(request));

        Assert.Equal(1, results.Count(r => r.NewlyGranted));
        Assert.True(results.All(r => r.Success));
        Assert.Equal(1, await CountLedgerRowsAsync(gateA, characterId, monsterId));

        var saved = await new PostgresCharacterRepository(gateA).FindByIdAsync(characterId);
        Assert.Equal(xp, saved!.Experience);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task FailureBeforeCommit_RollsBack_NoLedgerOrXp()
    {
        using var gate = CreateGate();
        var (characterId, _) = await SeedAsync(gate);
        var monsterId = Guid.NewGuid();
        var xp = CombatFormulas.MonsterExperienceReward(1);
        var rewards = new PostgresMonsterKillRewardRepository(gate)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected pre-commit failure"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rewards.TryGrantKillRewardAsync(new MonsterKillRewardRequest(characterId, monsterId, xp)));

        Assert.Equal(0, await CountLedgerRowsAsync(gate, characterId, monsterId));
        var saved = await new PostgresCharacterRepository(gate).FindByIdAsync(characterId);
        Assert.Equal(0, saved!.Experience);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CancellationBeforeCommit_RollsBack_NoLedgerOrXp()
    {
        using var gate = CreateGate();
        var (characterId, _) = await SeedAsync(gate);
        var monsterId = Guid.NewGuid();
        var xp = CombatFormulas.MonsterExperienceReward(1);
        using var cts = new CancellationTokenSource();
        var rewards = new PostgresMonsterKillRewardRepository(gate)
        {
            TestBeforeCommitAsync = ct =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            rewards.TryGrantKillRewardAsync(
                new MonsterKillRewardRequest(characterId, monsterId, xp),
                cts.Token));

        Assert.Equal(0, await CountLedgerRowsAsync(gate, characterId, monsterId));
        var saved = await new PostgresCharacterRepository(gate).FindByIdAsync(characterId);
        Assert.Equal(0, saved!.Experience);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task RetryAfterFailure_GrantsExactlyOnce()
    {
        using var gate = CreateGate();
        var (characterId, _) = await SeedAsync(gate);
        var monsterId = Guid.NewGuid();
        var xp = CombatFormulas.MonsterExperienceReward(1);
        var failOnce = true;
        var rewards = new PostgresMonsterKillRewardRepository(gate)
        {
            TestBeforeCommitAsync = _ =>
            {
                if (!failOnce)
                {
                    return Task.CompletedTask;
                }

                failOnce = false;
                throw new InvalidOperationException("injected pre-commit failure");
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rewards.TryGrantKillRewardAsync(new MonsterKillRewardRequest(characterId, monsterId, xp)));

        rewards.TestBeforeCommitAsync = null;
        var retry = await rewards.TryGrantKillRewardAsync(
            new MonsterKillRewardRequest(characterId, monsterId, xp));
        Assert.True(retry.Success);
        Assert.True(retry.NewlyGranted);
        Assert.Equal(1, await CountLedgerRowsAsync(gate, characterId, monsterId));

        var saved = await new PostgresCharacterRepository(gate).FindByIdAsync(characterId);
        Assert.Equal(xp, saved!.Experience);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private async Task<(Guid CharacterId, Phase7PostgresContentSeedResult Seed)> SeedAsync(FrogDbContextGate gate)
    {
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await CreateCharacterAsync(gate, seed);
        return (characterId, seed);
    }

    private async Task<(Guid CharacterId, Guid MonsterInstanceId, long Xp)> SeedIsolatedAsync()
    {
        using var gate = CreateGate();
        var (characterId, _) = await SeedAsync(gate);
        return (characterId, Guid.NewGuid(), CombatFormulas.MonsterExperienceReward(1));
    }

    private static async Task<Guid> CreateCharacterAsync(
        FrogDbContextGate gate,
        Phase7PostgresContentSeedResult seed)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"kr-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var character = await chars.CreateAsync(
            created.AccountId!.Value,
            $"Hero{Guid.NewGuid():N}"[..12],
            seed.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            seed.SpellId,
            1,
            32,
            48);
        Assert.Equal(CharacterCreateStatus.Created, character.Status);
        return character.Character!.Id;
    }

    private static async Task<int> CountLedgerRowsAsync(
        FrogDbContextGate gate,
        Guid characterId,
        Guid monsterInstanceId)
        => await gate.ExecuteAsync(async (db, ct) =>
            await db.PlayerMonsterKillRewards.CountAsync(
                r => r.CharacterId == characterId && r.MonsterInstanceId == monsterInstanceId,
                ct));
}
