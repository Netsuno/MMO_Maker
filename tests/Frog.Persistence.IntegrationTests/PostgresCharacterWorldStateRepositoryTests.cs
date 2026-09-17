using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Gameplay;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresCharacterWorldStateRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresCharacterWorldStateRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SetSwitch_RoundTrips()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var characterId = await CreateCharacterAsync(gate);
        var repo = new PostgresCharacterWorldStateRepository(gate);

        await repo.SetSwitchAsync(characterId, "door_open", true);
        var value = await repo.GetSwitchAsync(characterId, "door_open");
        Assert.True(value);

        var all = await repo.GetAllSwitchesAsync(characterId);
        Assert.True(all.TryGetValue("door_open", out var v));
        Assert.True(v);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SetVariable_RoundTrips()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var characterId = await CreateCharacterAsync(gate);
        var repo = new PostgresCharacterWorldStateRepository(gate);

        await repo.SetVariableAsync(characterId, "score", 42);
        Assert.Equal(42, await repo.GetVariableAsync(characterId, "score"));

        await repo.AddVariableAsync(characterId, "score", 8);
        Assert.Equal(50, await repo.GetVariableAsync(characterId, "score"));
    }

    private static async Task<Guid> CreateCharacterAsync(FrogDbContextGate gate)
    {
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"ws-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var character = await chars.CreateAsync(
            created.AccountId!.Value,
            "SwitchHero",
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
}
