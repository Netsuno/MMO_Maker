using Frog.Editor.Services;
using Frog.Persistence.PostgreSql;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Pure coordinator tests — no shared STA host, no WinForms pump.
/// Proves non-cooperative init timeout, scope retention, retry, and exactly-once disposal.
/// </summary>
public sealed class EditorMainFormCloseCoordinatorTests
{
    private const string FakePgCs = "Host=127.0.0.1;Port=5432;Database=frog_coordinator_test;Username=u;Password=p";

    [Fact]
    public async Task NonCooperativeInit_TimeoutKeepsScopesAlive_ThenRetryDisposesOnce()
    {
        EditorPostgreSqlScope.ResetTestCountersForTest();
        var releaseInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initTask = Task.Run(async () =>
        {
            await releaseInit.Task.ConfigureAwait(false);
        });

        var scope = new EditorPostgreSqlScope(FakePgCs);
        var disposeCount = 0;
        var stopPlaytestOnUiThread = false;

        var coordinator = new EditorMainFormCloseCoordinator(
            () =>
            {
                stopPlaytestOnUiThread = SynchronizationContext.Current is not null;
                return Task.CompletedTask;
            },
            () => initTask,
            () => new EditorPostgreSqlScope?[] { scope },
            () => Interlocked.Increment(ref disposeCount),
            _ => { },
            () => !initTask.IsCompleted,
            () => { });

        var failed = await coordinator.RunCloseCleanupAsync(TimeSpan.FromMilliseconds(200));
        Assert.False(failed);
        Assert.False(scope.IsDisposed);
        Assert.Equal(0, disposeCount);
        Assert.Equal(1, EditorPostgreSqlScope.ActiveScopeCountForTest);

        releaseInit.TrySetResult();
        await initTask.WaitAsync(TimeSpan.FromSeconds(5));

        var succeeded = await coordinator.RunCloseCleanupAsync(TimeSpan.FromSeconds(5));
        Assert.True(succeeded);
        Assert.True(scope.IsDisposed);
        Assert.Equal(1, scope.DisposeCallCountForTest);
        Assert.Equal(1, disposeCount);
        Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
    }

    [Fact]
    public async Task NonCooperativeInit_WindowAliveSemantics_NoDeadlockUnderParallelWait()
    {
        var releaseInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initTask = Task.Run(async () =>
        {
            initStarted.TrySetResult();
            await releaseInit.Task.ConfigureAwait(false);
        });

        var scope = new EditorPostgreSqlScope(FakePgCs);
        var coordinator = new EditorMainFormCloseCoordinator(
            () => Task.CompletedTask,
            () => initTask,
            () => new EditorPostgreSqlScope?[] { scope },
            () => scope.Dispose(),
            _ => { },
            () => !initTask.IsCompleted,
            () => { });

        await initStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var closeTask = coordinator.RunCloseCleanupAsync(TimeSpan.FromMilliseconds(150));
        var completed = await Task.WhenAny(closeTask, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(closeTask, completed);
        Assert.False(await closeTask);
        Assert.False(scope.IsDisposed);

        releaseInit.TrySetResult();
        await initTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await coordinator.RunCloseCleanupAsync(TimeSpan.FromSeconds(5)));
        Assert.True(scope.IsDisposed);
    }

    [Fact]
    public async Task StopPlaytestAsync_InvokedWithCapturedSyncContext()
    {
        var uiContext = new SingleThreadSynchronizationContext();
        var invokedOnUi = false;

        var coordinator = new EditorMainFormCloseCoordinator(
            () =>
            {
                invokedOnUi = ReferenceEquals(SynchronizationContext.Current, uiContext);
                return Task.CompletedTask;
            },
            () => null,
            () => Array.Empty<EditorPostgreSqlScope?>(),
            () => { },
            _ => { },
            () => false,
            () => { });

        await uiContext.RunAsync(async () =>
        {
            Assert.True(await coordinator.RunCloseCleanupAsync(TimeSpan.FromSeconds(5)));
        });

        Assert.True(invokedOnUi);
    }

    [Fact]
    public async Task ExactlyOnceDisposal_EvenWhenRetryCalledTwice()
    {
        EditorPostgreSqlScope.ResetTestCountersForTest();
        var disposeCount = 0;
        var scope = new EditorPostgreSqlScope(FakePgCs);

        var coordinator = new EditorMainFormCloseCoordinator(
            () => Task.CompletedTask,
            () => null,
            () => new EditorPostgreSqlScope?[] { scope },
            () => Interlocked.Increment(ref disposeCount),
            _ => { },
            () => false,
            () => { });

        Assert.True(await coordinator.RunCloseCleanupAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, disposeCount);
        Assert.Equal(1, scope.DisposeCallCountForTest);

        // Second cleanup must not double-dispose.
        Assert.True(await coordinator.RunCloseCleanupAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, disposeCount);
        Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
    }

    /// <summary>Minimal sync context for proving ConfigureAwait(true) marshaling without WinForms.</summary>
    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public Task RunAsync(Func<Task> work)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(this);
                _ = RunCoreAsync(work, tcs);
            }, null);
            PumpUntil(() => tcs.Task.IsCompleted, TimeSpan.FromSeconds(10));
            return tcs.Task;
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_queue)
            {
                _queue.Enqueue((d, state));
            }
        }

        private async Task RunCoreAsync(Func<Task> work, TaskCompletionSource tcs)
        {
            try
            {
                await work().ConfigureAwait(true);
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private void PumpUntil(Func<bool> predicate, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!predicate() && DateTime.UtcNow < deadline)
            {
                (SendOrPostCallback? cb, object? state) item;
                lock (_queue)
                {
                    if (_queue.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    item = _queue.Dequeue();
                }

                item.cb!(item.state);
            }

            if (!predicate())
            {
                throw new TimeoutException("SynchronizationContext pump timed out.");
            }
        }
    }
}
