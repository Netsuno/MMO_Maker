using System.Windows.Forms;
using Frog.Application.Content;
using Frog.Editor;
using Frog.Editor.Forms;
using Frog.Editor.Services;
using Frog.Persistence.PostgreSql;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MainFormLifecycleSmokeTests
{
    private const string FakePgCs = "Host=127.0.0.1;Port=5432;Database=frog_dispose_test;Username=u;Password=p";

    [Theory]
    [InlineData("map")]
    [InlineData("mapEvent")]
    [InlineData("phase8")]
    [InlineData("workspace")]
    public void MainForm_RealClose_WhileInitializationPending_CancelsAndDisposes(string phase)
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);

            var initEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sawCancel = false;

            EditorTestHooks.MainWorkspaceInitBarrierForTest = async (barrierPhase, ct) =>
            {
                if (!string.Equals(barrierPhase, phase, StringComparison.Ordinal))
                {
                    return;
                }

                initEntered.TrySetResult();
                try
                {
                    using var reg = ct.Register(() => Volatile.Write(ref sawCancel, true));
                    await releaseInit.Task.WaitAsync(ct).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    Volatile.Write(ref sawCancel, true);
                    throw;
                }
            };

            MainWindow? window = null;
            var closed = false;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                window.Closed += (_, _) => closed = true;

                StaTestRunner.PumpUntil(
                    () => initEntered.Task.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                Assert.False(window.EditorForm.WorkspaceInitializationTask.IsCompleted);

                window.Close();
                StaTestRunner.PumpUntil(
                    () => Volatile.Read(ref sawCancel)
                          || window.EditorForm.CloseCoordinatorForTest!.AllowFinalCloseForTest,
                    EditorSmokeTestAccess.DefaultTimeout);
                Assert.True(Volatile.Read(ref sawCancel));
                StaTestRunner.PumpUntil(() => closed, EditorSmokeTestAccess.DefaultTimeout);
                var coord = window.EditorForm.CloseCoordinatorForTest!;
                Assert.False(coord.CloseCleanupFailedForTest);
            }
            finally
            {
                releaseInit.TrySetResult();
                if (window is { } w && !closed)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(w);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void MainForm_NonCooperativeInit_TimeoutKeepsWindowAlive_ThenRetryCloses()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);
            EditorTestHooks.GameDataCloseCleanupTimeoutForTest = TimeSpan.FromMilliseconds(200);
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;

            var initEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            EditorTestHooks.MainWorkspaceInitBarrierForTest = async (_, _) =>
            {
                initEntered.TrySetResult();
                // Intentionally ignore cancellation — non-cooperative init for timeout/retry coverage.
                await releaseInit.Task.ConfigureAwait(true);
            };

            MainWindow? window = null;
            var closed = false;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                window.Closed += (_, _) => closed = true;
                StaTestRunner.PumpUntil(
                    () => initEntered.Task.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                window.Close();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.CloseCoordinatorForTest!.CloseCleanupFailedForTest,
                    TimeSpan.FromSeconds(10));
                Assert.True(window.IsVisible);

                releaseInit.TrySetResult();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                window.AllowCloseWithoutPromptForTest();
                window.Close();
                StaTestRunner.PumpUntil(() => closed, EditorSmokeTestAccess.DefaultTimeout);
            }
            finally
            {
                releaseInit.TrySetResult();
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorTestHooks.GameDataCloseCleanupTimeoutForTest = null;
                EditorTestHooks.OverrideMessageBoxResult = null;
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void MainForm_RealClose_WhileSavePending_DrainsThenDisposes()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
            EditorPostgreSqlScope.ResetTestCountersForTest();

            var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sawCancel = false;

            EditorTestHooks.MainFormSaveBarrierForTest = async (_, ct) =>
            {
                saveEntered.TrySetResult();
                try
                {
                    using var reg = ct.Register(() => Volatile.Write(ref sawCancel, true));
                    await releaseSave.Task.WaitAsync(ct).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    Volatile.Write(ref sawCancel, true);
                    throw;
                }
            };

            MainWindow? window = null;
            var closed = false;
            EditorPostgreSqlScope? mapScope = null;
            EditorPostgreSqlScope? mapEventScope = null;
            EditorPostgreSqlScope? phase8Scope = null;
            MapEventsPostgreSqlService? mapEventService = null;
            Phase8ContentPostgreSqlService? phase8Service = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                window.Closed += (_, _) => closed = true;
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                (mapScope, mapEventScope, phase8Scope, mapEventService, phase8Service) =
                    AttachDisposableScopes(window.EditorForm);
                var scopesBefore = EditorPostgreSqlScope.ActiveScopeCountForTest;
                Assert.True(scopesBefore >= 3);

                window.EditorForm.SaveMap();
                StaTestRunner.PumpUntil(() => saveEntered.Task.IsCompleted, TimeSpan.FromSeconds(10));
                Assert.True(window.EditorForm.IsSaveInProgressForTest());
                Assert.False(mapScope!.IsDisposed);
                Assert.False(mapEventScope!.IsDisposed);
                Assert.False(phase8Scope!.IsDisposed);
                Assert.Equal(scopesBefore, EditorPostgreSqlScope.ActiveScopeCountForTest);

                window.Close();
                StaTestRunner.PumpUntil(
                    () => Volatile.Read(ref sawCancel)
                          || window.EditorForm.CloseCoordinatorForTest!.AllowFinalCloseForTest,
                    EditorSmokeTestAccess.DefaultTimeout);
                Assert.True(Volatile.Read(ref sawCancel));
                StaTestRunner.PumpUntil(() => closed, EditorSmokeTestAccess.DefaultTimeout);

                Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
                Assert.Equal(1, mapScope.DisposeCallCountForTest);
                Assert.Equal(1, mapEventScope.DisposeCallCountForTest);
                Assert.Equal(1, phase8Scope.DisposeCallCountForTest);
                Assert.Equal(1, mapEventService!.DisposeCallCountForTest);
                Assert.Equal(1, phase8Service!.DisposeCallCountForTest);
                Assert.True(mapScope.Gate.DisposeCallCountForTest >= 1);
                Assert.False(window.EditorForm.CloseCoordinatorForTest!.CloseCleanupFailedForTest);
            }
            finally
            {
                releaseSave.TrySetResult();
                EditorTestHooks.MainFormSaveBarrierForTest = null;
                if (window is { } w && !closed)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(w);
                }

                mapScope?.Dispose();
                mapEventScope?.Dispose();
                phase8Scope?.Dispose();
                mapEventService?.Dispose();
                phase8Service?.Dispose();
                EditorSmokeTestAccess.ResetHooks();
                EditorPostgreSqlScope.ResetTestCountersForTest();
            }
        });
    }

    [Fact]
    public void MainForm_NonCooperativeSave_TimeoutKeepsScopesAlive_ThenRetryCloses()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);
            EditorTestHooks.GameDataCloseCleanupTimeoutForTest = TimeSpan.FromMilliseconds(250);
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
            EditorPostgreSqlScope.ResetTestCountersForTest();

            var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            EditorTestHooks.MainFormSaveBarrierForTest = async (_, _) =>
            {
                saveEntered.TrySetResult();
                // Intentionally ignore cancellation — non-cooperative save.
                await releaseSave.Task.ConfigureAwait(true);
            };

            MainWindow? window = null;
            var closed = false;
            EditorPostgreSqlScope? mapScope = null;
            EditorPostgreSqlScope? mapEventScope = null;
            EditorPostgreSqlScope? phase8Scope = null;
            MapEventsPostgreSqlService? mapEventService = null;
            Phase8ContentPostgreSqlService? phase8Service = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                window.Closed += (_, _) => closed = true;
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                (mapScope, mapEventScope, phase8Scope, mapEventService, phase8Service) =
                    AttachDisposableScopes(window.EditorForm);

                window.EditorForm.SaveMap();
                StaTestRunner.PumpUntil(() => saveEntered.Task.IsCompleted, TimeSpan.FromSeconds(10));

                window.Close();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.CloseCoordinatorForTest!.CloseCleanupFailedForTest,
                    TimeSpan.FromSeconds(10));
                Assert.True(window.IsVisible);
                Assert.False(mapScope!.IsDisposed);
                Assert.False(mapEventScope!.IsDisposed);
                Assert.False(phase8Scope!.IsDisposed);
                Assert.True(EditorPostgreSqlScope.ActiveScopeCountForTest >= 3);
                Assert.Same(mapScope, window.EditorForm.MapDatabaseScopeForTest);
                Assert.Same(mapEventService, window.EditorForm.MapEventServiceForTest);

                releaseSave.TrySetResult();
                StaTestRunner.PumpUntil(
                    () => !window.EditorForm.IsSaveInProgressForTest(),
                    EditorSmokeTestAccess.DefaultTimeout);

                window.EditorForm.CloseCoordinatorForTest!.RetryCloseCleanupForTest(window.EditorForm);
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.CloseCoordinatorForTest!.AllowFinalCloseForTest,
                    EditorSmokeTestAccess.DefaultTimeout);
                window.AllowCloseWithoutPromptForTest();
                window.Close();
                StaTestRunner.PumpUntil(() => closed, EditorSmokeTestAccess.DefaultTimeout);

                Assert.Equal(0, EditorPostgreSqlScope.ActiveScopeCountForTest);
                Assert.Equal(1, mapScope.DisposeCallCountForTest);
                Assert.Equal(1, mapEventScope.DisposeCallCountForTest);
                Assert.Equal(1, phase8Scope.DisposeCallCountForTest);
                Assert.Equal(1, mapEventService!.DisposeCallCountForTest);
                Assert.Equal(1, phase8Service!.DisposeCallCountForTest);
            }
            finally
            {
                releaseSave.TrySetResult();
                EditorTestHooks.MainFormSaveBarrierForTest = null;
                EditorTestHooks.GameDataCloseCleanupTimeoutForTest = null;
                if (window is not null && !closed)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                mapScope?.Dispose();
                mapEventScope?.Dispose();
                phase8Scope?.Dispose();
                mapEventService?.Dispose();
                phase8Service?.Dispose();
                EditorSmokeTestAccess.ResetHooks();
                EditorPostgreSqlScope.ResetTestCountersForTest();
            }
        });
    }

    private static (
        EditorPostgreSqlScope MapScope,
        EditorPostgreSqlScope MapEventScope,
        EditorPostgreSqlScope Phase8Scope,
        MapEventsPostgreSqlService MapEventService,
        Phase8ContentPostgreSqlService Phase8Service) AttachDisposableScopes(MainForm form)
    {
        var mapScope = new EditorPostgreSqlScope(FakePgCs);
        var mapEventScope = new EditorPostgreSqlScope(FakePgCs);
        var phase8Scope = new EditorPostgreSqlScope(FakePgCs);
        var mapEventService = new MapEventsPostgreSqlService(
            new PostgresMapEventRepository(mapEventScope.Gate),
            mapEventScope.Gate,
            ownsGate: false);
        var phase8Service = new InMemoryPhase8ContentEditorService();
        form.AttachScopesAndServicesForDisposeTest(
            mapScope, mapEventScope, phase8Scope, mapEventService, phase8Service);
        return (mapScope, mapEventScope, phase8Scope, mapEventService, phase8Service);
    }
}
