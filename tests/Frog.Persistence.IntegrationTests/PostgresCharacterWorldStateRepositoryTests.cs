using Frog.Application.Events;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
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
        var repo = new PostgresCharacterWorldStateRepository(gate);
        var characterId = Guid.NewGuid();

        await repo.SetSwitchAsync(characterId, "door_open", true);
        var value = await repo.GetSwitchAsync(characterId, "door_open");
        Assert.True(value);

        var all = await repo.GetAllSwitchesAsync(characterId);
        Assert.True(all.TryGetValue("door_open", out var v));
        Assert.True(v);
    }
}
