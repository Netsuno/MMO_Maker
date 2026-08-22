using System;
using System.Threading.Tasks;
using System.Windows;
using Frog.Editor;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class EditorMainWindowSmokeTests
{
    [Fact]
    public Task MainWindow_OpensDemoMap_WithInMemoryRepository()
        => StaTestRunner.Run(RunSmokeAsync);

    private static async Task RunSmokeAsync()
    {
        EditorSmokeTestAccess.ConfigureInMemoryRepository();

        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        MainWindow? window = null;
        try
        {
            window = EditorSmokeTestAccess.CreateAndShowMainWindow();

            await EditorSmokeTestAccess.WaitForWorkspaceReadyAsync(
                window,
                EditorSmokeTestAccess.DefaultTimeout).ConfigureAwait(true);

            EditorSmokeTestAccess.AssertShellReady(window);

            await EditorSmokeTestAccess.AssertDispatcherResponsiveAsync(
                window,
                TimeSpan.FromSeconds(5)).ConfigureAwait(true);
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
