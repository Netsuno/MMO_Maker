using System.Windows.Forms;
using Frog.Editor;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MainFormLifecycleSmokeTests
{
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

            var initEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            EditorTestHooks.MainWorkspaceInitBarrierForTest = async (_, _) =>
            {
                initEntered.TrySetResult();
                // Intentionally ignore cancellation — non-cooperative init for timeout/retry coverage.
                await releaseInit.Task.ConfigureAwait(true);
            };

            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => initEntered.Task.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                window.EditorForm.BeginCloseCleanupViaCoordinatorForTest();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.CloseCoordinatorForTest!.CloseCleanupFailedForTest,
                    TimeSpan.FromSeconds(10));
                Assert.True(window.IsVisible);

                releaseInit.TrySetResult();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.CloseCoordinatorForTest!.AllowFinalCloseForTest
                          || window.EditorForm.CloseCoordinatorForTest!.IsCleanupRunningForTest == false,
                    EditorSmokeTestAccess.DefaultTimeout);
                window.EditorForm.CloseCoordinatorForTest!.RetryCloseCleanupForTest(window.EditorForm);
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.CloseCoordinatorForTest!.AllowFinalCloseForTest,
                    EditorSmokeTestAccess.DefaultTimeout);
            }
            finally
            {
                releaseInit.TrySetResult();
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorTestHooks.GameDataCloseCleanupTimeoutForTest = null;
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }
}
