using Frog.Editor;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class GameDataNpcSmokeTests
{
    [Fact]
    public void GameData_Npc_CreateSavePublish_InMemory()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                EditorSmokeTestAccess.AssertShellReady(window);

                var task = EditorSmokeTestAccess.OpenGameDataAndSaveSampleNpcAsync(window);
                StaTestRunner.PumpUntil(() => task.IsCompleted, EditorSmokeTestAccess.DefaultTimeout);
                Assert.True(task.IsCompletedSuccessfully, task.Exception?.ToString());
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
}
