using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
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

    private static bool _wpfThemeLoaded;

    public static void ResetHooks()
    {
        EditorTestHooks.OverrideMapRepository = null;
        EditorTestHooks.OverrideTilesetRepository = null;
        EditorTestHooks.OverrideNpcRepository = null;
        EditorTestHooks.OverrideItemRepository = null;
        EditorTestHooks.OverrideSpellRepository = null;
        EditorTestHooks.OverrideClassRepository = null;
        EditorTestHooks.OverrideShopRepository = null;
        EditorTestHooks.OverrideDialogService = null;
        EditorTestHooks.OverridePlaytestProcessLauncher = null;
        EditorTestHooks.OverrideServerExePath = null;
        EditorTestHooks.OverrideClientExePath = null;
        EditorTestHooks.OverrideSpawnTile = null;
        EditorTestHooks.AllowNonDurablePlaytest = false;
        EditorTestHooks.SkipMariaDbOnStartup = true;
        TilesetCache.Clear();
        Environment.SetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory, "1");
    }

    public static void ConfigureInMemoryRepository()
    {
        ResetHooks();
        EditorTestHooks.OverrideMapRepository = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
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
        EditorTestHooks.OverrideDialogService = new SilentEditorDialogService();
    }

    public static async Task OpenGameDataAndSaveSampleTilesetAsync(MainWindow window)
    {
        using var form = new Forms.GameData.GameDataForm();
        // Accès interne via type public : on valide create/save via session injectée.
        var bundle = Services.EditorTilesetRepositoryFactory.CreateBundle();
        var session = new Frog.Application.Content.TilesetWorkspaceSession(bundle.Repository);
        var def = new Frog.Core.Models.TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokeTileset",
            LogicalPath = "tiles/smoke.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('A', 64),
            EditorPaletteId = 1,
        };
        session.AdoptNewDraft(def);
        var saved = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.SaveDraft);
        if (saved is not Frog.Application.Content.SaveTilesetResult.Success)
        {
            throw new InvalidOperationException($"Tileset smoke save failed: {saved}");
        }

        session.MarkDirty();
        var published = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.Publish);
        if (published is not Frog.Application.Content.SaveTilesetResult.Success)
        {
            throw new InvalidOperationException($"Tileset smoke publish failed: {published}");
        }

        var catalog = await bundle.PublishedCatalog.ListPublishedAsync();
        if (catalog.All(t => t.Name != "SmokeTileset"))
        {
            throw new InvalidOperationException("Published tileset missing from catalog after smoke publish.");
        }
    }

    public static async Task OpenGameDataAndSaveSampleNpcAsync(MainWindow window)
    {
        using var form = new Forms.GameData.GameDataForm();
        var bundle = Services.EditorNpcRepositoryFactory.CreateBundle();
        var session = new Frog.Application.Content.NpcWorkspaceSession(bundle.Repository);
        session.AdoptNewDraft(new Frog.Core.Models.NpcDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokeMonster",
            Kind = Frog.Core.Models.NpcKind.Monster,
            SpriteLogicalPath = "sprites/npcs/smoke-monster.png",
            Level = 12,
            Notes = "NPC smoke test",
            EditorAliasId = 2,
        });
        var saved = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.SaveDraft);
        if (saved is not Frog.Application.Content.SaveNpcResult.Success)
        {
            throw new InvalidOperationException($"NPC smoke save failed: {saved}");
        }

        session.MarkDirty();
        var published = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.Publish);
        if (published is not Frog.Application.Content.SaveNpcResult.Success)
        {
            throw new InvalidOperationException($"NPC smoke publish failed: {published}");
        }

        var catalog = await bundle.PublishedCatalog.ListPublishedAsync();
        if (catalog.All(n => n.Name != "SmokeMonster"))
        {
            throw new InvalidOperationException("Published NPC missing from catalog after smoke publish.");
        }
    }

    public static async Task OpenGameDataAndSaveSampleItemAsync(MainWindow window)
    {
        using var form = new Forms.GameData.GameDataForm();
        var bundle = Services.EditorItemRepositoryFactory.CreateBundle();
        var session = new Frog.Application.Content.ItemWorkspaceSession(bundle.Repository);
        session.AdoptNewDraft(new Frog.Core.Models.ItemDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokePotion",
            Kind = ItemType.Consumable,
            IconLogicalPath = "icons/items/smoke-potion.png",
            MaxStack = 20,
            BuyPrice = 50,
            SellPrice = 15,
            Description = "Item smoke test",
        });
        var saved = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.SaveDraft);
        if (saved is not Frog.Application.Content.SaveItemResult.Success)
        {
            throw new InvalidOperationException($"Item smoke save failed: {saved}");
        }

        session.MarkDirty();
        var published = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.Publish);
        if (published is not Frog.Application.Content.SaveItemResult.Success)
        {
            throw new InvalidOperationException($"Item smoke publish failed: {published}");
        }

        var catalog = await bundle.PublishedCatalog.ListPublishedAsync();
        if (catalog.All(item => item.Name != "SmokePotion"))
        {
            throw new InvalidOperationException("Published item missing from catalog after smoke publish.");
        }
    }

    public static async Task OpenGameDataAndSaveSampleSpellAsync(MainWindow window)
    {
        using var form = new Forms.GameData.GameDataForm();
        var bundle = Services.EditorSpellRepositoryFactory.CreateBundle();
        var session = new Frog.Application.Content.SpellWorkspaceSession(bundle.Repository);
        session.AdoptNewDraft(new Frog.Core.Models.SpellDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokeFireball",
            Kind = SpellKind.Spell,
            ManaCost = 20,
            CooldownMs = 1500,
            TargetType = TargetType.SingleEnemy,
            IconLogicalPath = "icons/spells/smoke-fireball.png",
            Description = "Spell smoke test",
        });
        var saved = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.SaveDraft);
        if (saved is not Frog.Application.Content.SaveSpellResult.Success)
        {
            throw new InvalidOperationException($"Spell smoke save failed: {saved}");
        }

        session.MarkDirty();
        var published = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.Publish);
        if (published is not Frog.Application.Content.SaveSpellResult.Success)
        {
            throw new InvalidOperationException($"Spell smoke publish failed: {published}");
        }

        var catalog = await bundle.PublishedCatalog.ListPublishedAsync();
        if (catalog.All(spell => spell.Name != "SmokeFireball"))
        {
            throw new InvalidOperationException("Published spell missing from catalog after smoke publish.");
        }
    }

    public static async Task OpenGameDataAndSaveSampleClassAsync(MainWindow window)
    {
        using var form = new Forms.GameData.GameDataForm();
        var spellBundle = Services.EditorSpellRepositoryFactory.CreateBundle();
        var spellSession = new Frog.Application.Content.SpellWorkspaceSession(spellBundle.Repository);
        spellSession.AdoptNewDraft(new Frog.Core.Models.SpellDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokeClassStarter",
            Kind = SpellKind.Skill,
            ManaCost = 0,
            CooldownMs = 500,
            TargetType = TargetType.Self,
            IconLogicalPath = "icons/spells/smoke-class-starter.png",
        });
        var spellPublished = await spellSession.SaveCurrentAsync(
            Frog.Application.Content.SaveContentIntent.Publish);
        if (spellPublished is not Frog.Application.Content.SaveSpellResult.Success spellSuccess)
        {
            throw new InvalidOperationException($"Class smoke prerequisite spell failed: {spellPublished}");
        }

        var bundle = Services.EditorClassRepositoryFactory.CreateBundle(spellBundle.Repository);
        var session = new Frog.Application.Content.ClassWorkspaceSession(bundle.Repository);
        session.AdoptNewDraft(new Frog.Core.Models.ClassDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokeWarrior",
            Description = "Class smoke test",
            BaseHp = 120,
            BaseMp = 30,
            Str = 15,
            Agi = 9,
            Vit = 14,
            Int = 5,
            Dex = 10,
            Luck = 7,
            StartingSpellId = spellSuccess.SpellId,
        });
        var saved = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.SaveDraft);
        if (saved is not Frog.Application.Content.SaveClassResult.Success)
        {
            throw new InvalidOperationException($"Class smoke save failed: {saved}");
        }

        session.MarkDirty();
        var published = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.Publish);
        if (published is not Frog.Application.Content.SaveClassResult.Success)
        {
            throw new InvalidOperationException($"Class smoke publish failed: {published}");
        }

        var catalog = await bundle.PublishedCatalog.ListPublishedAsync();
        if (catalog.All(characterClass => characterClass.Name != "SmokeWarrior"))
        {
            throw new InvalidOperationException("Published class missing from catalog after smoke publish.");
        }
    }

    public static async Task OpenGameDataAndSaveSampleShopAsync(MainWindow window)
    {
        using var form = new Forms.GameData.GameDataForm();
        var itemBundle = Services.EditorItemRepositoryFactory.CreateBundle();
        var itemSession = new Frog.Application.Content.ItemWorkspaceSession(itemBundle.Repository);
        itemSession.AdoptNewDraft(new Frog.Core.Models.ItemDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokeShopPotion",
            Kind = ItemType.Consumable,
            IconLogicalPath = "icons/items/smoke-shop-potion.png",
            MaxStack = 20,
            BuyPrice = 50,
            SellPrice = 15,
        });
        var itemPublished = await itemSession.SaveCurrentAsync(
            Frog.Application.Content.SaveContentIntent.Publish);
        if (itemPublished is not Frog.Application.Content.SaveItemResult.Success itemSuccess)
        {
            throw new InvalidOperationException($"Shop smoke prerequisite item failed: {itemPublished}");
        }

        var bundle = Services.EditorShopRepositoryFactory.CreateBundle(itemBundle.PublishedCatalog);
        var session = new Frog.Application.Content.ShopWorkspaceSession(bundle.Repository);
        session.AdoptNewDraft(new Frog.Core.Models.ShopDefinition
        {
            Id = Guid.NewGuid(),
            Name = "SmokeShop",
            Description = "Shop content smoke test",
            Listings =
            {
                new Frog.Core.Models.ShopListing
                {
                    ItemId = itemSuccess.ItemId,
                    Price = 75,
                    Stock = null,
                },
            },
        });
        var saved = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.SaveDraft);
        if (saved is not Frog.Application.Content.SaveShopResult.Success)
        {
            throw new InvalidOperationException($"Shop smoke save failed: {saved}");
        }

        session.MarkDirty();
        var published = await session.SaveCurrentAsync(Frog.Application.Content.SaveContentIntent.Publish);
        if (published is not Frog.Application.Content.SaveShopResult.Success)
        {
            throw new InvalidOperationException($"Shop smoke publish failed: {published}");
        }

        var catalog = await bundle.PublishedCatalog.ListPublishedAsync();
        if (catalog.All(shop => shop.Name != "SmokeShop"))
        {
            throw new InvalidOperationException("Published shop missing from catalog after smoke publish.");
        }
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
