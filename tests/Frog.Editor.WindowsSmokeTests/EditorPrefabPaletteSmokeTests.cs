using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Frog.Editor;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Config;
using Frog.Editor.Enums;
using Frog.Editor.Panels;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorPrefabPaletteSmokeTests
{
    [Fact]
    public void Palette_FiltersByNameOrId_AndShowsPlaceModeCopy()
    {
        StaTestRunner.Run(() =>
        {
            var empty = Path.Combine(Path.GetTempPath(), $"frog-prefab-empty-ui-{Guid.NewGuid():N}");
            Directory.CreateDirectory(empty);
            PrefabSpriteCache.ResetCacheForTest();
            PrefabSpriteCache.OverrideDirectoryForTest = empty;
            try
            {
            var panel = new EditorLeftToolsWpf();
            Assert.Equal("Canapé", panel.SelectedNameForTest);
            Assert.Equal(8, panel.VisiblePrefabCountForTest);
            Assert.False(panel.HasPrefabPreviewForTest);
            Assert.Equal("P pour placer", panel.StatusTextForTest);
            Assert.False(panel.CanDuplicateForTest);

            panel.SetFilterForTest("canape");
            Assert.Equal(1, panel.VisiblePrefabCountForTest);
            Assert.Equal("Canapé", panel.SelectedNameForTest);

            panel.SetFilterForTest("chest");
            Assert.Equal(1, panel.VisiblePrefabCountForTest);
            Assert.Contains("sofa", panel.SelectedMetaForTest, StringComparison.Ordinal);

            panel.SetFilterForTest("aucun-id");
            Assert.Equal(0, panel.VisiblePrefabCountForTest);

            panel.SetPrefabPlaceMode(true);
            Assert.True(panel.PlaceModeForTest);
            Assert.Equal("Clic pour placer — Échap pour annuler", panel.StatusTextForTest);

            panel.SetCanDuplicateLastPrefab(true);
            Assert.True(panel.CanDuplicateForTest);
            panel.SetPrefabActionMessage("Copie posée en (4, 4).");
            Assert.Contains("Copie posée en (4, 4).", panel.StatusTextForTest, StringComparison.Ordinal);
            Assert.Contains("Clic pour placer — Échap pour annuler", panel.StatusTextForTest, StringComparison.Ordinal);

            panel.SetPrefabPlaceMode(false);
            Assert.Equal("P pour placer", panel.StatusTextForTest);
            }
            finally
            {
                PrefabSpriteCache.ResetCacheForTest();
                PrefabSpriteCache.OverrideDirectoryForTest = null;
                TryDelete(empty);
            }
        });
    }

    [Fact]
    public void Palette_ShowsThumbnailOnlyWhenSpriteIsCached()
    {
        StaTestRunner.Run(() =>
        {
            var withSprite = Path.Combine(Path.GetTempPath(), $"frog-prefab-png-{Guid.NewGuid():N}");
            var empty = Path.Combine(Path.GetTempPath(), $"frog-prefab-empty-{Guid.NewGuid():N}");
            Directory.CreateDirectory(withSprite);
            Directory.CreateDirectory(empty);
            PrefabSpriteCache.ResetCacheForTest();
            try
            {
                using (var bitmap = new Bitmap(8, 8))
                {
                    bitmap.SetPixel(1, 1, Color.OrangeRed);
                    bitmap.Save(Path.Combine(withSprite, "sofa-south.png"), ImageFormat.Png);
                }

                PrefabSpriteCache.OverrideDirectoryForTest = withSprite;
                var panel = new EditorLeftToolsWpf();
                panel.BindPrefabCatalog(BuiltInPrefabCatalog.Create(), BuiltInPrefabCatalog.SofaId, PrefabFacing.South);
                Assert.True(panel.HasPrefabPreviewForTest);

                PrefabSpriteCache.ResetCacheForTest();
                PrefabSpriteCache.OverrideDirectoryForTest = empty;
                panel.BindPrefabCatalog(BuiltInPrefabCatalog.Create(), BuiltInPrefabCatalog.SofaId, PrefabFacing.South);
                Assert.False(panel.HasPrefabPreviewForTest);
                Assert.Equal("Canapé", panel.SelectedNameForTest);
            }
            finally
            {
                PrefabSpriteCache.ResetCacheForTest();
                PrefabSpriteCache.OverrideDirectoryForTest = null;
                TryDelete(withSprite);
                TryDelete(empty);
            }
        });
    }

    [Fact]
    public void Escape_CancelsPlaceMode_AndLastPrefabRestoresAcrossSessions()
    {
        StaTestRunner.Run(() =>
        {
            var temp = Path.Combine(Path.GetTempPath(), $"frog-prefab-palette-{Guid.NewGuid():N}.json");
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            EditorLocalWorkstate.OverrideFilePathForTest = temp;
            MainWindow? window = null;
            var closed = false;
            try
            {
                EditorLocalWorkstate.WriteLastPrefabSelection(BuiltInPrefabCatalog.PlantId, PrefabFacing.North);
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                window.Closed += (_, _) => closed = true;
                StaTestRunner.PumpUntil(
                    () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                    EditorSmokeTestAccess.DefaultTimeout);
                if (window.EditorForm.WorkspaceInitializationTask.IsFaulted)
                {
                    throw window.EditorForm.WorkspaceInitializationTask.Exception?.GetBaseException()
                          ?? new InvalidOperationException("Workspace initialization failed.");
                }

                var form = window.EditorForm;
                Assert.Equal(BuiltInPrefabCatalog.PlantId, form.GetCanvasForTest().SelectedPrefabId);
                Assert.Equal(PrefabFacing.North, form.GetCanvasForTest().SelectedPrefabFacing);
                Assert.Equal("Plante", form.PrefabSelectedNameForTest());

                form.SelectEditorToolForTest(EditorTool.Spawn);
                form.SelectEditorToolForTest(EditorTool.Prefab);
                Assert.Equal(EditorTool.Prefab, form.GetActiveToolForTest());
                Assert.Equal("Clic pour placer — Échap pour annuler", form.PrefabStatusForTest());

                Assert.True(form.TryProcessCmdKeyForTest(Keys.Escape));
                Assert.Equal(EditorTool.Spawn, form.GetActiveToolForTest());
                Assert.Equal("P pour placer", form.PrefabStatusForTest());
            }
            finally
            {
                if (window is not null && !closed)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                    StaTestRunner.PumpUntil(() => closed || !window.IsVisible, TimeSpan.FromSeconds(5));
                }

                EditorSmokeTestAccess.ResetHooks();
                EditorLocalWorkstate.OverrideFilePathForTest = null;
                TryDeleteFile(temp);
            }
        });
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
