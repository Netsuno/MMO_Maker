using Frog.Editor;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class GameDataInitializationLeakSmokeTests
{
    [Fact]
    public void GameData_RepeatedOpenClose_DoesNotFailInitialization()
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
                EditorSmokeTestAccess.OpenGameDataRepeatedOpenClose(window);
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
