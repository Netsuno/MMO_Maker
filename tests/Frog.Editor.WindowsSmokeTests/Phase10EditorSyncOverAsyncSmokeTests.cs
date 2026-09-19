using System.Collections.Concurrent;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Characterizes the editor-open deadlock: a WinForms-like context that never
/// pumps while the caller is blocked. The old
/// <c>ExecuteAsync(...).GetResult()</c> posts a continuation to that context
/// and hangs. <see cref="MapEventsPostgreSqlService.RunOffUiSyncContext"/>
/// starts the work on the thread pool (no captured context).
/// </summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class Phase10EditorSyncOverAsyncSmokeTests
{
    [Fact]
    public void RunOffUiSyncContext_Completes_WhenCallerHasBlockingWinFormsLikeContext()
    {
        StaTestRunner.Run(() =>
        {
            var previous = SynchronizationContext.Current;
            var ctx = new BlockingNoPumpSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            try
            {
                SynchronizationContext? innerContext = ctx;
                var result = MapEventsPostgreSqlService.RunOffUiSyncContext(async () =>
                {
                    await Task.Yield();
                    innerContext = SynchronizationContext.Current;
                    await Task.Delay(15).ConfigureAwait(true);
                    return 42;
                });

                Assert.Equal(42, result);
                Assert.False(ReferenceEquals(innerContext, ctx));
                Assert.Equal(0, ctx.PostedCount);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        });
    }

    /// <summary>
    /// Queues <see cref="Post"/> but never pumps. A continuation captured on this
    /// context while the calling thread is inside <c>GetResult</c> never runs.
    /// </summary>
    private sealed class BlockingNoPumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public int PostedCount { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostedCount++;
            _queue.Enqueue((d, state));
        }

        public override void Send(SendOrPostCallback d, object? state)
            => throw new InvalidOperationException("Send would deadlock the UI thread.");
    }
}
