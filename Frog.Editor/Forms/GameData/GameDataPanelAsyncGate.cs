namespace Frog.Editor.Forms.GameData;

/// <summary>Sérialise les opérations async déclenchées par l'UI d'un panneau éditeur.</summary>
internal sealed class GameDataPanelAsyncGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public CancellationToken Token
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _cts.Token;
        }
    }

    public async Task RunAsync(Func<CancellationToken, Task> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await action(_cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public void CancelPending()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    public async Task DrainAsync(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var drainCts = new CancellationTokenSource(timeout);
        await _gate.WaitAsync(drainCts.Token).ConfigureAwait(false);
        _gate.Release();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _gate.Dispose();
    }
}
