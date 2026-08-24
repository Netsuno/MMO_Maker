using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Forms.GameData;
using Frog.Editor.Services;

namespace Frog.Editor;

/// <summary>Automatisation UI réelle pour les smokes Données de jeu (boutons/contrôles WinForms).</summary>
internal static class GameDataSmokeUiDriver
{
    public static GameDataForm OpenViaMainWindowCommand(MainWindow window, TimeSpan timeout)
    {
        EditorTestHooks.GameDataNonModalForTest = true;
        GameDataForm? form = null;
        EditorTestHooks.OnGameDataFormShown = opened => form = (GameDataForm)opened;

        if (window.Dispatcher.CheckAccess())
        {
            MainWindow.CmdGameData.Execute(null, window);
        }
        else
        {
            window.Dispatcher.Invoke(() => MainWindow.CmdGameData.Execute(null, window));
        }
        PumpUntil(() => form is not null && form.IsInitializedForTest, timeout);
        return form ?? throw new InvalidOperationException("Game Data form did not open.");
    }

    public static void CloseForm(GameDataForm form, TimeSpan timeout)
    {
        EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
        try
        {
            form.Close();
            PumpUntil(() => form.IsDisposed, timeout);
        }
        finally
        {
            EditorTestHooks.OverrideMessageBoxResult = null;
            EditorTestHooks.GameDataNonModalForTest = false;
            EditorTestHooks.OnGameDataFormShown = null;
        }
    }

    public static string CreateSmokeAssetRoot(params string[] relativePaths)
    {
        if (relativePaths.Length == 0)
        {
            relativePaths = ["preview.png"];
        }

        var root = Path.Combine(Path.GetTempPath(), $"frog-smoke-assets-{Guid.NewGuid():N}");
        foreach (var relativePath in relativePaths)
        {
            var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            using var bitmap = new Bitmap(32, 32);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.SteelBlue);
            bitmap.Save(full, ImageFormat.Png);
        }

