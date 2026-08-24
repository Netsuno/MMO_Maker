using System.Windows.Forms;
using Frog.Editor;
using Frog.Editor.Forms.GameData;
using Frog.Editor.Services;
using Frog.Persistence.PostgreSql;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class GameDataPanelLifecycleSmokeTests
{
    [Fact]
    public void GameData_PanelLifecycle_SerializesOnUiThread_AndCloseDrainsWithoutExceptions()
    {
        StaTestRunner.Run(() =>
        {
            GameDataSmokeTestHelper.ConfigureInMemory();
            var observed = new List<Exception>();
            EditorTestHooks.OnPanelLifecycleExceptionForTest = ex =>
            {
                lock (observed)
                {
                    observed.Add(ex);
                }
            };

            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                var timeout = EditorSmokeTestAccess.DefaultTimeout;
                var form = GameDataSmokeUiDriver.OpenViaMainWindowCommand(window, timeout);
                var panel = form.TilesetsForTest;
                var uiThreadId = Environment.CurrentManagedThreadId;
                var threadIds = new List<int>();
                var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var barrierHits = 0;

                EditorTestHooks.PanelOperationBarrierForTest = async (name, ct) =>
                {
                    var hit = Interlocked.Increment(ref barrierHits);
                    lock (threadIds)
                    {
                        threadIds.Add(Environment.CurrentManagedThreadId);
                    }

                    if (hit == 1)
                    {
                        firstEntered.TrySetResult();
                        await releaseFirst.Task.WaitAsync(ct).ConfigureAwait(true);
                    }
                };

                GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                GameDataSmokeUiDriver.SetText(panel.NameForTest, "LifecycleTilesetA");
                GameDataSmokeUiDriver.Click(panel.BtnSaveForTest);

                StaTestRunner.PumpUntil(() => firstEntered.Task.IsCompleted, timeout);

                GameDataSmokeUiDriver.SetText(panel.SearchForTest, "Lifecycle");
                StaTestRunner.PumpUntil(() => Volatile.Read(ref barrierHits) >= 2, timeout);

                releaseFirst.TrySetResult();
                StaTestRunner.PumpUntil(() => panel.LifecycleForTest.IsIdle, timeout);

                Assert.All(threadIds, id => Assert.Equal(uiThreadId, id));
                Assert.Empty(observed);

                EditorTestHooks.PanelOperationBarrierForTest = async (name, ct) =>
                {
                    if (name is "save" or "publish" or "delete" or "initialize")
                    {
                        await Task.Delay(150, ct).ConfigureAwait(true);
                    }
                };

                GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                GameDataSmokeUiDriver.SetText(panel.NameForTest, "LifecycleTilesetB");
                GameDataSmokeUiDriver.Click(panel.BtnSaveForTest);
                GameDataSmokeUiDriver.Click(panel.BtnPublishForTest);
                GameDataSmokeUiDriver.SetText(panel.SearchForTest, "Life");
                panel.StatusFilterForTest.SelectedIndex = 2;
                GameDataSmokeUiDriver.Click(panel.BtnDeleteForTest);

                EditorTestHooks.PanelOperationBarrierForTest = null;
                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                GameDataSmokeUiDriver.CloseForm(form, timeout);

                Assert.Null(form.CloseCleanupExceptionForTest);
                Assert.Empty(observed);
                Assert.Null(form.RepositorySetForTest);
            }
            finally
            {
                EditorTestHooks.PanelOperationBarrierForTest = null;
                EditorTestHooks.OnPanelLifecycleExceptionForTest = null;
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void GameData_InitFailure_DisposesScope_ActiveCountZero()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            Environment.SetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory, null);
            EditorPostgreSqlScope.ResetTestCountersForTest();
            GameDataInitializationService.ResetInjectedMigrateCountForTest();
            EditorTestHooks.OverridePostgreSqlConnectionString =
                "Host=127.0.0.1;Port=1;Database=frog_missing;Username=frog;Password=frog;Timeout=1;Command Timeout=1";
            EditorTestHooks.OverridePostgreSqlMigrateForTest = _ =>
                throw new InvalidOperationException("forced migrate failure");
            EditorTestHooks.UseSynchronousGameDataInitForTest = false;
            EditorTestHooks.GameDataNonModalForTest = true;
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;

            MainWindow? window = null;
            GameDataForm? form = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                EditorTestHooks.OnGameDataFormShown = opened => form = (GameDataForm)opened;
                MainWindow.CmdGameData.Execute(null, window);

                StaTestRunner.PumpUntil(
                    () => form is not null && form.IsDisposed,
                    TimeSpan.FromSeconds(20));

                Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
            }
            finally
            {
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void GameData_RapidFilterThenPublishAndClose_NoLifecycleExceptions()
    {
        StaTestRunner.Run(() =>
        {
            GameDataSmokeTestHelper.ConfigureInMemory();
            var observed = new List<Exception>();
            EditorTestHooks.OnPanelLifecycleExceptionForTest = ex =>
            {
                lock (observed)
                {
                    observed.Add(ex);
                }
            };

            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                var timeout = EditorSmokeTestAccess.DefaultTimeout;
                var form = GameDataSmokeUiDriver.OpenViaMainWindowCommand(window, timeout);
                var panel = form.TilesetsForTest;

                GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                GameDataSmokeUiDriver.SetText(panel.NameForTest, "RapidFilterTileset");
                for (var i = 0; i < 8; i++)
                {
                    GameDataSmokeUiDriver.SetText(panel.SearchForTest, i % 2 == 0 ? "Rapid" : "Filter");
                    panel.StatusFilterForTest.SelectedIndex = i % 3;
                }

                GameDataSmokeUiDriver.Click(panel.BtnPublishForTest);
                StaTestRunner.PumpUntil(() => panel.LifecycleForTest.IsIdle || panel.IsDirty, timeout);
                GameDataSmokeUiDriver.CloseForm(form, timeout);
                Assert.Empty(observed);
                Assert.Null(form.CloseCleanupExceptionForTest);
            }
            finally
            {
                EditorTestHooks.OnPanelLifecycleExceptionForTest = null;
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }
}
