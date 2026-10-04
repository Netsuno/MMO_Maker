using System.IO;
using Frog.Editor;
using Frog.Editor.Assets;
using Frog.Editor.Forms.GameData;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class GameDataMapObjectSmokeTests
{
    [Fact]
    public void GameData_MapObject_Publish_AppearsInPlacementPalette()
    {
        StaTestRunner.Run(() =>
        {
            var prefabDir = Path.Combine(Path.GetTempPath(), $"frog-map-object-prefabs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(prefabDir);
            PrefabSpriteCache.ResetCacheForTest();
            PrefabSpriteCache.OverrideDirectoryForTest = prefabDir;
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            MainWindow? window = null;
            string? assetRoot = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                EditorSmokeTestAccess.AssertShellReady(window);
                Assert.Equal(8, window.EditorForm.LeftToolsForTest.VisiblePrefabCountForTest);

                assetRoot = GameDataSmokeUiDriver.CreateSmokeAssetRoot("prefabs/lampe.png");
                var form = GameDataSmokeUiDriver.OpenViaMainWindowCommand(window, EditorSmokeTestAccess.DefaultTimeout);
                form.SelectCategoryForTest(GameDataForm.MapObjectCategoryIndex);
                var panel = form.MapObjectsForTest;
                GameDataSmokeUiDriver.Click(panel.BtnNewForTest);
                GameDataSmokeUiDriver.SetText(panel.NameForTest, "Lampe de table");
                GameDataSmokeUiDriver.SetText(panel.PathForTest, "prefabs/lampe.png");
                StaTestRunner.PumpUntil(
                    () => panel.PreviewForTest.PreviewState == AssetPreviewState.Loaded,
                    EditorSmokeTestAccess.DefaultTimeout);

                GameDataSmokeUiDriver.ClickAndWait(
                    panel.BtnSaveForTest,
                    () => !panel.IsDirty,
                    EditorSmokeTestAccess.DefaultTimeout);
                Assert.DoesNotContain(
                    window.EditorForm.LeftToolsForTest.PrefabLabelsForTest,
                    label => label == "Lampe de table");

                GameDataSmokeUiDriver.ClickPublishAndWait(
                    panel.BtnPublishForTest,
                    panel.ListForTest,
                    "Lampe de table",
                    () => panel.LifecycleForTest.IsIdle,
                    EditorSmokeTestAccess.DefaultTimeout);
                GameDataSmokeUiDriver.AssertListContains(panel.ListForTest, "Lampe de table", "Published");

                GameDataSmokeUiDriver.CloseForm(form, EditorSmokeTestAccess.DefaultTimeout);
                var reload = window.EditorForm.PublishedMapObjectReloadTaskForTest;
                Assert.NotNull(reload);
                StaTestRunner.PumpUntil(() => reload!.IsCompleted, EditorSmokeTestAccess.DefaultTimeout);
                Assert.True(reload!.IsCompletedSuccessfully, reload.Exception?.ToString());

                var labels = window.EditorForm.LeftToolsForTest.PrefabLabelsForTest;
                Assert.Contains("Canapé", labels);
                Assert.Contains("Lampe de table", labels);
                Assert.Equal(9, window.EditorForm.LeftToolsForTest.VisiblePrefabCountForTest);
            }
            finally
            {
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                GameDataSmokeUiDriver.CleanupAssetRoot(assetRoot);
                PrefabSpriteCache.ResetCacheForTest();
                PrefabSpriteCache.OverrideDirectoryForTest = null;
                try
                {
                    Directory.Delete(prefabDir, recursive: true);
                }
                catch
                {
                    // best-effort
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }
}
