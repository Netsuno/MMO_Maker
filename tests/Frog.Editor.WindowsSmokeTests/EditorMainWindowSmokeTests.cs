using System;
using System.Windows.Threading;
using Frog.Application.Maps;
using Frog.Editor;
using Xunit;;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class EditorMainWindowSmokeTests
{
    [Fact]
    public void MainWindow_OpensDemoMap_WithInMemoryRepository()
    {
        StaTestRunner.Run(RunOpenSmoke);
    }

    [Fact]
    public void MainWindow_SaveDraft_InMemoryRepository()
    {
        StaTestRunner.Run(RunSaveSmoke);
    }

    private static void RunOpenSmoke()
    {
        RunSmokeCore(includeSave: false);
    }

    private static void RunSaveSmoke()
    {
        RunSmokeCore(includeSave: true);
    }

    private static void RunSmokeCore(bool includeSave)
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
                Exception? saveError = null;
                window.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var session = window.EditorForm.GetWorkspaceSessionForTest()!;
                        var beforeRev = session.CurrentRevision;
                        session.CurrentMap!.Name = "Smoke saved";
                        session.MarkDirty();
                        var saveTask = session.SaveCurrentAsync(MapPublishStatus.Draft);
                        saveTask.Wait(EditorSmokeTestAccess.DefaultTimeout);
                        if (saveTask.IsFaulted)
                        {
                            throw saveTask.Exception?.GetBaseException()
                                  ?? new InvalidOperationException("Save failed.");
                        }

                        EditorSmokeTestAccess.AssertSaveSuccess(saveTask.Result, beforeRev);
                        if (session.IsDirty)
                        {
                            throw new InvalidOperationException("Session should be clean after save.");
                        }
                    }
                    catch (Exception ex)
                    {
                        saveError = ex;
                    }
                });

                if (saveError is not null)
                {
                    throw saveError;
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
