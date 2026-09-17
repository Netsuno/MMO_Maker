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
public sealed class EditorPlaytestSmokeTests
{
    [Fact]
    public void Playtest_WithoutDurablePostgres_ShowsActionableError()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorTestHooks.AllowNonDurablePlaytest = false;
            // Exes injectés pour prouver que le gate durable s’applique avant le lancement.
            EditorTestHooks.OverrideServerExePath = "fake-Frog.Server.exe";
            EditorTestHooks.OverrideClientExePath = "fake-Frog.Client.exe";
            EditorTestHooks.OverrideSpawnTile = new System.Drawing.Point(0, 0);

            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                var task = window.EditorForm.StartPlaytestAsync();
                StaTestRunner.PumpUntil(() => task.IsCompleted, EditorSmokeTestAccess.DefaultTimeout);
                Assert.True(task.IsCompletedSuccessfully);
                Assert.False(window.EditorForm.IsPlaytestActiveForTest());
                Assert.False(string.IsNullOrWhiteSpace(window.EditorForm.LastPlaytestErrorForTest));
                Assert.Contains("PostgreSQL", window.EditorForm.LastPlaytestErrorForTest!, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (window is not null)
                {
                    window.AllowCloseWithoutPromptForTest();
                    window.Close();
                    window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, static () => { });
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Playtest_CancelDuringLaunch_CleansUpProcesses()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorTestHooks.AllowNonDurablePlaytest = true;
            EditorTestHooks.OverrideServerExePath = "fake-Frog.Server.exe";
            EditorTestHooks.OverrideClientExePath = "fake-Frog.Client.exe";
            EditorTestHooks.OverrideSpawnTile = new System.Drawing.Point(1, 1);
            var launcher = new CancelAwareFakeLauncher();
            EditorTestHooks.OverridePlaytestProcessLauncher = launcher;

            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);

                var start = window.EditorForm.StartPlaytestAsync();
                StaTestRunner.PumpUntil(() => launcher.ServerStartEntered, TimeSpan.FromSeconds(20));
                var stop = window.EditorForm.StopPlaytestAsync();
                StaTestRunner.PumpUntil(() => stop.IsCompleted && start.IsCompleted, EditorSmokeTestAccess.DefaultTimeout);

                Assert.True(
                    launcher.StopCount >= 1,
                    $"expected process cleanup on cancel; StopCount={launcher.StopCount}, LastError={window.EditorForm.LastPlaytestErrorForTest}");
                Assert.False(window.EditorForm.IsPlaytestActiveForTest());
            }
            finally
            {
                if (window is not null)
                {
                    window.AllowCloseWithoutPromptForTest();
                    window.Close();
                    window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, static () => { });
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    private sealed class CancelAwareFakeLauncher : IPlaytestProcessLauncher
    {
        private int _pid = 4000;
        public int StopCount { get; private set; }
        public bool ServerStartEntered { get; private set; }

        public async Task<PlaytestProcessHandle> StartServerAsync(
            PlaytestServerStartRequest request,
            CancellationToken cancellationToken = default)
        {
            ServerStartEntered = true;
            // Mirror EditorPlaytestProcessLauncher: if wait/cancel fails after start, stop locally.
            try
            {
                await Task.Delay(30_000, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                StopCount++;
                throw;
            }

            return new PlaytestProcessHandle
            {
                ProcessId = Interlocked.Increment(ref _pid),
                Role = "server",
                ExecutablePath = request.ExecutablePath,
            };
        }

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

        public bool IsRunning(PlaytestProcessHandle handle) => false;

        public bool HasOwnedProcesses => false;
    }
}
