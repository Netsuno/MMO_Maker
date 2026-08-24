using Frog.Editor;

namespace Frog.Editor.WindowsSmokeTests;

internal static class GameDataSmokeTestHelper
{
    public static void ConfigureInMemory()
    {
        GameDataSmokeTestHelper.ConfigureInMemory();
        EditorSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);
    }
}
