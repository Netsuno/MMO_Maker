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
    public void GameData_PanelLifecycle_SerializedOperations_RunOnOwningStaUiThread()
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
                var lifecycle = form.TilesetsForTest.LifecycleForTest;
                var uiThreadId = Environment.CurrentManagedThreadId;
                var threadIds = new List<int>();

                var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var firstBlock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var secondDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                _ = lifecycle.RunAsync(
                    async _ =>
                    {
                        lock (threadIds)
                        {
                            threadIds.Add(Environment.CurrentManagedThreadId);
                        }

                        firstEntered.TrySetResult();
                        await firstBlock.Task.ConfigureAwait(true);
                        lock (threadIds)
                        {
                            threadIds.Add(Environment.CurrentManagedThreadId);
                        }
                    },
                    "serialized-first");

                StaTestRunner.PumpUntil(() => firstEntered.Task.IsCompleted, timeout);
                Assert.False(lifecycle.IsIdle);
                Assert.True(lifecycle.PendingCountForTest >= 1);

                _ = lifecycle.RunAsync(
                    async _ =>
                    {
                        lock (threadIds)
                        {
                            threadIds.Add(Environment.CurrentManagedThreadId);
                        }

                        await Task.Yield();
                        lock (threadIds)
                        {
                            threadIds.Add(Environment.CurrentManagedThreadId);
                        }

                        secondDone.TrySetResult();
                    },
                    "serialized-second");

                Assert.True(lifecycle.PendingCountForTest >= 2);

                _ = Task.Run(() => firstBlock.TrySetResult());
                StaTestRunner.PumpUntil(
                    () => secondDone.Task.IsCompleted && lifecycle.IsIdle,
                    timeout);

                Assert.NotEmpty(threadIds);
                Assert.All(threadIds, id => Assert.Equal(uiThreadId, id));
                Assert.Empty(observed);
                Assert.Null(lifecycle.ObservedExceptionForTest);

                GameDataSmokeUiDriver.CloseForm(form, timeout);
                Assert.True(form.IsDisposed);
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

    [Theory]
    [InlineData("refresh")]
    [InlineData("save")]
    [InlineData("publish")]
    [InlineData("delete")]
    public void GameData_RealClose_WhileOperationPending_DrainsThenDisposes(string operationName)
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
            string? assetRoot = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                var timeout = EditorSmokeTestAccess.DefaultTimeout;
                assetRoot = GameDataSmokeUiDriver.CreateSmokeAssetRoot("tiles/close-op.png");
                var form = GameDataSmokeUiDriver.OpenViaMainWindowCommand(window, timeout);
                var panel = form.TilesetsForTest;
                var lifecycle = panel.LifecycleForTest;

                if (operationName is "save" or "publish" or "delete")
                {
                    GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                    GameDataSmokeUiDriver.SetText(panel.NameForTest, $"CloseDuring-{operationName}");
                    GameDataSmokeUiDriver.SetText(panel.PathForTest, "tiles/close-op.png");
                    if (operationName is "publish" or "delete")
                    {
                        GameDataSmokeUiDriver.ClickAndWait(
                            panel.BtnSaveForTest,
                            () => panel.LifecycleForTest.IsIdle && !panel.IsDirty,
                            timeout);
                    }
                }

                StaTestRunner.PumpUntil(() => panel.LifecycleForTest.IsIdle, timeout);

                var barrierEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var sawCancellation = false;

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                EditorTestHooks.PanelOperationBarrierForTest = async (name, ct) =>
                {
                    if (!string.Equals(name, operationName, StringComparison.Ordinal))
                    {
                        return;
                    }

                    barrierEntered.TrySetResult();
                    try
                    {
                        using var reg = ct.Register(() =>
                        {
                            Volatile.Write(ref sawCancellation, true);
                            releaseBarrier.TrySetResult();
                        });
                        await releaseBarrier.Task.WaitAsync(ct).ConfigureAwait(true);
                    }
                    catch (OperationCanceledException)
                    {
                        Volatile.Write(ref sawCancellation, true);
                        throw;
                    }
                };

                switch (operationName)
                {
                    case "refresh":
                        GameDataSmokeUiDriver.SetText(panel.SearchForTest, "CloseDuring");
                        break;
                    case "save":
                        GameDataSmokeUiDriver.Click(panel.BtnSaveForTest);
                        break;
                    case "publish":
                        GameDataSmokeUiDriver.Click(panel.BtnPublishForTest);
                        break;
                    case "delete":
                        GameDataSmokeUiDriver.Click(panel.BtnDeleteForTest);
                        break;
                }

                StaTestRunner.PumpUntil(() => barrierEntered.Task.IsCompleted, TimeSpan.FromSeconds(10));
                Assert.True(lifecycle.PendingCountForTest > 0);
                Assert.False(lifecycle.IsIdle);

                var reposBefore = form.RepositorySetForTest;
                Assert.NotNull(reposBefore);

                // Real close while operation is still pending — do not wait for IsIdle.
                GameDataSmokeUiDriver.RequestRealClose(form);
                // Close cleanup budget is 30s; wait longer so dispose can finish after cancel drain.
                var disposeTimeout = TimeSpan.FromSeconds(60);
                try
                {
                    StaTestRunner.PumpUntil(() => form.IsDisposed, disposeTimeout);
                }
                catch (TimeoutException)
                {
                    throw new TimeoutException(
                        $"GameData form did not dispose within {disposeTimeout.TotalSeconds:0}s while '{operationName}' was pending. " +
                        $"PendingCount={lifecycle.PendingCountForTest}, IsIdle={lifecycle.IsIdle}, sawCancellation={Volatile.Read(ref sawCancellation)}.");
                }

                Assert.True(Volatile.Read(ref sawCancellation));
                Assert.True(lifecycle.IsIdle || form.IsDisposed);
                Assert.Null(form.RepositorySetForTest);
                Assert.Null(form.CloseCleanupExceptionForTest);
                Assert.False(form.CloseCleanupFailedForTest);
                Assert.Empty(observed);
                Assert.True(form.IsDisposed);
            }
            finally
            {
                EditorTestHooks.PanelOperationBarrierForTest = null;
                EditorTestHooks.OnPanelLifecycleExceptionForTest = null;
                GameDataSmokeUiDriver.CleanupAssetRoot(assetRoot);
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void GameData_RealClose_WhileInitializationPending_CancelsAndDisposes()
    {
        StaTestRunner.Run(() =>
        {
            GameDataSmokeTestHelper.ConfigureInMemory();
            EditorTestHooks.UseSynchronousGameDataInitForTest = false;

            var initEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sawCancel = false;

            EditorTestHooks.GameDataInitBarrierForTest = async ct =>
            {
                initEntered.TrySetResult();
                try
                {
                    using var reg = ct.Register(() =>
                    {
                        Volatile.Write(ref sawCancel, true);
                        releaseInit.TrySetResult();
                    });
                    await releaseInit.Task.WaitAsync(ct).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    Volatile.Write(ref sawCancel, true);
                    throw;
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
                var form = GameDataSmokeUiDriver.OpenPendingInitViaMainWindowCommand(window, timeout);

                StaTestRunner.PumpUntil(() => initEntered.Task.IsCompleted, timeout);
                Assert.False(form.IsInitializedForTest);

                GameDataSmokeUiDriver.RequestRealClose(form);
                StaTestRunner.PumpUntil(() => form.IsDisposed, timeout);

                Assert.True(Volatile.Read(ref sawCancel));
                Assert.Null(form.RepositorySetForTest);
                Assert.True(form.IsDisposed);
                Assert.False(form.CloseCleanupFailedForTest);
            }
            finally
            {
                EditorTestHooks.GameDataInitBarrierForTest = null;
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void GameData_NonCooperativeOperation_TimeoutKeepsFormAndScopeAlive_ThenRetryCloses()
    {
        StaTestRunner.Run(() =>
        {
            GameDataSmokeTestHelper.ConfigureInMemory();
            EditorTestHooks.GameDataCloseCleanupTimeoutForTest = TimeSpan.FromMilliseconds(400);
            var observed = new List<Exception>();
            EditorTestHooks.OnPanelLifecycleExceptionForTest = ex =>
            {
                lock (observed)
                {
                    observed.Add(ex);
                }
            };

            MainWindow? window = null;
            string? assetRoot = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                var timeout = EditorSmokeTestAccess.DefaultTimeout;
                assetRoot = GameDataSmokeUiDriver.CreateSmokeAssetRoot("tiles/noncoop.png");
                var form = GameDataSmokeUiDriver.OpenViaMainWindowCommand(window, timeout);
                var panel = form.TilesetsForTest;
                var lifecycle = panel.LifecycleForTest;

                GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                GameDataSmokeUiDriver.SetText(panel.NameForTest, "NonCoopTileset");
                GameDataSmokeUiDriver.SetText(panel.PathForTest, "tiles/noncoop.png");

                var barrierEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                EditorTestHooks.PanelOperationBarrierForTest = async (name, _) =>
                {
                    if (!string.Equals(name, "save", StringComparison.Ordinal))
                    {
                        return;
                    }

                    barrierEntered.TrySetResult();
                    // Intentionally ignore cancellation — non-cooperative.
                    await releaseBarrier.Task.ConfigureAwait(true);
                };

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                EditorTestHooks.GameDataCloseCleanupTimeoutForTest = TimeSpan.FromMilliseconds(400);

                GameDataSmokeUiDriver.Click(panel.BtnSaveForTest);
                StaTestRunner.PumpUntil(() => barrierEntered.Task.IsCompleted, timeout);
                Assert.False(lifecycle.IsIdle);

                var repos = form.RepositorySetForTest;
                Assert.NotNull(repos);

                GameDataSmokeUiDriver.RequestRealClose(form);
                StaTestRunner.PumpUntil(() => form.CloseCleanupFailedForTest, TimeSpan.FromSeconds(10));

                Assert.False(form.IsDisposed);
                Assert.True(form.Visible);
                Assert.Same(repos, form.RepositorySetForTest);
                Assert.True(form.CloseCleanupFailedForTest);

                releaseBarrier.TrySetResult();
                StaTestRunner.PumpUntil(() => lifecycle.IsIdle, timeout);

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                form.RetryCloseCleanupForTest();
                StaTestRunner.PumpUntil(() => form.IsDisposed, timeout);

                Assert.Null(form.RepositorySetForTest);
                Assert.True(form.IsDisposed);
                Assert.Empty(observed);
            }
            finally
            {
                EditorTestHooks.PanelOperationBarrierForTest = null;
                EditorTestHooks.OnPanelLifecycleExceptionForTest = null;
                EditorTestHooks.GameDataCloseCleanupTimeoutForTest = null;
                GameDataSmokeUiDriver.CleanupAssetRoot(assetRoot);
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
                Assert.True(form.IsDisposed);
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