        EditorTestHooks.OverrideProjectAssetRoot = root;
        return root;
    }

    public static void CleanupAssetRoot(string? root)
    {
        EditorTestHooks.OverrideProjectAssetRoot = null;
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    public static void Click(Button button) => button.PerformClick();

    public static void SetText(TextBox box, string value) => box.Text = value;

    public static void ClickAndWait(Button button, Func<bool> done, TimeSpan timeout)
    {
        Click(button);
        PumpUntil(done, timeout);
    }

    private static void PumpUntil(Func<bool> predicate, TimeSpan timeout)
        => EditorSmokeTestAccess.PumpUntilForTest(predicate, timeout);

    internal static void PumpUntilFallback(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        if (!predicate())
        {
            throw new TimeoutException("UI condition not met before timeout.");
        }
    }

    private static void WaitForTask(Task task, TimeSpan timeout)
    {
        PumpUntil(() => task.IsCompleted, timeout);
        if (task.IsFaulted)
        {
            throw task.Exception?.GetBaseException()
                  ?? new InvalidOperationException("Background UI task failed.");
        }
    }

    public static void AssertListContains(ListBox list, string namePart, string? statusPart = null)
    {
        var labels = list.Items.Cast<object>().Select(item => item.ToString() ?? string.Empty).ToArray();
        if (labels.All(label =>
                !label.Contains(namePart, StringComparison.Ordinal)
                || (statusPart is not null && !label.Contains(statusPart, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                $"List missing '{namePart}'{(statusPart is null ? string.Empty : $" [{statusPart}]")}: {string.Join("; ", labels)}");
        }
    }

    public static void SelectListItemContaining(ListBox list, string namePart)
    {
        for (var i = 0; i < list.Items.Count; i++)
        {
            if ((list.Items[i]?.ToString() ?? string.Empty).Contains(namePart, StringComparison.Ordinal))
            {
                list.SelectedIndex = i;
                return;
            }
        }

        throw new InvalidOperationException($"No list item contains '{namePart}'.");
    }

    public static void RunTilesetScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("tiles/smoke-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            var panel = form.TilesetsForTest;
            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeTilesetUi");
            SetText(panel.PathForTest, "tiles/smoke-ui.png");
            PumpUntil(() => panel.PreviewForTest.PreviewState == AssetPreviewState.Loaded, timeout);

            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeTilesetUi", "Published");

            SetText(panel.SearchForTest, "SmokeTileset");
            PumpUntil(() => panel.ListForTest.Items.Count >= 1, timeout);
            panel.StatusFilterForTest.SelectedIndex = 2;
            PumpUntil(() => panel.ListForTest.Items.Count >= 1, timeout);

            CloseForm(form, timeout);

            var reopened = OpenViaMainWindowCommand(window, timeout);
            var reopenedPanel = reopened.TilesetsForTest;
            PumpUntil(() => reopenedPanel.ListForTest.Items.Count >= 1, timeout);
            SelectListItemContaining(reopenedPanel.ListForTest, "SmokeTilesetUi");
            PumpUntil(() => !reopenedPanel.IsDirty, timeout);
            if (!string.Equals(reopenedPanel.NameForTest.Text, "SmokeTilesetUi", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Reopened tileset name mismatch.");
            }

            CloseForm(reopened, timeout);
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunNpcScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("sprites/npcs/smoke-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            form.SelectCategoryForTest(1);
            var panel = form.NpcsForTest;
            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeMonsterUi");
            SetText(panel.SpritePathForTest, "sprites/npcs/smoke-ui.png");
            PumpUntil(() => panel.PreviewForTest.PreviewState == AssetPreviewState.Loaded, timeout);
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeMonsterUi", "Published");
            CloseForm(form, timeout);
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunItemScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("icons/items/smoke-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            form.SelectCategoryForTest(2);
            var panel = form.ItemsForTest;
            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokePotionUi");
            SetText(panel.IconPathForTest, "icons/items/smoke-ui.png");
            PumpUntil(() => panel.PreviewForTest.PreviewState == AssetPreviewState.Loaded, timeout);
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokePotionUi", "Published");
            CloseForm(form, timeout);
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunSpellScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("icons/spells/smoke-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            form.SelectCategoryForTest(3);
            var panel = form.SpellsForTest;
            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeFireballUi");
            SetText(panel.IconPathForTest, "icons/spells/smoke-ui.png");
            PumpUntil(() => panel.PreviewForTest.PreviewState == AssetPreviewState.Loaded, timeout);
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeFireballUi", "Published");
            CloseForm(form, timeout);
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunClassScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("icons/spells/smoke-class-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            form.SelectCategoryForTest(3);
            var spells = form.SpellsForTest;
            Click(spells.BtnNewForTest);
            SetText(spells.NameForTest, "SmokeClassStarterUi");
            SetText(spells.IconPathForTest, "icons/spells/smoke-class-ui.png");
            ClickAndWait(spells.BtnPublishForTest, () => !spells.IsDirty, timeout);

            form.SelectCategoryForTest(4);
            WaitForTask(form.ClassesForTest.InitializeAsync(), timeout);
            var panel = form.ClassesForTest;
            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeWarriorUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeWarriorUi", "Published");
            CloseForm(form, timeout);
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunShopScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("icons/items/smoke-shop-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            form.SelectCategoryForTest(2);
            var items = form.ItemsForTest;
            Click(items.BtnNewForTest);
            SetText(items.NameForTest, "SmokeShopPotionUi");
            SetText(items.IconPathForTest, "icons/items/smoke-shop-ui.png");
            ClickAndWait(items.BtnPublishForTest, () => !items.IsDirty, timeout);

            form.SelectCategoryForTest(5);
            WaitForTask(form.ShopsForTest.InitializeAsync(), timeout);
            var panel = form.ShopsForTest;
            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeShopUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeShopUi", "Published");
            CloseForm(form, timeout);
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunResourceAndSpawnScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("sprites/resources/smoke-ui.png", "icons/items/smoke-yield-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            form.SelectCategoryForTest(2);
            var items = form.ItemsForTest;
            Click(items.BtnNewForTest);
            SetText(items.NameForTest, "SmokeYieldUi");
            SetText(items.IconPathForTest, "icons/items/smoke-yield-ui.png");
            ClickAndWait(items.BtnPublishForTest, () => !items.IsDirty, timeout);

            form.SelectCategoryForTest(6);
            WaitForTask(form.ResourcesForTest.InitializeAsync(), timeout);
            var resources = form.ResourcesForTest.ResourcesPanelForTest;
            Click(resources.BtnNewForTest);
            SetText(resources.NameForTest, "SmokeTreeUi");
            SetText(resources.SpritePathForTest, "sprites/resources/smoke-ui.png");
            PumpUntil(() => resources.PreviewForTest.PreviewState == AssetPreviewState.Loaded, timeout);
            ClickAndWait(resources.BtnPublishForTest, () => !resources.IsDirty, timeout);

            form.ResourcesForTest.TabsForTest.SelectedIndex = 1;
            PumpUntil(() => form.ResourcesForTest.SpawnsPanelForTest.ListForTest.IsHandleCreated, timeout);
            var spawns = form.ResourcesForTest.SpawnsPanelForTest;
            PumpUntil(() => spawns.MapFilterForTest.Items.Count > 1, timeout);
            if (spawns.MapFilterForTest.Items.Count > 1)
            {
                spawns.MapFilterForTest.SelectedIndex = 1;
            }

            if (spawns.ResourceFilterForTest.Items.Count > 1)
            {
                spawns.ResourceFilterForTest.SelectedIndex = 1;
            }

            Click(spawns.BtnNewForTest);
            ClickAndWait(spawns.BtnSaveForTest, () => !spawns.IsDirty, timeout);
            ClickAndWait(spawns.BtnPublishForTest, () => !spawns.IsDirty, timeout);
            PumpUntil(() => spawns.ListForTest.Items.Count >= 1, timeout);
            CloseForm(form, timeout);
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunDirtyStateRegression(MainWindow window, TimeSpan timeout)
    {
        var form = OpenViaMainWindowCommand(window, timeout);
        var panel = form.TilesetsForTest;

        static void CreatePublish(TilesetEditorPanel p, string suffix, TimeSpan t)
        {
            Click(p.BtnNewForTest);
            SetText(p.NameForTest, $"SmokeDirty{suffix}");
            ClickAndWait(p.BtnSaveForTest, () => !p.IsDirty, t);
            ClickAndWait(p.BtnPublishForTest, () => !p.IsDirty, t);
        }

        CreatePublish(panel, "A", timeout);
        CreatePublish(panel, "B", timeout);
        PumpUntil(() => panel.ListForTest.Items.Count >= 2, timeout);

        SelectListItemContaining(panel.ListForTest, "SmokeDirtyA");
        PumpUntil(() => !panel.IsDirty, timeout);

        var beforeName = panel.NameForTest.Text;
        var editedName = beforeName + "X";
        SetText(panel.NameForTest, editedName);
        PumpUntil(() => panel.IsDirty, timeout);

        var stayIndex = panel.ListForTest.SelectedIndex;
        EditorTestHooks.OverrideMessageBoxResult = DialogResult.No;
        try
        {
            panel.ListForTest.SelectedIndex = stayIndex == 0 ? 1 : 0;
            PumpUntil(() => panel.ListForTest.SelectedIndex == stayIndex, timeout);
            if (!string.Equals(panel.NameForTest.Text, editedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Dirty edits should be preserved after canceling navigation.");
            }

            if (!panel.IsDirty)
            {
                throw new InvalidOperationException("Session should remain dirty after canceling navigation.");
            }
        }
        finally
        {
            EditorTestHooks.OverrideMessageBoxResult = null;
        }

        CloseForm(form, timeout);
    }

    public static void RunInitializationOpenCloseLeak(MainWindow window, TimeSpan timeout)
    {
        for (var i = 0; i < 3; i++)
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            if (form.RepositorySetForTest?.DatabaseScope is { IsDisposed: true })
            {
                throw new InvalidOperationException("Database scope disposed while form open.");
            }

            CloseForm(form, timeout);
        }
    }
}
