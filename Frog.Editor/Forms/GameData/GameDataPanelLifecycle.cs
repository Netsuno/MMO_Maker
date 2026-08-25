namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Coordinateur de cycle de vie async par panneau Données de jeu :
/// sérialisation, contexte UI WinForms, annulation/fermeture et observation des erreurs.
/// </summary>
internal sealed class GameDataPanelLifecycle : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private readonly HashSet<Task> _tracked = new();
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource _cts = new();
    private int _pending;
    private bool _closing;
    private bool _disposed;
    private Exception? _observedException;

    public GameDataPanelLifecycle()
    {
        _uiContext = SynchronizationContext.Current;
    }

    public CancellationToken Token
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _cts.Token;
        }
    }

    public bool IsClosing => _closing;

    public bool IsIdle => Volatile.Read(ref _pending) == 0;

    public int PendingCountForTest => Volatile.Read(ref _pending);

    public Exception? ObservedExceptionForTest => _observedException;

    /// <summary>Exécute une opération async sérialisée sur le contexte UI (si présent).</summary>
    public Task RunAsync(Func<CancellationToken, Task> action, string operationName = "panel")
        => StartTrackedAsync(action, operationName, serialize: true);

    /// <summary>Suit une opération (Save/Publish/Delete) sans la sérialiser avec les filtres.</summary>
    public Task TrackAsync(Func<CancellationToken, Task> action, string operationName = "panel")
        => StartTrackedAsync(action, operationName, serialize: false);

    private Task StartTrackedAsync(Func<CancellationToken, Task> action, string operationName, bool serialize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_closing)
        {
            return Task.CompletedTask;
        }

        Interlocked.Increment(ref _pending);
        var run = ExecuteTrackedAsync(action, operationName, serialize);
        lock (_sync)
        {
            _tracked.Add(run);
        }

        _ = run.ContinueWith(
            t =>
            {
                lock (_sync)
                {
                    _tracked.Remove(t);
                }

                Interlocked.Decrement(ref _pending);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return run;
    }

    private async Task ExecuteTrackedAsync(
        Func<CancellationToken, Task> action,
        string operationName,
        bool serialize)
    {
        try
        {
            if (Services.EditorTestHooks.PanelOperationBarrierForTest is { } barrier)
            {
                await barrier(operationName, _cts.Token).ConfigureAwait(true);
            }

            if (serialize)
            {
                await _gate.WaitAsync(_cts.Token).ConfigureAwait(true);
            }

            try
            {
                if (_closing || _disposed)
                {
                    return;
                }

                await action(_cts.Token).ConfigureAwait(true);
            }
            finally
            {
                if (serialize)
                {
                    _gate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested || _closing)
        {
        }
        catch (Exception ex)
        {
            _observedException = ex;
            Services.EditorTestHooks.OnPanelLifecycleExceptionForTest?.Invoke(ex);
        }
    }

    public void BeginClosing()
    {
        if (_disposed)
        {
            return;
        }

        _closing = true;
        CancelPending();
    }

    public void CancelPending()
    {
        if (_disposed)
        {
            return;
        }

        var previous = _cts;
        _cts = new CancellationTokenSource();
        try
        {
            previous.Cancel();
        }
        finally
        {
            previous.Dispose();
        }
    }

    public async Task DrainAsync(TimeSpan timeout)
    {
        if (_disposed)
        {
            return;
        }

        using var drainCts = new CancellationTokenSource(timeout);
        Task[] snapshot;
        lock (_sync)
        {
            snapshot = _tracked.ToArray();
        }

        if (snapshot.Length > 0)
        {
            try
            {
                await Task.WhenAll(snapshot).WaitAsync(drainCts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _observedException ??= ex;
            }
        }

        try
        {
            await _gate.WaitAsync(drainCts.Token).ConfigureAwait(true);
            _gate.Release();
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _closing = true;
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignore
        }

        _cts.Dispose();
        _gate.Dispose();
    }

}
