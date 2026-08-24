using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

/// <summary>Portée explicite d’un <see cref="FrogDbContext"/> partagé par les éditeurs Données de jeu.</summary>
public sealed class EditorPostgreSqlScope : IDisposable
{
    private bool _disposed;

    public EditorPostgreSqlScope(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        Db = new FrogDbContext(FrogDbContextOptions.Create(connectionString));
    }

    public FrogDbContext Db { get; }

    public bool IsDisposed => _disposed;

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Db.Dispose();
    }
}
