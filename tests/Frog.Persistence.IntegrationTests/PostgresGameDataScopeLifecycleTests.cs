using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresGameDataScopeLifecycleTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresGameDataScopeLifecycleTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SharedGate_MigrateOnce_PerScope_AndDisposeClosesContext()
    {
        var migrateCalls = 0;
        var gate = CreateGate();
        try
        {
            await gate.ExecuteAsync(
                async (db, ct) =>
                {
                    migrateCalls++;
                    await db.Database.MigrateAsync(ct).ConfigureAwait(false);
                }).ConfigureAwait(false);

            Assert.Equal(1, migrateCalls);
            Assert.NotNull(gate.Db);

            await gate.ExecuteAsync(
                static (db, ct) => db.Tilesets.CountAsync(ct),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            gate.Dispose();
        }

        Assert.Throws<ObjectDisposedException>(() => _ = gate.Db);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task RepeatedOpenClose_DoesNotThrowOrLeakGate()
    {
        for (var i = 0; i < 3; i++)
        {
            using var gate = CreateGate();
            var repo = new PostgresTilesetRepository(gate);
            var summaries = await repo.ListSummariesAsync().ConfigureAwait(false);
            Assert.NotNull(summaries);
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task DrainAsync_WaitsForPendingOperation_BeforeDispose()
    {
        var gate = CreateGate();
        var operation = gate.ExecuteAsync(
            async (db, ct) =>
            {
                _ = await db.Tilesets.CountAsync(ct).ConfigureAwait(false);
                await Task.Delay(100, ct).ConfigureAwait(false);
            });

        await gate.DrainAsync().ConfigureAwait(false);
        await operation.ConfigureAwait(false);
        gate.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = gate.Db);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task FailedConnection_DoesNotLeaveUndisposedGate()
    {
        FrogDbContextGate? gate = null;
        try
        {
            gate = new FrogDbContextGate(
                new FrogDbContext(FrogDbContextOptions.Create("Host=127.0.0.1;Port=59999;Database=missing;Username=x;Password=x")));
            await Assert.ThrowsAnyAsync<Exception>(
                () => gate.ExecuteAsync(
                    static (db, ct) => db.Database.MigrateAsync(ct),
                    CancellationToken.None)).ConfigureAwait(false);
        }
        finally
        {
            gate?.Dispose();
        }

        if (gate is not null)
        {
            Assert.Throws<ObjectDisposedException>(() => _ = gate.Db);
        }
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
}
