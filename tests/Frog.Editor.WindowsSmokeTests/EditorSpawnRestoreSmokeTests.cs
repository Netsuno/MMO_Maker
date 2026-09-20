using System;
using System.IO;
using System.Windows.Forms;
using Frog.Editor;
using Frog.Editor.Config;
using Frog.Editor.Enums;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorSpawnRestoreSmokeTests
{
    [Fact]
    public void Spawn_PersistsInWorkstate_RestoresOnReload_AndHotkeySelectsTool()
    {
        StaTestRunner.Run(() =>
        {
            var temp = Path.Combine(Path.GetTempPath(), $"frog-editor-spawn-smoke-{Guid.NewGuid():N}.json");
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorLocalWorkstate.OverrideFilePathForTest = temp;

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
                var form = window.EditorForm;
                var session = form.GetWorkspaceSessionForTest()!;
                var map = form.GetCanvasMapForTest()!;

                Assert.Null(form.GetPlaytestSpawnForTest());
                form.SetHoverTileForTest(6, 7);
                Assert.Equal((6, 7), form.ResolvePlaytestDialogDefaultsForTest());

                Assert.True(form.TrySetPlaytestSpawnForTest(4, 5));
                Assert.Equal(new System.Drawing.Point(4, 5), form.GetPlaytestSpawnForTest());
                Assert.True(EditorMapSpawnWorkstate.TryRead(session.CurrentMapId, map, out var storedX, out var storedY));
                Assert.Equal((4, 5), (storedX, storedY));

                EditorMapSpawnWorkstate.Write(session.CurrentMapId, map, 8, 2);
                form.RestorePlaytestSpawnFromWorkstateForTest();
                Assert.Equal(new System.Drawing.Point(8, 2), form.GetPlaytestSpawnForTest());

                Assert.True(form.TryProcessCmdKeyForTest(Keys.D));
                Assert.Equal(EditorTool.Spawn, form.GetActiveToolForTest());
                Assert.True(form.TryProcessCmdKeyForTest(Keys.B));
                Assert.Equal(EditorTool.Brush, form.GetActiveToolForTest());

                var assignedId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
                form.PersistCurrentPlaytestSpawnUnderMapIdForTest(assignedId);
                Assert.True(EditorMapSpawnWorkstate.TryRead(assignedId, map, out var migratedX, out var migratedY));
                Assert.Equal((8, 2), (migratedX, migratedY));
            }
            finally
            {
                if (window is not null && !closed)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                    StaTestRunner.PumpUntil(() => closed || !window.IsVisible, TimeSpan.FromSeconds(5));
                }

                EditorSmokeTestAccess.ResetHooks();
                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch
                {
                    // best-effort
                }
            }
        });
    }
}
