using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

/// <summary>Portée explicite d’un <see cref="FrogDbContext"/> partagé par les éditeurs Données de jeu.</summary>
public sealed class EditorPostgreSqlScope : IDisposable
{
    private static int _activeScopeCount;
    private static int _migrateCallCount;
    private bool _disposed;

    public EditorPostgreSqlScope(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
        Gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        Interlocked.Increment(ref _activeScopeCount);
    }

    public string ConnectionString { get; }

    public FrogDbContextGate Gate { get; }

    public FrogDbContext Db => Gate.Db;

    public bool IsDisposed => _disposed;

    internal static int ActiveScopeCountForTest => Volatile.Read(ref _activeScopeCount);

    internal static int MigrateCallCountForTest => Volatile.Read(ref _migrateCallCount);

    internal static void ResetTestCountersForTest()
    {
        Interlocked.Exchange(ref _activeScopeCount, 0);
        Interlocked.Exchange(ref _migrateCallCount, 0);
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Interlocked.Increment(ref _migrateCallCount);
        if (EditorTestHooks.OverridePostgreSqlMigrateForTest is { } overrideMigrate)
        {
            await overrideMigrate(cancellationToken).ConfigureAwait(false);
            return;
        }

        await Gate.ExecuteAsync(
            static (db, ct) => db.Database.MigrateAsync(ct),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Gate.DrainAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Gate.Dispose();
        Interlocked.Decrement(ref _activeScopeCount);
    }
}
