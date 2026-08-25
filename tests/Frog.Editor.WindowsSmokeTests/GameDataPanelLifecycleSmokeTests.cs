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
    public void GameData_PanelLifecycle_UiThreadAndCloseDisposesWithoutExceptions()
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

                EditorTestHooks.PanelOperationBarrierForTest = (_, _) =>
                {
                    lock (threadIds)
                    {
                        threadIds.Add(Environment.CurrentManagedThreadId);
                    }

                    return Task.CompletedTask;
                };

                GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                GameDataSmokeUiDriver.SetText(panel.NameForTest, "LifecycleTilesetA");
                GameDataSmokeUiDriver.Click(panel.BtnSaveForTest);
                StaTestRunner.PumpUntil(() => panel.LifecycleForTest.IsIdle, timeout);
                GameDataSmokeUiDriver.SetText(panel.SearchForTest, "Lifecycle");
                panel.StatusFilterForTest.SelectedIndex = 2;
                StaTestRunner.PumpUntil(() => panel.LifecycleForTest.IsIdle, timeout);

                Assert.NotEmpty(threadIds);
                Assert.All(threadIds, id => Assert.Equal(uiThreadId, id));
                Assert.Empty(observed);

                GameDataSmokeUiDriver.Click(panel.BtnPublishForTest);
                StaTestRunner.PumpUntil(() => panel.LifecycleForTest.IsIdle, timeout);
                GameDataSmokeUiDriver.CloseForm(form, timeout);

                Assert.Null(form.CloseCleanupExceptionForTest);
                Assert.Empty(observed);
                Assert.Null(form.RepositorySetForTest);
                Assert.True(form.IsDisposed);
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
            EditorTestHooks.OverridePostgreSqlConnectionString = "Host=127.0.0.1;Port=5432;Database=unused;Username=u;Password=p";
            EditorTestHooks.OverridePostgreSqlMigrateForTest = _ =>
                throw new InvalidOperationException("forced migrate failure");
            EditorTestHooks.UseSynchronousGameDataInitForTest = false;
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;

            try
            {
                var task = GameDataInitializationService.InitializeAsync();
                var ex = Assert.ThrowsAny<Exception>(() => task.GetAwaiter().GetResult());
                Assert.Contains("forced migrate failure", ex.Message, StringComparison.Ordinal);
                Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
            }
            finally
            {
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
                StaTestRunner.PumpUntil(() => panel.LifecycleForTest.IsIdle, timeout);
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
