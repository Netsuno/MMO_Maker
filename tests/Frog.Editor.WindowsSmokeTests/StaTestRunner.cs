using System;
using System.Threading;
using System.Windows.Threading;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Hôte STA unique pour tous les smokes UI : une Application WPF, un dispatcher, pas de parallélisme.
/// </summary>
internal static class StaTestRunner
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static Dispatcher? _dispatcher;
    private static Exception? _hostFault;

    public static void Run(Action testBody)
    {
        EnsureHost();
        if (_hostFault is not null)
        {
            throw new InvalidOperationException("STA smoke host failed to start.", _hostFault);
        }

        Exception? captured = null;
        _dispatcher!.Invoke(() =>
        {
            try
            {
                testBody();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is not null)
        {
            throw captured;
        }
    }

    /// <summary>Pompe le dispatcher courant (doit être appelé depuis le thread STA hôte).</summary>
    public static void PumpUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            dispatcher.Invoke(DispatcherPriority.Background, static () => { });
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        if (!predicate())
        {
            throw new TimeoutException("Dispatcher pump timed out before condition was met.");
        }
    }

    private static void EnsureHost()
    {
        lock (Gate)
        {
            if (_dispatcher is not null)
            {
                return;
            }

            using var ready = new ManualResetEventSlim(false);
            _thread = new Thread(() =>
            {
                try
                {
                    Frog.Editor.EditorSmokeTestAccess.EnsureWinFormsInitialized();
                    Frog.Editor.EditorSmokeTestAccess.EnsureWpfApplicationInitialized();
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    _hostFault = ex;
                    ready.Set();
                }
            })
            {
                IsBackground = true,
                Name = "Frog.Editor.WindowsSmoke.STA",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("STA smoke host did not start.");
            }
        }
    }
}
