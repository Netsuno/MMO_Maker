using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Frog.Editor;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class EditorMainWindowSmokeTests
{
    [Fact]
    public void MainWindow_OpensDemoMap_WithInMemoryRepository()
    {
        StaTestRunner.Run(RunSmoke);
    }

    private static void RunSmoke()
    {
        EditorSmokeTestAccess.ConfigureInMemoryRepository();

        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        MainWindow? window = null;
        try
        {
            window = EditorSmokeTestAccess.CreateAndShowMainWindow();

            StaTestRunner.PumpUntil(
                () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                EditorSmokeTestAccess.DefaultTimeout);

            if (window.EditorForm.WorkspaceInitializationTask.IsFaulted)
            {
                throw window.EditorForm.WorkspaceInitializationTask.Exception
                      ?? new InvalidOperationException("Workspace initialization failed.");
            }

            EditorSmokeTestAccess.AssertShellReady(window);

            var dispatcherDone = false;
            window.Dispatcher.InvokeAsync(() => dispatcherDone = true, DispatcherPriority.Normal);
            StaTestRunner.PumpUntil(() => dispatcherDone, TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (window is not null)
            {
                EditorSmokeTestAccess.CloseMainWindow(window);
            }

            if (System.Windows.Application.Current is not null)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }
    }
}
