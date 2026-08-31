using System;
using Frog.Editor;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorMainWindowSmokeTests
{
    [Fact]
    public void MainWindow_OpensAndSavesDemoMap_WithInMemoryRepository()
    {
        StaTestRunner.Run(() => RunSmoke());
    }

    private static void RunSmoke()
    {
        EditorSmokeTestAccess.ConfigureInMemoryRepository();

        MainWindow? window = null;
        var closed = false;
        try
        {
            window = EditorSmokeTestAccess.CreateAndShowMainWindow();
            window.Closed += (_, _) => closed = true;

            StaTestRunner.PumpUntil(
                () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                EditorSmokeTestAccess.DefaultTimeout);

            if (window.EditorForm.WorkspaceInitializationTask.IsFaulted)
            {
                throw window.EditorForm.WorkspaceInitializationTask.Exception?.GetBaseException()
                      ?? new InvalidOperationException("Workspace initialization failed.");
            }

            EditorSmokeTestAccess.AssertShellReady(window);

            var session = window.EditorForm.GetWorkspaceSessionForTest()!;
            session.CurrentMap!.Name = "Smoke saved";
            session.MarkDirty();
            window.EditorForm.SaveMap();

            StaTestRunner.PumpUntil(
                () => window.EditorForm.PendingSaveOperationForTest?.IsCompleted == true
                      || (!window.EditorForm.IsSaveInProgressForTest() && !session.IsDirty),
                EditorSmokeTestAccess.DefaultTimeout);

            if (window.EditorForm.PendingSaveOperationForTest?.IsFaulted == true)
            {
                throw window.EditorForm.PendingSaveOperationForTest.Exception?.GetBaseException()
                      ?? new InvalidOperationException("Save failed.");
            }

            if (session.IsDirty)
            {
                throw new InvalidOperationException("Session should be clean after save.");
            }

            if (!string.Equals(session.CurrentMap!.Name, "Smoke saved", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Saved map name was not persisted in session.");
            }
        }
        finally
        {
            if (window is not null && !closed)
            {
                EditorSmokeTestAccess.ForceCloseMainWindow(window);
                StaTestRunner.PumpUntil(() => closed || !window.IsVisible, TimeSpan.FromSeconds(5));
            }

            EditorSmokeTestAccess.ResetHooks();
        }
    }
}
