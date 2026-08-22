using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Frog.Editor.WindowsSmokeTests;

internal static class StaTestRunner
{
    public static void Run(Action testBody)
    {
        Exception? captured = null;
        using var done = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                testBody();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                done.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!done.Wait(TimeSpan.FromMinutes(2)))
        {
            throw new TimeoutException("STA test thread did not complete within 2 minutes.");
        }

        if (captured is not null)
        {
            throw captured;
        }
    }

    public static Task RunAsync(Func<Task> testBody)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Run(() =>
        {
            try
            {
                testBody().GetAwaiter().GetResult();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>Pompe le dispatcher WPF courant jusqu’à ce que <paramref name="predicate"/> soit vrai ou timeout.</summary>
    public static void PumpUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            dispatcher.Invoke(DispatcherPriority.Background, static () => { });
            Thread.Sleep(15);
        }

        if (!predicate())
        {
            throw new TimeoutException("Dispatcher pump timed out before condition was met.");
        }
    }
}
