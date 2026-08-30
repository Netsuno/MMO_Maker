using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Hôte STA unique pour tous les smokes UI : une Application WPF, un dispatcher, pas de parallélisme.
/// Capture les exceptions UI/domaine et arrête proprement le thread STA en fin de collection.
/// </summary>
internal static class StaTestRunner
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static Dispatcher? _dispatcher;
    private static Exception? _hostFault;
    private static readonly List<Exception> CapturedExceptions = new();
    private static bool _hooksInstalled;

    public static void Run(Action testBody)
    {
        EnsureHost();
        if (_hostFault is not null)
        {
            throw new InvalidOperationException("STA smoke host failed to start.", _hostFault);
        }

        Exception? captured = null;
        // Bound the STA invoke so a hung test body cannot block the runner forever.
        var completed = _dispatcher!.InvokeAsync(() =>
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

        if (completed.Wait(TimeSpan.FromMinutes(2)) != DispatcherOperationStatus.Completed)
        {
            throw new TimeoutException(
                "STA smoke test body did not complete within 2 minutes (possible modal dialog or hung cleanup).");
        }

        if (captured is not null)
        {
            throw captured;
        }

        AssertClean();
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

    public static void ShutdownSuite()
    {
        lock (Gate)
        {
            if (_dispatcher is null)
            {
                return;
            }

            try
            {
                _dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current is { } wpfApp)
                    {
                        wpfApp.Shutdown();
                    }
                });
            }
            catch
            {
                // Best effort — dispatcher may already be shutting down.
            }

            _dispatcher.InvokeShutdown();
            if (!_thread!.Join(TimeSpan.FromSeconds(15)))
            {
                throw new TimeoutException("STA smoke host thread did not exit within 15 seconds.");
            }

            _dispatcher = null;
            _thread = null;
            _hooksInstalled = false;
        }

        AssertClean();
    }

    internal static void ClearCapturedExceptionsForTest()
    {
        lock (Gate)
        {
            CapturedExceptions.Clear();
        }
    }

    internal static void AssertClean()
    {
        lock (Gate)
        {
            if (CapturedExceptions.Count == 0)
            {
                return;
            }

            var details = string.Join(
                Environment.NewLine + "---" + Environment.NewLine,
                CapturedExceptions.Select(ex => ex.ToString()));
            CapturedExceptions.Clear();
            throw new InvalidOperationException(
                $"Unexpected smoke lifecycle exception(s):{Environment.NewLine}{details}");
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
                    InstallExceptionHooks();
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
                IsBackground = false,
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

    private static void InstallExceptionHooks()
    {
        if (_hooksInstalled)
        {
            return;
        }

        System.Windows.Forms.Application.SetUnhandledExceptionMode(
            System.Windows.Forms.UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, args) => RecordException(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                RecordException(ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            RecordException(args.Exception);
            args.SetObserved();
        };
        _hooksInstalled = true;
    }

    private static void RecordException(Exception ex)
    {
        lock (Gate)
        {
            CapturedExceptions.Add(ex);
        }
    }
}
