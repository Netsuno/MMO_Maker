using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

/// <summary>Sérialise les opérations EF Core sur un contexte partagé (éditeur Données de jeu).</summary>
public sealed class FrogDbContextGate : IDisposable
{
    private readonly FrogDbContext _db;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AsyncLocal<int> _reentrancyDepth = new();
    private bool _disposed;

    public FrogDbContextGate(FrogDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public FrogDbContext Db
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _db;
        }
    }

    public async Task<T> ExecuteAsync<T>(
        Func<FrogDbContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_reentrancyDepth.Value > 0)
        {
            return await action(_db, cancellationToken).ConfigureAwait(false);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _reentrancyDepth.Value++;
        try
        {
            return await action(_db, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _reentrancyDepth.Value--;
            _gate.Release();
        }
    }

    public async Task ExecuteAsync(
        Func<FrogDbContext, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_reentrancyDepth.Value > 0)
        {
            await action(_db, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _reentrancyDepth.Value++;
        try
        {
            await action(_db, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _reentrancyDepth.Value--;
            _gate.Release();
        }
    }

    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_reentrancyDepth.Value > 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _gate.Release();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _db.Dispose();
        _gate.Dispose();
    }
}
