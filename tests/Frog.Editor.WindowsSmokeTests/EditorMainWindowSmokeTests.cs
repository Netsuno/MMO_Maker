using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Frog.Application.Maps;
using Frog.Editor;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class EditorMainWindowSmokeTests
{
    [Fact]
    public void MainWindow_OpensAndSavesDemoMap_WithInMemoryRepository()
    {
        StaTestRunner.Run(() => RunSmoke(includeSave: true));
    }

    private static void RunSmoke(bool includeSave)
    {
        EditorSmokeTestAccess.ConfigureInMemoryRepository();

        MainWindow? window = null;
        try
        {
            window = EditorSmokeTestAccess.CreateAndShowMainWindow();

            StaTestRunner.PumpUntil(
                () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                EditorSmokeTestAccess.DefaultTimeout);

            if (window.EditorForm.WorkspaceInitializationTask.IsFaulted)
            {
                throw window.EditorForm.WorkspaceInitializationTask.Exception?.GetBaseException()
                      ?? new InvalidOperationException("Workspace initialization failed.");
            }

            EditorSmokeTestAccess.AssertShellReady(window);

            if (includeSave)
            {
                window.Dispatcher.Invoke(() =>
                {
                    var session = window.EditorForm.GetWorkspaceSessionForTest()!;
                    session.CurrentMap!.Name = "Smoke saved";
                    session.MarkDirty();
                    window.EditorForm.SaveMap();
                });

                StaTestRunner.PumpUntil(
                    () => window.EditorForm.PendingSaveOperationForTest?.IsCompleted == true
                          || (!window.EditorForm.IsSaveInProgressForTest()
                              && window.EditorForm.GetWorkspaceSessionForTest()?.IsDirty == false),
                    EditorSmokeTestAccess.DefaultTimeout);

                if (window.EditorForm.PendingSaveOperationForTest?.IsFaulted == true)
                {
                    throw window.EditorForm.PendingSaveOperationForTest.Exception?.GetBaseException()
                          ?? new InvalidOperationException("Save failed.");
                }

                var session = window.EditorForm.GetWorkspaceSessionForTest()!;
                if (session.IsDirty)
                {
                    throw new InvalidOperationException("Session should be clean after save.");
                }

                if (!string.Equals(session.CurrentMap!.Name, "Smoke saved", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Saved map name was not persisted in session.");
                }
            }

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
