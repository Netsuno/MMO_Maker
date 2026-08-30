using System.Windows.Forms;
using Frog.Persistence.PostgreSql;

namespace Frog.Editor.Services;

/// <summary>État machine de fermeture asynchrone pour MainForm (P8-I1).</summary>
internal sealed class EditorMainFormCloseCoordinator
{
    private readonly Func<Task> _stopPlaytestAsync;
    private readonly Func<Task?> _getWorkspaceInitTask;
    private readonly Func<IReadOnlyList<EditorPostgreSqlScope?>> _getScopes;
    private readonly Action _disposeServicesAndScopes;
    private readonly Action<bool> _setClosingUiState;
    private readonly Func<bool> _hasPendingOperations;
    private readonly Action _requestFinalClose;

    private CancellationTokenSource? _workspaceInitCts;
    private CancellationTokenSource? _closeCts;
    private bool _allowCloseAfterCleanup;
    private bool _cleanupRunning;
    private bool _closeCleanupFailed;
    private Exception? _closeCleanupException;
    private bool _editorClosing;
    private bool _disposed;

    public EditorMainFormCloseCoordinator(
        Func<Task> stopPlaytestAsync,
        Func<Task?> getWorkspaceInitTask,
        Func<IReadOnlyList<EditorPostgreSqlScope?>> getScopes,
        Action disposeServicesAndScopes,
        Action<bool> setClosingUiState,
        Func<bool> hasPendingOperations,
        Action requestFinalClose)
    {
        _stopPlaytestAsync = stopPlaytestAsync;
        _getWorkspaceInitTask = getWorkspaceInitTask;
        _getScopes = getScopes;
        _disposeServicesAndScopes = disposeServicesAndScopes;
        _setClosingUiState = setClosingUiState;
        _hasPendingOperations = hasPendingOperations;
        _requestFinalClose = requestFinalClose;
    }

    public CancellationToken WorkspaceInitToken =>
        (_workspaceInitCts ??= new CancellationTokenSource()).Token;

    public bool AllowFinalCloseForTest => _allowCloseAfterCleanup;

    public bool CloseCleanupFailedForTest => _closeCleanupFailed;

    public Exception? CloseCleanupExceptionForTest => _closeCleanupException;

    public bool IsEditorClosingForTest => _editorClosing;

    public bool IsCleanupRunningForTest => _cleanupRunning;

    public void BeginWorkspaceInitialization()
    {
        _workspaceInitCts?.Cancel();
        _workspaceInitCts?.Dispose();
        _workspaceInitCts = new CancellationTokenSource();
    }

    public void CancelWorkspaceInitialization()
    {
        _workspaceInitCts?.Cancel();
    }

    public bool TryHandleFormClosing(FormClosingEventArgs e, Func<Task<bool>> confirmDiscardIfDirtyAsync)
    {
        if (_allowCloseAfterCleanup)
        {
            return false;
        }

        e.Cancel = true;
        if (_cleanupRunning)
        {
            return true;
        }

        _ = RunAsyncClosePipelineAsync(confirmDiscardIfDirtyAsync);
        return true;
    }

    internal void RetryCloseCleanupForTest(Form form)
    {
        if (_allowCloseAfterCleanup || _cleanupRunning || form.IsDisposed)
        {
            return;
        }

        _cleanupRunning = true;
        _ = RunAsyncClosePipelineAsync(() => Task.FromResult(true));
    }

    private async Task RunAsyncClosePipelineAsync(Func<Task<bool>> confirmDiscardIfDirtyAsync)
    {
        if (!await confirmDiscardIfDirtyAsync().ConfigureAwait(true))
        {
            _cleanupRunning = false;
            _editorClosing = false;
            _setClosingUiState(true);
            return;
        }

        _cleanupRunning = true;
        _editorClosing = true;
        _closeCts?.Cancel();
        _closeCts = new CancellationTokenSource();
        _setClosingUiState(false);

        var timeout = EditorTestHooks.GameDataCloseCleanupTimeoutForTest ?? TimeSpan.FromSeconds(30);
        try
        {
            var success = await RunCloseCleanupAsync(timeout).ConfigureAwait(true);
            if (!success)
            {
                _closeCleanupFailed = true;
                _cleanupRunning = false;
                _editorClosing = false;
                _setClosingUiState(true);
                return;
            }

            _closeCleanupFailed = false;
            _allowCloseAfterCleanup = true;
            _cleanupRunning = false;
            _requestFinalClose();
        }
        catch (Exception ex)
        {
            _closeCleanupException = ex;
            _closeCleanupFailed = true;
            _cleanupRunning = false;
            _editorClosing = false;
            _setClosingUiState(true);
            return;
        }
    }

    public async Task<bool> RunCloseCleanupAsync(TimeSpan timeout)
    {
        CancelWorkspaceInitialization();

        var initTask = _getWorkspaceInitTask();
        if (initTask is not null)
        {
            try
            {
                await initTask.WaitAsync(timeout).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Init cancelled — continue draining scopes.
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        try
        {
            await _stopPlaytestAsync().ConfigureAwait(true);
        }
        catch
        {
            // best-effort
        }

        var deadline = DateTime.UtcNow + timeout;
        while (_hasPendingOperations() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50).ConfigureAwait(true);
        }

        if (_hasPendingOperations())
        {
            return false;
        }

        var remaining = deadline - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.FromMilliseconds(250);
        }

        foreach (var scope in _getScopes())
        {
            if (scope is null || scope.IsDisposed)
            {
                continue;
            }

            await scope.DrainAsync(_closeCts?.Token ?? CancellationToken.None).ConfigureAwait(true);
        }

        if (!_disposed)
        {
            _disposeServicesAndScopes();
            _disposed = true;
        }

        return true;
    }

    /// <summary>Shutdown sans fermer la fenêtre (ex. fermeture MainWindow WPF pendant init).</summary>
    internal async Task<bool> TryShutdownWithoutCloseAsync(
        Func<Task<bool>> confirmDiscardIfDirtyAsync,
        TimeSpan timeout)
    {
        if (_allowCloseAfterCleanup)
        {
            return true;
        }

        if (!await confirmDiscardIfDirtyAsync().ConfigureAwait(true))
        {
            return false;
        }

        _editorClosing = true;
        _closeCts?.Cancel();
        _closeCts = new CancellationTokenSource();
        _setClosingUiState(false);

        var success = await RunCloseCleanupAsync(timeout).ConfigureAwait(true);
        if (!success)
        {
            _closeCleanupFailed = true;
            _editorClosing = false;
            _setClosingUiState(true);
            return false;
        }

        _closeCleanupFailed = false;
        _allowCloseAfterCleanup = true;
        _editorClosing = false;
        return true;
    }
}
