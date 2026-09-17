using Frog.Editor;

namespace Frog.Editor.WindowsSmokeTests;

internal static class GameDataSmokeTestHelper
{
    public static void ConfigureInMemory()
    {
        EditorSmokeTestAccess.ConfigureInMemoryRepository();
        EditorSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);
    }
}
