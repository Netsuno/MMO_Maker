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
    private static readonly string PreviewScreenshotDirectory = Path.Combine(
        "/workspace",
        "docs",
        "progress",
        "phase-06-essential-content-editors",
        "screenshots");

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

    public static void AssertInitialTilesetCategory(GameDataForm form)
    {
        if (form.CategorySelectedIndexForTest != 0)
        {
            throw new InvalidOperationException(
                $"Expected category index 0, got {form.CategorySelectedIndexForTest}.");
        }

        if (!form.IsTilesetPanelVisibleForTest)
        {
            throw new InvalidOperationException("Tileset panel is not visible on initial open.");
        }

        var tilesets = form.TilesetsForTest;
        if (!form.HostPanelForTest.Controls.Contains(tilesets) || !tilesets.Visible)
        {
            throw new InvalidOperationException("Tileset panel is not parented in the host or not visible.");
        }
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

    public static void AssertListMissing(ListBox list, string namePart)
    {
        var labels = list.Items.Cast<object>().Select(item => item.ToString() ?? string.Empty).ToArray();
        if (labels.Any(label => label.Contains(namePart, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"List should not contain '{namePart}': {string.Join("; ", labels)}");
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

    private static void SelectComboItemContaining(ComboBox combo, string labelPart)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if ((combo.Items[i]?.ToString() ?? string.Empty).Contains(labelPart, StringComparison.Ordinal))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        throw new InvalidOperationException($"No combo item contains '{labelPart}'.");
    }

    private static void ApplySearchAndStatusFilter(
        TextBox search,
        ComboBox statusFilter,
        ListBox list,
        string searchTerm,
        int statusIndex,
        TimeSpan timeout)
    {
        SetText(search, searchTerm);
        PumpUntil(() => list.Items.Count >= 1, timeout);
        statusFilter.SelectedIndex = statusIndex;
        PumpUntil(() => list.Items.Count >= 1, timeout);
    }

    private static void RejectInvalidPublication(
        TextBox nameBox,
        Button publishButton,
        Func<bool> isDirty,
        TimeSpan timeout)
    {
        var previous = EditorTestHooks.OverrideMessageBoxResult;
        EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
        try
        {
            SetText(nameBox, string.Empty);
            PumpUntil(isDirty, timeout);
            Click(publishButton);
            PumpUntil(isDirty, timeout);
            if (!isDirty())
            {
                throw new InvalidOperationException("Session should remain dirty after rejected publication.");
            }
        }
        finally
        {
            EditorTestHooks.OverrideMessageBoxResult = previous;
        }
    }

    private static void CancelDirtyListNavigation(
        ListBox list,
        TextBox nameBox,
        Func<bool> isDirty,
        string firstRecord,
        string secondRecord,
        TimeSpan timeout)
    {
        SelectListItemContaining(list, firstRecord);
        PumpUntil(() => !isDirty(), timeout);
        var editedName = nameBox.Text + "X";
        SetText(nameBox, editedName);
        PumpUntil(isDirty, timeout);

        var stayIndex = list.SelectedIndex;
        var previous = EditorTestHooks.OverrideMessageBoxResult;
        EditorTestHooks.OverrideMessageBoxResult = DialogResult.No;
        try
        {
            SelectListItemContaining(list, secondRecord);
            PumpUntil(() => list.SelectedIndex == stayIndex, timeout);
            if (!string.Equals(nameBox.Text, editedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Dirty edits should be preserved after canceling navigation.");
            }

            if (!isDirty())
            {
                throw new InvalidOperationException("Session should remain dirty after canceling navigation.");
            }
        }
        finally
        {
            EditorTestHooks.OverrideMessageBoxResult = previous;
        }
    }

    private static void DeleteAllowedRecord(
        ListBox list,
        Button deleteButton,
        string recordName,
        TimeSpan timeout)
    {
        var previous = EditorTestHooks.OverrideMessageBoxResult;
        EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
        try
        {
            SelectListItemContaining(list, recordName);
            var countBefore = list.Items.Count;
            Click(deleteButton);
            PumpUntil(() => list.Items.Count < countBefore, timeout);
            AssertListMissing(list, recordName);
        }
        finally
        {
            EditorTestHooks.OverrideMessageBoxResult = previous;
        }
    }

    private static void AttemptProtectedDelete(
        ListBox list,
        Button deleteButton,
        string recordName,
        TimeSpan timeout)
    {
        var previous = EditorTestHooks.OverrideMessageBoxResult;
        EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
        try
        {
            SelectListItemContaining(list, recordName);
            var countBefore = list.Items.Count;
            Click(deleteButton);
            PumpUntil(() => list.Items.Count == countBefore, timeout);
            AssertListContains(list, recordName);
        }
        finally
        {
            EditorTestHooks.OverrideMessageBoxResult = previous;
        }
    }

    private static void CloseReopenAndVerify(
        MainWindow window,
        TimeSpan timeout,
        int categoryIndex,
        Action<GameDataForm> verify)
    {
        // caller closes form; this reopens
        var reopened = OpenViaMainWindowCommand(window, timeout);
        if (categoryIndex != 0)
        {
            reopened.SelectCategoryForTest(categoryIndex);
        }

        verify(reopened);
        CloseForm(reopened, timeout);
    }

    public static void SavePreviewScreenshot(AssetPreviewControl preview, string fileName)
    {
        var directory = Path.GetFullPath(PreviewScreenshotDirectory);
        preview.SavePreviewScreenshotForTest(Path.Combine(directory, fileName));
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
            SavePreviewScreenshot(panel.PreviewForTest, "tileset-preview-smoke.png");

            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeTilesetUi", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokeTilesetUi");
            Click(panel.BtnDupForTest);
            SetText(panel.NameForTest, "SmokeTilesetUiCopy");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeTilesetUiCopy", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokeTilesetUi");
            RejectInvalidPublication(panel.NameForTest, panel.BtnPublishForTest, () => panel.IsDirty, timeout);
            SetText(panel.NameForTest, "SmokeTilesetUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);

            ApplySearchAndStatusFilter(
                panel.SearchForTest,
                panel.StatusFilterForTest,
                panel.ListForTest,
                "SmokeTileset",
                2,
                timeout);

            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeTilesetDeleteUi");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            CancelDirtyListNavigation(
                panel.ListForTest,
                panel.NameForTest,
                () => panel.IsDirty,
                "SmokeTilesetUi",
                "SmokeTilesetUiCopy",
                timeout);

            DeleteAllowedRecord(panel.ListForTest, panel.BtnDeleteForTest, "SmokeTilesetDeleteUi", timeout);

            CloseForm(form, timeout);

            CloseReopenAndVerify(
                window,
                timeout,
                0,
                reopened =>
                {
                    var reopenedPanel = reopened.TilesetsForTest;
                    PumpUntil(() => reopenedPanel.ListForTest.Items.Count >= 1, timeout);
                    SelectListItemContaining(reopenedPanel.ListForTest, "SmokeTilesetUi");
                    PumpUntil(() => !reopenedPanel.IsDirty, timeout);
                    if (!string.Equals(reopenedPanel.NameForTest.Text, "SmokeTilesetUi", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Reopened tileset name mismatch.");
                    }
                });
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
            SavePreviewScreenshot(panel.PreviewForTest, "npc-preview-smoke.png");

            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeMonsterUi", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokeMonsterUi");
            Click(panel.BtnDupForTest);
            SetText(panel.NameForTest, "SmokeMonsterUiCopy");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeMonsterUiCopy", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokeMonsterUi");
            RejectInvalidPublication(panel.NameForTest, panel.BtnPublishForTest, () => panel.IsDirty, timeout);
            SetText(panel.NameForTest, "SmokeMonsterUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);

            ApplySearchAndStatusFilter(
                panel.SearchForTest,
                panel.StatusFilterForTest,
                panel.ListForTest,
                "SmokeMonster",
                2,
                timeout);

            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeMonsterDeleteUi");
            SetText(panel.SpritePathForTest, "sprites/npcs/smoke-ui.png");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            CancelDirtyListNavigation(
                panel.ListForTest,
                panel.NameForTest,
                () => panel.IsDirty,
                "SmokeMonsterUi",
                "SmokeMonsterUiCopy",
                timeout);

            DeleteAllowedRecord(panel.ListForTest, panel.BtnDeleteForTest, "SmokeMonsterDeleteUi", timeout);

            CloseForm(form, timeout);

            CloseReopenAndVerify(
                window,
                timeout,
                1,
                reopened =>
                {
                    var reopenedPanel = reopened.NpcsForTest;
                    PumpUntil(() => reopenedPanel.ListForTest.Items.Count >= 1, timeout);
                    SelectListItemContaining(reopenedPanel.ListForTest, "SmokeMonsterUi");
                    PumpUntil(() => !reopenedPanel.IsDirty, timeout);
                });
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
            SavePreviewScreenshot(panel.PreviewForTest, "item-preview-smoke.png");

            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokePotionUi", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokePotionUi");
            Click(panel.BtnDupForTest);
            SetText(panel.NameForTest, "SmokePotionUiCopy");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            SelectListItemContaining(panel.ListForTest, "SmokePotionUi");
            RejectInvalidPublication(panel.NameForTest, panel.BtnPublishForTest, () => panel.IsDirty, timeout);
            SetText(panel.NameForTest, "SmokePotionUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);

            ApplySearchAndStatusFilter(
                panel.SearchForTest,
                panel.StatusFilterForTest,
                panel.ListForTest,
                "SmokePotion",
                2,
                timeout);

            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokePotionDeleteUi");
            SetText(panel.IconPathForTest, "icons/items/smoke-ui.png");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            CancelDirtyListNavigation(
                panel.ListForTest,
                panel.NameForTest,
                () => panel.IsDirty,
                "SmokePotionUi",
                "SmokePotionUiCopy",
                timeout);

            DeleteAllowedRecord(panel.ListForTest, panel.BtnDeleteForTest, "SmokePotionDeleteUi", timeout);

            CloseForm(form, timeout);

            CloseReopenAndVerify(
                window,
                timeout,
                2,
                reopened =>
                {
                    var reopenedPanel = reopened.ItemsForTest;
                    PumpUntil(() => reopenedPanel.ListForTest.Items.Count >= 1, timeout);
                    SelectListItemContaining(reopenedPanel.ListForTest, "SmokePotionUi");
                });
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
            SavePreviewScreenshot(panel.PreviewForTest, "spell-preview-smoke.png");

            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeFireballUi", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokeFireballUi");
            Click(panel.BtnDupForTest);
            SetText(panel.NameForTest, "SmokeFireballUiCopy");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            SelectListItemContaining(panel.ListForTest, "SmokeFireballUi");
            RejectInvalidPublication(panel.NameForTest, panel.BtnPublishForTest, () => panel.IsDirty, timeout);
            SetText(panel.NameForTest, "SmokeFireballUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);

            ApplySearchAndStatusFilter(
                panel.SearchForTest,
                panel.StatusFilterForTest,
                panel.ListForTest,
                "SmokeFireball",
                2,
                timeout);

            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeFireballDeleteUi");
            SetText(panel.IconPathForTest, "icons/spells/smoke-ui.png");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            CancelDirtyListNavigation(
                panel.ListForTest,
                panel.NameForTest,
                () => panel.IsDirty,
                "SmokeFireballUi",
                "SmokeFireballUiCopy",
                timeout);

            DeleteAllowedRecord(panel.ListForTest, panel.BtnDeleteForTest, "SmokeFireballDeleteUi", timeout);

            CloseForm(form, timeout);

            CloseReopenAndVerify(
                window,
                timeout,
                3,
                reopened =>
                {
                    var reopenedPanel = reopened.SpellsForTest;
                    PumpUntil(() => reopenedPanel.ListForTest.Items.Count >= 1, timeout);
                    SelectListItemContaining(reopenedPanel.ListForTest, "SmokeFireballUi");
                });
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
            SelectComboItemContaining(panel.StartingSpellForTest, "SmokeClassStarterUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeWarriorUi", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokeWarriorUi");
            Click(panel.BtnDupForTest);
            SetText(panel.NameForTest, "SmokeWarriorUiCopy");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            SelectListItemContaining(panel.ListForTest, "SmokeWarriorUi");
            RejectInvalidPublication(panel.NameForTest, panel.BtnPublishForTest, () => panel.IsDirty, timeout);
            SetText(panel.NameForTest, "SmokeWarriorUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);

            ApplySearchAndStatusFilter(
                panel.SearchForTest,
                panel.StatusFilterForTest,
                panel.ListForTest,
                "SmokeWarrior",
                2,
                timeout);

            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeWarriorDeleteUi");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            CancelDirtyListNavigation(
                panel.ListForTest,
                panel.NameForTest,
                () => panel.IsDirty,
                "SmokeWarriorUi",
                "SmokeWarriorUiCopy",
                timeout);

            DeleteAllowedRecord(panel.ListForTest, panel.BtnDeleteForTest, "SmokeWarriorDeleteUi", timeout);

            form.SelectCategoryForTest(3);
            AttemptProtectedDelete(
                form.SpellsForTest.ListForTest,
                form.SpellsForTest.BtnDeleteForTest,
                "SmokeClassStarterUi",
                timeout);

            CloseForm(form, timeout);

            CloseReopenAndVerify(
                window,
                timeout,
                4,
                reopened =>
                {
                    var reopenedPanel = reopened.ClassesForTest;
                    PumpUntil(() => reopenedPanel.ListForTest.Items.Count >= 1, timeout);
                    SelectListItemContaining(reopenedPanel.ListForTest, "SmokeWarriorUi");
                });
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
            if (panel.ListingsForTest.Rows.Count == 0)
            {
                Click(panel.BtnAddListingForTest);
            }

            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);
            AssertListContains(panel.ListForTest, "SmokeShopUi", "Published");

            SelectListItemContaining(panel.ListForTest, "SmokeShopUi");
            Click(panel.BtnDupForTest);
            SetText(panel.NameForTest, "SmokeShopUiCopy");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            SelectListItemContaining(panel.ListForTest, "SmokeShopUi");
            RejectInvalidPublication(panel.NameForTest, panel.BtnPublishForTest, () => panel.IsDirty, timeout);
            SetText(panel.NameForTest, "SmokeShopUi");
            ClickAndWait(panel.BtnSaveForTest, () => !panel.IsDirty, timeout);

            ApplySearchAndStatusFilter(
                panel.SearchForTest,
                panel.StatusFilterForTest,
                panel.ListForTest,
                "SmokeShop",
                2,
                timeout);

            Click(panel.BtnNewForTest);
            SetText(panel.NameForTest, "SmokeShopDeleteUi");
            ClickAndWait(panel.BtnPublishForTest, () => !panel.IsDirty, timeout);

            CancelDirtyListNavigation(
                panel.ListForTest,
                panel.NameForTest,
                () => panel.IsDirty,
                "SmokeShopUi",
                "SmokeShopUiCopy",
                timeout);

            DeleteAllowedRecord(panel.ListForTest, panel.BtnDeleteForTest, "SmokeShopDeleteUi", timeout);

            form.SelectCategoryForTest(2);
            AttemptProtectedDelete(
                form.ItemsForTest.ListForTest,
                form.ItemsForTest.BtnDeleteForTest,
                "SmokeShopPotionUi",
                timeout);

            CloseForm(form, timeout);

            CloseReopenAndVerify(
                window,
                timeout,
                5,
                reopened =>
                {
                    WaitForTask(reopened.ShopsForTest.InitializeAsync(), timeout);
                    var reopenedPanel = reopened.ShopsForTest;
                    PumpUntil(() => reopenedPanel.ListForTest.Items.Count >= 1, timeout);
                    SelectListItemContaining(reopenedPanel.ListForTest, "SmokeShopUi");
                });
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
            SelectComboItemContaining(resources.YieldItemForTest, "SmokeYieldUi");
            PumpUntil(() => resources.PreviewForTest.PreviewState == AssetPreviewState.Loaded, timeout);
            SavePreviewScreenshot(resources.PreviewForTest, "resource-preview-smoke.png");
            ClickAndWait(resources.BtnPublishForTest, () => !resources.IsDirty, timeout);
            AssertListContains(resources.ListForTest, "SmokeTreeUi", "Published");

            SelectListItemContaining(resources.ListForTest, "SmokeTreeUi");
            Click(resources.BtnDupForTest);
            SetText(resources.NameForTest, "SmokeTreeUiCopy");
            ClickAndWait(resources.BtnPublishForTest, () => !resources.IsDirty, timeout);

            SelectListItemContaining(resources.ListForTest, "SmokeTreeUi");
            RejectInvalidPublication(resources.NameForTest, resources.BtnPublishForTest, () => resources.IsDirty, timeout);
            SetText(resources.NameForTest, "SmokeTreeUi");
            ClickAndWait(resources.BtnSaveForTest, () => !resources.IsDirty, timeout);

            ApplySearchAndStatusFilter(
                resources.SearchForTest,
                resources.StatusFilterForTest,
                resources.ListForTest,
                "SmokeTree",
                2,
                timeout);

            Click(resources.BtnNewForTest);
            SetText(resources.NameForTest, "SmokeTreeDeleteUi");
            SetText(resources.SpritePathForTest, "sprites/resources/smoke-ui.png");
            SelectComboItemContaining(resources.YieldItemForTest, "SmokeYieldUi");
            ClickAndWait(resources.BtnPublishForTest, () => !resources.IsDirty, timeout);

            CancelDirtyListNavigation(
                resources.ListForTest,
                resources.NameForTest,
                () => resources.IsDirty,
                "SmokeTreeUi",
                "SmokeTreeUiCopy",
                timeout);

            DeleteAllowedRecord(resources.ListForTest, resources.BtnDeleteForTest, "SmokeTreeDeleteUi", timeout);

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

            AttemptProtectedDelete(
                resources.ListForTest,
                resources.BtnDeleteForTest,
                "SmokeTreeUi",
                timeout);

            CloseForm(form, timeout);

            CloseReopenAndVerify(
                window,
                timeout,
                6,
                reopened =>
                {
                    WaitForTask(reopened.ResourcesForTest.InitializeAsync(), timeout);
                    var reopenedResources = reopened.ResourcesForTest.ResourcesPanelForTest;
                    PumpUntil(() => reopenedResources.ListForTest.Items.Count >= 1, timeout);
                    SelectListItemContaining(reopenedResources.ListForTest, "SmokeTreeUi");
                });
        }
        finally
        {
            CleanupAssetRoot(assetRoot);
        }
    }

    public static void RunSpawnFilterScenario(MainWindow window, TimeSpan timeout)
    {
        var assetRoot = CreateSmokeAssetRoot("sprites/resources/smoke-filter-ui.png", "icons/items/smoke-filter-yield-ui.png");
        try
        {
            var form = OpenViaMainWindowCommand(window, timeout);
            form.SelectCategoryForTest(2);
            var items = form.ItemsForTest;
            Click(items.BtnNewForTest);
            SetText(items.NameForTest, "SmokeFilterYieldUi");
            SetText(items.IconPathForTest, "icons/items/smoke-filter-yield-ui.png");
            ClickAndWait(items.BtnPublishForTest, () => !items.IsDirty, timeout);

            form.SelectCategoryForTest(6);
            WaitForTask(form.ResourcesForTest.InitializeAsync(), timeout);
            var resources = form.ResourcesForTest.ResourcesPanelForTest;
            Click(resources.BtnNewForTest);
            SetText(resources.NameForTest, "SmokeFilterTreeUi");
            SetText(resources.SpritePathForTest, "sprites/resources/smoke-filter-ui.png");
            SelectComboItemContaining(resources.YieldItemForTest, "SmokeFilterYieldUi");
            ClickAndWait(resources.BtnPublishForTest, () => !resources.IsDirty, timeout);

            form.ResourcesForTest.TabsForTest.SelectedIndex = 1;
            var spawns = form.ResourcesForTest.SpawnsPanelForTest;
            PumpUntil(() => spawns.MapFilterForTest.Items.Count > 1, timeout);
            PumpUntil(() => spawns.ResourceFilterForTest.Items.Count > 1, timeout);

            Click(spawns.BtnNewForTest);
            ClickAndWait(spawns.BtnPublishForTest, () => !spawns.IsDirty, timeout);
            var totalSpawns = spawns.ListForTest.Items.Count;
            if (totalSpawns < 1)
            {
                throw new InvalidOperationException("Expected at least one spawn after publish.");
            }

            spawns.MapFilterForTest.SelectedIndex = 1;
            PumpUntil(() => spawns.ListForTest.Items.Count >= 1, timeout);
            var filteredByMap = spawns.ListForTest.Items.Count;

            if (spawns.ResourceFilterForTest.Items.Count > 1)
            {
                spawns.ResourceFilterForTest.SelectedIndex = 1;
                PumpUntil(() => spawns.ListForTest.Items.Count >= 1, timeout);
            }

            var filteredByBoth = spawns.ListForTest.Items.Count;

            spawns.MapFilterForTest.SelectedIndex = 0;
            spawns.ResourceFilterForTest.SelectedIndex = 0;
            PumpUntil(() => spawns.ListForTest.Items.Count == totalSpawns, timeout);

            if (filteredByMap > totalSpawns || filteredByBoth > totalSpawns)
            {
                throw new InvalidOperationException(
                    $"Filter counts invalid: total={totalSpawns}, map={filteredByMap}, both={filteredByBoth}.");
            }

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

        CancelDirtyListNavigation(
            panel.ListForTest,
            panel.NameForTest,
            () => panel.IsDirty,
            "SmokeDirtyA",
            "SmokeDirtyB",
            timeout);

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
