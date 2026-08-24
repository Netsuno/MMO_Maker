using System.IO;
using Frog.Application.Assets;
using Frog.Editor;
using Frog.Editor.Forms.GameData;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class GameDataAssetPreviewSmokeTests
{
    [Fact]
    public void AssetPreview_ValidMissingCorruptTraversalRefreshAndGcRetention()
    {
        StaTestRunner.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), $"frog-preview-smoke-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var validPath = Path.Combine(root, "tiles", "valid.png");
            Directory.CreateDirectory(Path.GetDirectoryName(validPath)!);
            using (var bmp = new System.Drawing.Bitmap(16, 16))
            {
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.Coral);
                bmp.Save(validPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            var corruptPath = Path.Combine(root, "tiles", "corrupt.png");
            File.WriteAllText(corruptPath, "not-a-png");

            AssetPreviewControl? preview = null;
            try
            {
                preview = new AssetPreviewControl { AssetRoot = root };

                preview.LogicalPath = "tiles/valid.png";
                Assert.Equal(AssetPreviewState.Loaded, preview.PreviewState);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Assert.Equal(AssetPreviewState.Loaded, preview.PreviewState);
                Assert.NotNull(preview.PreviewImageForTest);

                preview.LogicalPath = "tiles/missing.png";
                Assert.Equal(AssetPreviewState.Missing, preview.PreviewState);

                preview.LogicalPath = "tiles/corrupt.png";
                Assert.Equal(AssetPreviewState.Corrupt, preview.PreviewState);

                preview.LogicalPath = "../outside.png";
                Assert.Equal(AssetPreviewState.Rejected, preview.PreviewState);

                preview.LogicalPath = "tiles/valid.png";
                Assert.Equal(AssetPreviewState.Loaded, preview.PreviewState);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Assert.Equal(AssetPreviewState.Loaded, preview.PreviewState);

                var traversal = ProjectAssetPathResolver.TryResolve(root, "../outside.png");
                Assert.Equal(ProjectAssetPathResolver.ResolveStatus.TraversalRejected, traversal.Status);
            }
            finally
            {
                preview?.Dispose();
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // best-effort
                }
            }
        });
    }

    [Fact]
    public void AssetPreview_GameDataPanels_SaveSmokeScreenshots()
    {
        StaTestRunner.Run(() =>
        {
            GameDataSmokeTestHelper.ConfigureInMemory();
            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                EditorSmokeTestAccess.AssertShellReady(window);

                foreach (var task in new[]
                         {
                             EditorSmokeTestAccess.OpenGameDataAndSaveSampleTilesetAsync(window),
                             EditorSmokeTestAccess.OpenGameDataAndSaveSampleNpcAsync(window),
                             EditorSmokeTestAccess.OpenGameDataAndSaveSampleItemAsync(window),
                             EditorSmokeTestAccess.OpenGameDataAndSaveSampleSpellAsync(window),
                             EditorSmokeTestAccess.OpenGameDataAndSaveSampleResourceAndSpawnAsync(window),
                         })
                {
                    StaTestRunner.PumpUntil(() => task.IsCompleted, EditorSmokeTestAccess.DefaultTimeout);
                    Assert.True(task.IsCompletedSuccessfully, task.Exception?.ToString());
                }

                var screenshotDir = Path.Combine(
                    "/workspace",
                    "docs",
                    "progress",
                    "phase-06-essential-content-editors",
                    "screenshots");
                Assert.True(Directory.Exists(screenshotDir), $"Missing screenshot directory: {screenshotDir}");

                foreach (var fileName in new[]
                         {
                             "tileset-preview-smoke.png",
                             "npc-preview-smoke.png",
                             "item-preview-smoke.png",
                             "spell-preview-smoke.png",
                             "resource-preview-smoke.png",
                         })
                {
                    var path = Path.Combine(screenshotDir, fileName);
                    Assert.True(File.Exists(path), $"Missing preview screenshot: {path}");
                    Assert.True(new FileInfo(path).Length > 0, $"Empty preview screenshot: {path}");
                }
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
