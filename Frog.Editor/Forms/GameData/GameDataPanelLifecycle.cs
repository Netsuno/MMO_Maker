namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Coordinateur de cycle de vie async par panneau Données de jeu :
/// sérialisation, contexte UI, annulation stable et drain strict.
/// </summary>
internal sealed class GameDataPanelLifecycle : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private readonly HashSet<Task> _tracked = new();
    private readonly SynchronizationContext? _uiContext;
    private readonly int _uiThreadId;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private int _pending;
    private bool _closing;
    private bool _disposed;
    private Exception? _observedException;

    public GameDataPanelLifecycle()
    {
        _uiContext = SynchronizationContext.Current;
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    public CancellationToken Token
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _lifetimeCts.Token;
        }
    }

    public SynchronizationContext? UiContext => _uiContext;

    public int UiThreadIdForTest => _uiThreadId;

    public bool IsClosing => _closing;

    public bool IsIdle => Volatile.Read(ref _pending) == 0;

    public int PendingCountForTest => Volatile.Read(ref _pending);

    public Exception? ObservedExceptionForTest => _observedException;

    public Task RunAsync(Func<CancellationToken, Task> action, string operationName = "panel")
        => StartTrackedAsync(action, operationName, serialize: true);

    public Task TrackAsync(Func<CancellationToken, Task> action, string operationName = "panel")
        => StartTrackedAsync(action, operationName, serialize: false);

    private Task StartTrackedAsync(Func<CancellationToken, Task> action, string operationName, bool serialize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_closing || _lifetimeCts.IsCancellationRequested)
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
        // Per-operation linked token — never replaced; lifetime cancel is stable.
        using var opCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        var opToken = opCts.Token;
        try
        {
            if (Services.EditorTestHooks.PanelOperationBarrierForTest is { } barrier)
            {
                // Never block the UI thread inside a barrier — close/dispose must remain able to
                // cancel pending work while the STA pump continues.
                await barrier(operationName, opToken).ConfigureAwait(false);
            }

            if (serialize)
            {
                await _gate.WaitAsync(opToken).ConfigureAwait(false);
            }

            try
            {
                if (_closing || _disposed || opToken.IsCancellationRequested)
                {
                    return;
                }

                await InvokeOnUiAsync(() => action(opToken), opToken).ConfigureAwait(false);
            }
            finally
            {
                if (serialize)
                {
                    _gate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (opToken.IsCancellationRequested || _closing)
        {
        }
        catch (Exception ex)
        {
            _observedException = ex;
            Services.EditorTestHooks.OnPanelLifecycleExceptionForTest?.Invoke(ex);
        }
    }

    private Task InvokeOnUiAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        // Hybrid WPF + WinForms STA: the lifecycle may capture DispatcherSynchronizationContext
        // at construction, while button clicks later see WindowsFormsSynchronizationContext.
        // Always run inline on the owning STA thread so UI work stays on that thread and
        // in-memory ops can complete synchronously through click handlers.
        if (Environment.CurrentManagedThreadId == _uiThreadId
            || _uiContext is null
            || ReferenceEquals(SynchronizationContext.Current, _uiContext))
        {
            return action();
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = SynchronizationContext.Current ?? _uiContext;
        context.Post(
            _ =>
            {
                _ = RunPostedAsync();
            },
            null);

        async Task RunPostedAsync()
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action().ConfigureAwait(true);
                tcs.TrySetResult();
            }
            catch (OperationCanceledException oce)
            {
                tcs.TrySetCanceled(oce.CancellationToken.IsCancellationRequested
                    ? oce.CancellationToken
                    : cancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        return AwaitWithCancellationAsync();

        async Task AwaitWithCancellationAsync()
        {
            await using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
    }

    public void BeginClosing()
    {
        if (_disposed)
        {
            return;
        }

        _closing = true;
        try
        {
            _lifetimeCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Attend la fin de toutes les opérations suivies.
    /// Retourne false si le délai expire alors que du travail est encore actif.
    /// </summary>
    public async Task<bool> DrainAsync(TimeSpan timeout)
    {
        if (_disposed)
        {
            return true;
        }

        Task[] snapshot;
        lock (_sync)
        {
            snapshot = _tracked.ToArray();
        }

        if (snapshot.Length == 0 && IsIdle)
        {
            return true;
        }

        try
        {
            await Task.WhenAll(snapshot).WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            // cancelled ops still complete their tracked tasks
        }
        catch (Exception ex)
        {
            _observedException ??= ex;
        }

        if (!IsIdle)
        {
            return false;
        }

        try
        {
            using var gateCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _gate.WaitAsync(gateCts.Token).ConfigureAwait(false);
            _gate.Release();
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return IsIdle;
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
            _lifetimeCts.Cancel();
        }
        catch
        {
            // ignore
        }

        _lifetimeCts.Dispose();
        _gate.Dispose();
    }
}
