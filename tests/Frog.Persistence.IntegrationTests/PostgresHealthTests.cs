using Frog.Persistence.PostgreSql;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!IsolatedPostgresFixture.IsConfigured)
        {
            Skip = "FROG_POSTGRES_TEST_CONNECTION_STRING absent (2026-08-22, Task 4).";
        }
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (!IsolatedPostgresFixture.IsConfigured)
        {
            Skip = "FROG_POSTGRES_TEST_CONNECTION_STRING absent (2026-08-22, Task 4).";
        }
    }
}

[Collection("PostgresIsolated")]
public sealed class PostgresHealthTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresHealthTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EmptyDatabase_Migrates_AndHealthPasses()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var health = new PostgresDatabaseHealth(gate.Db);
        var result = await health.CheckAsync();
        Assert.True(result.Ok, result.Detail);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT nspname
            FROM pg_namespace
            WHERE nspname IN ('world', 'content', 'ops')
            ORDER BY 1;
            """,
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        Assert.Equal(new[] { "content", "ops", "world" }, names.ToArray());
    }
}
