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
    public async Task EditorScope_MigrateOnce_PerInitialization_AndDisposeClosesContext()
    {
        EditorPostgreSqlScope.ResetTestCountersForTest();
        var before = EditorPostgreSqlScope.MigrateCallCountForTest;
        var scope = new EditorPostgreSqlScope(_fixture.ConnectionString);
        try
        {
            Assert.Equal(1, EditorPostgreSqlScope.ActiveScopeCountForTest);
            await scope.MigrateAsync().ConfigureAwait(false);
            Assert.Equal(before + 1, EditorPostgreSqlScope.MigrateCallCountForTest);

            await scope.Gate.ExecuteAsync(
                static (db, ct) => db.Tilesets.CountAsync(ct),
                CancellationToken.None).ConfigureAwait(false);
            Assert.False(scope.IsDisposed);
        }
        finally
        {
            scope.Dispose();
        }

        Assert.True(scope.IsDisposed);
        Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
        Assert.Throws<ObjectDisposedException>(() => _ = scope.Db);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EditorScope_Dispose_IsIdempotent_ExactlyOnceActiveCount()
    {
        EditorPostgreSqlScope.ResetTestCountersForTest();
        var scope = new EditorPostgreSqlScope(_fixture.ConnectionString);
        Assert.Equal(1, EditorPostgreSqlScope.ActiveScopeCountForTest);
        await scope.MigrateAsync().ConfigureAwait(false);
        scope.Dispose();
        scope.Dispose();
        Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
        Assert.True(scope.IsDisposed);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EditorScope_RepeatedOpenClose_ReturnsActiveCountToZero()
    {
        EditorPostgreSqlScope.ResetTestCountersForTest();
        for (var i = 0; i < 3; i++)
        {
            var scope = new EditorPostgreSqlScope(_fixture.ConnectionString);
            await scope.MigrateAsync().ConfigureAwait(false);
            var repo = new PostgresTilesetRepository(scope.Gate);
            Assert.NotNull(await repo.ListSummariesAsync().ConfigureAwait(false));
            await scope.DrainAsync().ConfigureAwait(false);
            scope.Dispose();
            Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EditorScope_DrainAsync_WaitsForPendingOperation_BeforeDispose()
    {
        EditorPostgreSqlScope.ResetTestCountersForTest();
        var scope = new EditorPostgreSqlScope(_fixture.ConnectionString);
        await scope.MigrateAsync().ConfigureAwait(false);
        var operation = scope.Gate.ExecuteAsync(
            async (db, ct) =>
            {
                _ = await db.Tilesets.CountAsync(ct).ConfigureAwait(false);
                await Task.Delay(100, ct).ConfigureAwait(false);
            });

        await scope.DrainAsync().ConfigureAwait(false);
        await operation.ConfigureAwait(false);
        scope.Dispose();
        Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
        Assert.Throws<ObjectDisposedException>(() => _ = scope.Db);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EditorScope_CancelledMigrate_ThenDispose_ActiveCountZero()
    {
        EditorPostgreSqlScope.ResetTestCountersForTest();
        var scope = new EditorPostgreSqlScope(_fixture.ConnectionString);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => scope.MigrateAsync(cts.Token)).ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
        }

        Assert.True(scope.IsDisposed);
        Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
    }
}
