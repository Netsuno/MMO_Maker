using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Frog.Application.Playtest;
using Frog.Editor;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorPlaytestCloseSmokeTests
{
    [Fact]
    public void Close_ActivePlaytest_CleanMap_AwaitsStop_ThenCloses()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorTestHooks.AllowNonDurablePlaytest = true;
            EditorTestHooks.OverrideServerExePath = "fake-server";
            EditorTestHooks.OverrideClientExePath = "fake-client";
            EditorTestHooks.OverrideSpawnTile = new System.Drawing.Point(0, 0);
            var launcher = new StickyFakeLauncher();
            EditorTestHooks.OverridePlaytestProcessLauncher = launcher;

            MainWindow? window = null;
            var closed = false;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                window.Closed += (_, _) => closed = true;
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                var start = window.EditorForm.StartPlaytestAsync();
                StaTestRunner.PumpUntil(() => start.IsCompleted && window.EditorForm.IsPlaytestActiveForTest(),
                    EditorSmokeTestAccess.DefaultTimeout);
                Assert.True(window.EditorForm.IsPlaytestActiveForTest());
                Assert.False(window.EditorForm.HasUnsavedChangesForTest());

                window.Close();
                window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, static () => { });
                StaTestRunner.PumpUntil(() => closed, EditorSmokeTestAccess.DefaultTimeout);

                Assert.True(closed);
                Assert.True(launcher.StopCount >= 2, $"StopCount={launcher.StopCount}");
                Assert.False(window.EditorForm.IsPlaytestActiveForTest());
                Assert.False(window.EditorForm.HasOwnedPlaytestProcessesForTest());
            }
            finally
            {
                if (window is not null && !closed)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Close_ActivePlaytest_DirtyMap_CancelKeepsOpen_AndPlaytestStillStoppable()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorTestHooks.AllowNonDurablePlaytest = true;
            EditorTestHooks.OverrideDialogService = new CancelDialogService();
            EditorTestHooks.OverrideServerExePath = "fake-server";
            EditorTestHooks.OverrideClientExePath = "fake-client";
            EditorTestHooks.OverrideSpawnTile = new System.Drawing.Point(1, 1);
            var launcher = new StickyFakeLauncher();
            EditorTestHooks.OverridePlaytestProcessLauncher = launcher;

            MainWindow? window = null;
            var closed = false;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                window.Closed += (_, _) => closed = true;
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                var start = window.EditorForm.StartPlaytestAsync();
                StaTestRunner.PumpUntil(() => start.IsCompleted && window.EditorForm.IsPlaytestActiveForTest(),
                    EditorSmokeTestAccess.DefaultTimeout);

                var session = window.EditorForm.GetWorkspaceSessionForTest()!;
                session.CurrentMap!.Name = "DirtyPlaytestClose";
                session.MarkDirty();

                window.Close();
                window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, static () => { });
                window.Dispatcher.Invoke(DispatcherPriority.Background, static () => { });
                Thread.Sleep(200);
                window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, static () => { });

                Assert.False(closed);
                Assert.True(window.IsVisible);
                // Playtest stop runs before dirty prompt; dirty cancel keeps window open.
                Assert.True(launcher.StopCount >= 1);

                EditorSmokeTestAccess.ForceCloseMainWindow(window);
                StaTestRunner.PumpUntil(() => closed, TimeSpan.FromSeconds(5));
            }
            finally
            {
                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    private sealed class CancelDialogService : IEditorDialogService
    {
        public EditorPromptChoice PromptSaveDiscardCancel(string message, string title) => EditorPromptChoice.Cancel;
        public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title) { }
        public void ShowWarning(string message, string title) { }
        public void ShowError(string message, string title) { }
    }

    /// <summary>Fake that completes StartServer/StartClient immediately (no READY wait — not Owned launcher).</summary>
    private sealed class StickyFakeLauncher : IPlaytestProcessLauncher
    {
        private int _pid = 9000;
        public int StopCount { get; private set; }

        public Task<PlaytestProcessHandle> StartServerAsync(
            PlaytestServerStartRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PlaytestProcessHandle
            {
                ProcessId = Interlocked.Increment(ref _pid),
                Role = "server",
                ExecutablePath = request.ExecutablePath,
            });

        public Task<PlaytestProcessHandle> StartClientAsync(
            PlaytestClientStartRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PlaytestProcessHandle
            {
                ProcessId = Interlocked.Increment(ref _pid),
                Role = "client",
                ExecutablePath = request.ExecutablePath,
            });

        public Task StopAsync(PlaytestProcessHandle handle, CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public bool IsRunning(PlaytestProcessHandle handle) => StopCount == 0;

        public bool HasOwnedProcesses => StopCount == 0;
    }
}
