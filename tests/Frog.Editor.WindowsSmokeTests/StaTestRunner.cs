using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Frog.Editor.WindowsSmokeTests;

internal static class StaTestRunner
{
    public static Task Run(Func<Task> testBody)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                testBody().ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                        {
                            tcs.TrySetException(t.Exception!.InnerExceptions);
                        }
                        else if (t.IsCanceled)
                        {
                            tcs.TrySetCanceled();
                        }
                        else
                        {
                            tcs.TrySetResult();
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }
}
