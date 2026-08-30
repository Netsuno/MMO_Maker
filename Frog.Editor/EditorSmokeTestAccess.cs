using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Services;

namespace Frog.Editor;

/// <summary>API interne pour le smoke test Windows (assembly test via InternalsVisibleTo).</summary>
internal static class EditorSmokeTestAccess
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);

    private static Action<Func<bool>, TimeSpan>? _pumpUntil;

    private static bool _wpfThemeLoaded;

    internal static string FindRepositoryRootForTest()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Frog.Creator.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }

    public static void SetPumpUntilForTest(Action<Func<bool>, TimeSpan> pumpUntil)
    {
        _pumpUntil = pumpUntil;
    }

    internal static void PumpUntilForTest(Func<bool> predicate, TimeSpan timeout)
    {
        if (_pumpUntil is { } pump)
        {
            pump(predicate, timeout);
            return;
        }

        GameDataSmokeUiDriver.PumpUntilFallback(predicate, timeout);
    }

    public static void ResetHooks()
    {
        EditorTestHooks.OverrideMapRepository = null;
        EditorTestHooks.OverrideTilesetRepository = null;
        EditorTestHooks.OverrideNpcRepository = null;
        EditorTestHooks.OverrideItemRepository = null;
        EditorTestHooks.OverrideSpellRepository = null;
        EditorTestHooks.OverrideClassRepository = null;
        EditorTestHooks.OverrideShopRepository = null;
        EditorTestHooks.OverrideResourceRepository = null;
        EditorTestHooks.OverrideResourceSpawnRepository = null;
        EditorTestHooks.OverrideMapEventService = null;
        EditorTestHooks.OverridePhase8ContentService = null;
        EditorTestHooks.OverrideDialogService = null;
        EditorTestHooks.OverridePlaytestProcessLauncher = null;
        EditorTestHooks.OverrideServerExePath = null;
        EditorTestHooks.OverrideClientExePath = null;
        EditorTestHooks.OverrideSpawnTile = null;
        EditorTestHooks.AllowNonDurablePlaytest = false;
        EditorTestHooks.SkipMariaDbOnStartup = true;
        EditorTestHooks.GameDataNonModalForTest = false;
        EditorTestHooks.OnGameDataFormShown = null;
        EditorTestHooks.OverrideProjectAssetRoot = null;
        EditorTestHooks.OverrideMessageBoxResult = null;
        EditorTestHooks.UseSynchronousGameDataInitForTest = false;
        EditorTestHooks.PanelOperationBarrierForTest = null;
        EditorTestHooks.OnPanelLifecycleExceptionForTest = null;
        EditorTestHooks.OverridePostgreSqlConnectionString = null;
        EditorTestHooks.OverridePostgreSqlMigrateForTest = null;
        EditorTestHooks.GameDataCloseCleanupTimeoutForTest = null;
        EditorTestHooks.GameDataInitBarrierForTest = null;
        EditorTestHooks.MainWorkspaceInitBarrierForTest = null;
        _pumpUntil = null;
        TilesetCache.Clear();
        Environment.SetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory, "1");
    }

    public static void ConfigureInMemoryRepository()
    {
        ResetHooks();
        var mapRepository = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        EditorTestHooks.OverrideMapRepository = mapRepository;
        EditorTestHooks.OverrideTilesetRepository =
            new Frog.Application.Content.InMemoryTilesetRepository(
                Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        EditorTestHooks.OverrideNpcRepository =
            new Frog.Application.Content.InMemoryNpcRepository(
                Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        var itemRepository = new Frog.Application.Content.InMemoryItemRepository(
            Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        EditorTestHooks.OverrideItemRepository = itemRepository;
        var spellRepository = new Frog.Application.Content.InMemorySpellRepository(
            Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        EditorTestHooks.OverrideSpellRepository = spellRepository;
        EditorTestHooks.OverrideClassRepository =
            new Frog.Application.Content.InMemoryClassRepository(
                spellRepository,
                Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        EditorTestHooks.OverrideShopRepository =
            new Frog.Application.Content.InMemoryShopRepository(
                itemRepository,
                Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        var resourceRepository = new Frog.Application.Content.InMemoryResourceRepository(
            itemRepository,
            Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        EditorTestHooks.OverrideResourceRepository = resourceRepository;
        EditorTestHooks.OverrideResourceSpawnRepository =
            new Frog.Application.Content.InMemoryResourceSpawnRepository(
                mapRepository,
                resourceRepository,
                Frog.Application.Content.ContentRepositoryCapabilities.InMemoryTest);
        EditorTestHooks.OverrideDialogService = new SilentEditorDialogService();
        EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
        EditorTestHooks.UseSynchronousGameDataInitForTest = true;
    }

    public static Task OpenGameDataAndSaveSampleTilesetAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunTilesetScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static Task OpenGameDataAndSaveSampleNpcAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunNpcScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static Task OpenGameDataAndSaveSampleItemAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunItemScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static Task OpenGameDataAndSaveSampleSpellAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunSpellScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static Task OpenGameDataAndSaveSampleClassAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunClassScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static Task OpenGameDataAndSaveSampleShopAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunShopScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static Task OpenGameDataAndSaveSampleResourceAndSpawnAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunResourceAndSpawnScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static void OpenGameDataDirtyStateRegression(MainWindow window) =>
        GameDataSmokeUiDriver.RunDirtyStateRegression(window, DefaultTimeout);

    public static void OpenGameDataRepeatedOpenClose(MainWindow window) =>
        GameDataSmokeUiDriver.RunInitializationOpenCloseLeak(window, DefaultTimeout);

    public static void OpenGameDataInitialCategorySmoke(MainWindow window)
    {
        var form = GameDataSmokeUiDriver.OpenViaMainWindowCommand(window, DefaultTimeout);
        try
        {
            GameDataSmokeUiDriver.AssertInitialTilesetCategory(form);
        }
        finally
        {
            GameDataSmokeUiDriver.CloseForm(form, DefaultTimeout);
        }
    }

    public static Task OpenGameDataSpawnFilterSmokeAsync(MainWindow window)
    {
        GameDataSmokeUiDriver.RunSpawnFilterScenario(window, DefaultTimeout);
        return Task.CompletedTask;
    }

    public static void EnsureWinFormsInitialized()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
    }

    /// <summary>Crée Application WPF et fusionne EditorWpfTheme (comme App.xaml) sans StartupUri.</summary>
    public static void EnsureWpfApplicationInitialized()
    {
        if (System.Windows.Application.Current is null)
        {
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            _wpfThemeLoaded = false;
        }

        if (_wpfThemeLoaded)
        {
            return;
        }

        var theme = new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/Frog.Editor;component/Themes/EditorWpfTheme.xaml",
                UriKind.Absolute),
        };
        System.Windows.Application.Current!.Resources.MergedDictionaries.Add(theme);
        _wpfThemeLoaded = true;
    }

    public static MainWindow CreateAndShowMainWindow()
    {
        EnsureWpfApplicationInitialized();
        var window = new MainWindow();
        window.Show();
        window.UpdateLayout();
        return window;
    }

    /// <summary>Enregistre un tileset 64×64 via le chemin de production <see cref="TilesetCache.LoadFromFile"/>.</summary>
    public static int RegisterMinimalTileset()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frog-smoke-tileset-{Guid.NewGuid():N}.png");
        using (var bmp = new Bitmap(64, 64))
        {
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.ForestGreen);
            bmp.Save(path, ImageFormat.Png);
        }

        var id = TilesetCache.LoadFromFile(path);
        try
        {
            File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }

        return id;
    }

    public static void AssertShellReady(MainWindow window)
    {
        if (window.EditorForm.GetCanvasMapForTest() is not Map map)
        {
            throw new InvalidOperationException("Canvas map is null after workspace init.");
        }

        if (!string.Equals(map.Name, DemoMapFactory.DefaultName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected demo map name '{DemoMapFactory.DefaultName}', got '{map.Name}'.");
        }

        if (map.Width != 20 || map.Height != 15)
        {
            throw new InvalidOperationException($"Unexpected demo dimensions: {map.Width}x{map.Height}.");
        }

        if (map.Layers.Count < 3)
        {
            throw new InvalidOperationException($"Expected at least 3 demo layers, got {map.Layers.Count}.");
        }

        var layerTypes = map.Layers.Select(l => l.LayerType).ToArray();
        if (!layerTypes.Contains(LayerType.Ground)
            || !layerTypes.Contains(LayerType.Fringe)
            || !layerTypes.Contains(LayerType.Attributes))
        {
            throw new InvalidOperationException("Demo map missing expected layer types.");
        }

        var session = window.EditorForm.GetWorkspaceSessionForTest();
        if (session?.CurrentMapId is null || session.CurrentMapId == Guid.Empty)
        {
            throw new InvalidOperationException("Current map id missing after workspace init.");
        }

        if (session.Catalog.Count == 0)
        {
            throw new InvalidOperationException("Demo map missing from catalog.");
        }

        if (!window.EditorForm.AreShellHostsReadyForTest())
        {
            throw new InvalidOperationException("Left/center/right shell hosts are not ready.");
        }

        var caps = window.EditorForm.GetPersistenceCapabilitiesForTest();
        if (caps.IsDurablePersistence)
        {
            throw new InvalidOperationException("Smoke test must not run with durable PostgreSQL backend.");
        }

        if (!caps.AllowsSave)
        {
            throw new InvalidOperationException("Smoke test repository must allow in-memory save.");
        }
    }

    public static void ForceCloseMainWindow(MainWindow window)
    {
        if (!window.Dispatcher.CheckAccess())
        {
            window.Dispatcher.Invoke(() => ForceCloseMainWindow(window));
            return;
        }

        try
        {
            window.AllowCloseWithoutPromptForTest();
            if (window.IsVisible || window.IsLoaded)
            {
                window.Close();
            }
        }
        catch (InvalidOperationException)
        {
            // Dispatcher may already be shutting down for this window.
        }
    }

    public static void AssertSaveSuccess(SaveMapResult result, long previousRevision)
    {
        if (result is not SaveMapResult.Success success)
        {
            throw new InvalidOperationException($"Save failed: {result}");
        }

        if (success.NewRevision <= previousRevision)
        {
            throw new InvalidOperationException($"Expected revision > {previousRevision}, got {success.NewRevision}.");
        }
    }

    private sealed class SilentEditorDialogService : IEditorDialogService
    {
        public EditorPromptChoice PromptSaveDiscardCancel(string message, string title) => EditorPromptChoice.Save;

        public bool ConfirmYesNo(string message, string title) => true;

        public void ShowInfo(string message, string title)
        {
        }

        public void ShowWarning(string message, string title)
        {
        }

        public void ShowError(string message, string title)
        {
        }
    }
}
