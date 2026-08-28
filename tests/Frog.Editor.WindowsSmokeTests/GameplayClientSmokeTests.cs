using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Frog.Client;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Server;
using Frog.Server.Gameplay;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Frog.Client MainShellForm against in-memory Frog.Server: register/login, character,
/// shop acquire + equip via public client protocol, reconnect usability, named (P7-G5)
/// inventory/equipment/bank/ground UI, and combat (melee/spell/respawn) success paths.
/// Screenshots → artifacts/phase-07-gameplay-client/.
/// </summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class GameplayClientSmokeTests
{
    [Fact]
    public void GameplayClient_RegisterLoginCreateInventoryReconnect_Screenshots()
    {
        StaTestRunner.Run(() =>
        {
            using var harness = GameplaySmokeHarness.Create();
            var form = harness.Form;
            try
            {
                harness.ConnectRegisterLogin();
                Pump(form, () => form.CatalogClassesPopulatedForTest, "catalog classes after login");
                AssertAuthTokenStoredAndNeverLogged(form, "Login OK");
                ClientSmokeTestAccess.SaveScreenshot(form, "01-login-token-stored.png");

                var charName = "SmokeHero";
                harness.CreateCharacter(charName);
                ClientSmokeTestAccess.SaveScreenshot(form, "02-character-created.png");

                harness.EnterPlayingPhase(charName);
                Pump(form, () => form.TrySelectWeaponFromCatalogForTest(), "select weapon from catalog");
                var weaponId = form.SelectedCatalogWeaponIdForTest;
                Assert.NotNull(weaponId);

                form.ShopBuyButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.InventoryPanelForTest.ListedItemCountForTest > 0
                          && form.LogContainsForTest("Achat: Achat reussi."),
                    "shop buy put weapon in inventory");

                // P7-G5 : la liste inventaire affiche le nom publié ("[slot] Nom ×qty"), jamais le
                // GUID brut de l'objet.
                form.InventoryPanelForTest.SelectFirstForTest();
                var weaponRowText = form.InventoryPanelForTest.SelectedItemTextForTest;
                Assert.NotNull(weaponRowText);
                Assert.DoesNotContain(weaponId!.Value.ToString("N"), weaponRowText!, StringComparison.OrdinalIgnoreCase);
                Assert.Contains('×', weaponRowText!);

                form.InventoryPanelForTest.ClickEquipForTest();
                Pump(
                    form,
                    () => form.InventoryPanelForTest.EquippedWeaponItemId == weaponId,
                    "equip weapon via client protocol");

                // P7-G5 : EquipmentPanel affiche "Arme: {Name}" — jamais le GUID.
                Assert.StartsWith("Arme: ", form.EquipmentPanelForTest.WeaponLabelTextForTest);
                Assert.DoesNotContain(
                    weaponId.Value.ToString("N"),
                    form.EquipmentPanelForTest.WeaponLabelTextForTest,
                    StringComparison.OrdinalIgnoreCase);

                // P7-G5 : déséquiper puis ré-équiper avant les assertions de reconnexion ci-dessous
                // (qui exigent l'arme équipée) — couvre le flux "unequip after equip" avec succès strict.
                form.EquipmentPanelForTest.ClickUnequipWeaponForTest();
                Pump(
                    form,
                    () => form.InventoryPanelForTest.EquippedWeaponItemId is null
                          && form.LogContainsForTest("Déséquipement: "),
                    "unequip weapon via client protocol");
                Assert.Equal("Arme: —", form.EquipmentPanelForTest.WeaponLabelTextForTest);
                Assert.False(form.EquipmentPanelForTest.UnequipWeaponEnabledForTest);

                form.InventoryPanelForTest.SelectFirstForTest();
                form.InventoryPanelForTest.ClickEquipForTest();
                Pump(
                    form,
                    () => form.InventoryPanelForTest.EquippedWeaponItemId == weaponId,
                    "re-equip weapon via client protocol");

                ClientSmokeTestAccess.SaveScreenshot(form, "03-gameplay-inventory.png");

                form.DisconnectForTest();
                Pump(form, () => form.ConnectButtonForTest.Enabled && form.ConnectButtonForTest.Visible, "disconnect complete");

                form.ConnectButtonForTest.PerformClick();
                Pump(form, () => form.DisconnectButtonForTest.Enabled || form.BackDisconnectButtonForTest.Enabled, "reconnect TCP");
                form.ReconnectButtonForTest.PerformClick();
                // Catalog combo is not cleared on disconnect — wait for the reconnect result log,
                // not CatalogClassesPopulatedForTest (would race and return immediately).
                Pump(form, () => form.LogContainsForTest("Reconnect OK"), "reconnect success logged");
                AssertAuthTokenStoredAndNeverLogged(form, "Reconnect OK");
                Pump(form, () => form.CharCreateButtonForTest.Enabled && form.CharCreateButtonForTest.Visible, "reconnect auth restored character UI");
                Pump(
                    form,
                    () => form.CharactersComboForTest.Items.Count > 0
                          && form.EnterGameButtonForTest.Visible
                          && form.EnterGameButtonForTest.Enabled,
                    "character list after reconnect");
                ClientSmokeTestAccess.SaveScreenshot(form, "04-reconnect-ok.png");

                harness.EnterPlayingPhase(charName);
                // P7-G5 : succès strict — l'équipement doit être exactement celui persisté avant
                // déconnexion, plus de secours "ou objet en inventaire".
                Pump(
                    form,
                    () => form.InventoryPanelForTest.EquippedWeaponItemId == weaponId
                          || form.EquipmentPanelForTest.WeaponLabelTextForTest.StartsWith("Arme: É", StringComparison.Ordinal),
                    "equip persisted exactly across reconnect + character reselect",
                    TimeSpan.FromSeconds(120));
                Assert.True(form.ShopBuyButtonForTest.Enabled && form.ShopBuyButtonForTest.Visible, "shop control usable after reconnect");
                ClientSmokeTestAccess.SaveScreenshot(form, "05-reconnect-gameplay-usable.png");

                AssertScreenshots(
                    "01-login-token-stored.png",
                    "02-character-created.png",
                    "03-gameplay-inventory.png",
                    "04-reconnect-ok.png",
                    "05-reconnect-gameplay-usable.png");
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    [Fact]
    public void GameplayClient_ShopSellAndBankGold()
    {
        StaTestRunner.Run(() =>
        {
            using var harness = GameplaySmokeHarness.Create();
            var form = harness.Form;
            try
            {
                harness.ConnectRegisterLogin();
                Pump(form, () => form.CatalogClassesPopulatedForTest, "catalog after login");
                harness.CreateCharacter("Banker");
                harness.EnterPlayingPhase("Banker");
                form.SelectGameplayTabForTest();
                harness.SetCharacterGoldForTest(150);

                // P7-I1 : arme (stack 1) puis consommable — deux slots distincts.
                // par défaut a MaxStack=20 et serait empilé si acheté deux fois).
                Pump(form, () => form.TrySelectWeaponFromCatalogForTest(), "select weapon");
                form.ShopBuyButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Achat: Achat reussi.") && form.InventoryPanelForTest.ListedItemCountForTest > 0,
                    "buy weapon");

                Pump(form, () => form.TrySelectConsumableFromCatalogForTest(), "select consumable");
                form.ShopBuyButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.InventoryPanelForTest.ListedItemCountForTest >= 2,
                    "consumable in separate inventory slot");
                form.InventoryPanelForTest.SelectSlotByIndexForTest(1);
                Assert.True(form.InventoryPanelForTest.SelectedInventorySlotForTest > 0);

                form.BankQtyNumericForTest.Value = 1;
                form.BankDepositItemButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Banque dépôt: Depose en banque.") && form.BankItemsCountForTest > 0,
                    "bank item deposit success");
                Assert.Contains(
                    form.BankItemsListForTest.Items.Cast<object>(),
                    row => row.ToString()!.Contains('×', StringComparison.Ordinal));

                form.BankItemsListForTest.SelectedIndex = 0;
                form.BankWithdrawItemButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Banque retrait: Retire de la banque.") && form.BankItemsCountForTest == 0,
                    "bank item withdraw success");

                form.BankGoldNumericForTest.Value = 10;
                form.BankDepositGoldButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Banque dépôt: Operation reussie."), "bank gold deposit success");

                form.BankWithdrawGoldButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Banque retrait: Operation reussie."), "bank gold withdraw success");

                form.InventoryPanelForTest.SelectSlotByIndexForTest(1);
                form.ShopSellButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Vente: Vente reussie."), "shop sell success");
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    [Fact]
    public void GameplayClient_ChatRateLimited()
    {
        StaTestRunner.Run(() =>
        {
            using var harness = GameplaySmokeHarness.Create();
            var form = harness.Form;
            try
            {
                harness.ConnectRegisterLogin();
                Pump(form, () => form.CatalogClassesPopulatedForTest, "catalog after login");
                harness.CreateCharacter("Chatter");
                harness.EnterPlayingPhase("Chatter");
                form.SelectChatTabForTest();
                form.SelectChatChannelForTest(0);

                for (var i = 0; i < GameplayLimits.MaxChatMessagesPerWindow + 3; i++)
                {
                    form.ChatTextBoxForTest.Text = $"spam-{i}";
                    form.SendChatButtonForTest.PerformClick();
                    Pump(form, () => form.LogContainsForTest($"spam-{i}") || form.LogContainsForTest("Trop de messages"), $"chat send {i}", TimeSpan.FromSeconds(5));
                }

                Pump(form, () => form.LogContainsForTest("Trop de messages"), "chat rate limit");
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    /// <summary>
    /// P7-G5 : combat de bout en bout avec succès stricts — mêlée et sort réussis contre un
    /// monstre seedé en portée, combat invalide (cible inexistante) sans effet observable sur le
    /// joueur, puis mort forcée (in-memory, côté serveur) + respawn réussi via le protocole client.
    /// </summary>
    [Fact]
    public void GameplayClient_CombatMeleeSpellInvalidTargetAndRespawn()
    {
        StaTestRunner.Run(() =>
        {
            using var harness = GameplaySmokeHarness.Create();
            var form = harness.Form;
            try
            {
                harness.ConnectRegisterLogin();
                Pump(form, () => form.CatalogClassesPopulatedForTest, "catalog after login");
                harness.CreateCharacter("Caster");
                harness.EnterPlayingPhase("Caster");
                form.SelectGameplayTabForTest();

                // In-memory fallback (AllowInMemoryFallback) ne lance pas
                // PublishedWorldBootstrapHostedService : on seede le monstre nous-mêmes,
                // exactement au point de spawn du personnage, pour une portée mêlée/sort
                // déterministe sans dépendre d'un déplacement réseau.
                harness.SpawnSlimeAtDefaultSpawnForTest();

                Assert.True(form.SpellComboForTest.Items.Count > 0, "spell catalog empty");
                form.SpellComboForTest.SelectedIndex = 0;
                form.MeleeTargetComboForTest.Text = "Slime";

                // Mêlée réussie (P7-G5 : succès strict, plus de secours "or refused").
                form.MeleeButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Mêlée → Slime: touche"),
                    "melee hit success logged",
                    TimeSpan.FromSeconds(15));

                // Sort réussi contre la même cible, toujours en portée.
                form.SpellButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Sort: "),
                    "spell cast success logged",
                    TimeSpan.FromSeconds(15));
                Assert.DoesNotContain("Sort refusé:", form.LogTextForTest, StringComparison.Ordinal);

                // Combat invalide : cible inexistante → refus attendu côté serveur, et aucun HP
                // observable ne change côté joueur (le serveur ne renvoie pas de CombatState sur échec).
                var hpBeforeInvalid = form.CombatHpForTest;
                form.MeleeTargetComboForTest.Text = "MonstreInexistant";
                form.MeleeButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Mêlée → MonstreInexistant: rate"),
                    "invalid target melee refused");
                Assert.Equal(hpBeforeInvalid, form.CombatHpForTest);

                // Mort via combat PvP public (second client TCP) puis respawn via le bouton visible.
                harness.KillVictimViaPvpForTest();
                Pump(form, () => form.LogContainsForTest("Mort signalée par le serveur."), "death notify received");
                Pump(form, () => form.CombatIsDeadForTest, "combat state dead");
                Assert.True(form.RespawnButtonForTest.Visible && form.RespawnButtonForTest.Enabled, "respawn button visible");
                form.RespawnButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Respawn: Ressuscite."), "respawn success logged");
                Pump(form, () => !form.CombatIsDeadForTest, "combat state cleared (not dead) after respawn");
                Assert.Equal(form.CombatMaxHpForTest, form.CombatHpForTest);
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    [Fact]
    public void GameplayClient_InventoryDropGroundAndPickup()
    {
        StaTestRunner.Run(() =>
        {
            using var harness = GameplaySmokeHarness.Create();
            var form = harness.Form;
            try
            {
                harness.ConnectRegisterLogin();
                Pump(form, () => form.CatalogClassesPopulatedForTest, "catalog after login");
                harness.CreateCharacter("Dropper");
                harness.EnterPlayingPhase("Dropper");
                form.SelectGameplayTabForTest();

                Pump(form, () => form.TrySelectWeaponFromCatalogForTest(), "select weapon");
                var weaponId = form.SelectedCatalogWeaponIdForTest;
                Assert.NotNull(weaponId);
                form.ShopBuyButtonForTest.PerformClick();
                Pump(form, () => form.InventoryPanelForTest.ListedItemCountForTest > 0, "inventory has item");

                // P7-G5 : succès strict — le drop doit réussir, plus de secours "ou refusé".
                form.InventoryPanelForTest.SelectFirstForTest();
                form.InventoryPanelForTest.ClickDropForTest();
                Pump(
                    form,
                    () => form.LogContainsForTest("Drop: Depose.") && form.GroundItemsCountForTest > 0,
                    "drop item success");

                // P7-G5 : la liste des objets au sol affiche le nom publié, jamais le GUID brut.
                var groundRowText = form.GroundItemsListForTest.Items[0]!.ToString();
                Assert.NotNull(groundRowText);
                Assert.DoesNotContain(weaponId!.Value.ToString("N"), groundRowText!, StringComparison.OrdinalIgnoreCase);
                Assert.Contains('×', groundRowText!);

                // Pickup (P7-G5) : ramasser l'objet déposé — succès strict, item de retour en
                // inventaire, liste sol vidée et bouton Ramasser désactivé en conséquence.
                form.SelectFirstGroundItemForTest();
                var inventoryCountBeforePickup = form.InventoryPanelForTest.ListedItemCountForTest;
                form.ClickPickupForTest();
                Pump(
                    form,
                    () => form.LogContainsForTest("Ramassé: Ramasse.") && form.GroundItemsCountForTest == 0,
                    "pickup success");
                Assert.Equal(inventoryCountBeforePickup + 1, form.InventoryPanelForTest.ListedItemCountForTest);
                Assert.False(form.PickupButtonForTest.Enabled);
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    [Fact]
    public void GameplayClient_Phase7ScreenshotFlows()
    {
        StaTestRunner.Run(() =>
        {
            using var harness = GameplaySmokeHarness.Create();
            var form = harness.Form;
            try
            {
                harness.ConnectRegisterLogin();
                Pump(form, () => form.CatalogClassesPopulatedForTest, "catalog after login");
                harness.CreateCharacter("ShotHero");
                harness.EnterPlayingPhase("ShotHero");
                form.SelectGameplayTabForTest();
                harness.SetCharacterGoldForTest(150);

                Pump(form, () => form.TrySelectConsumableFromCatalogForTest(), "consumable for bank shot");
                form.ShopBuyButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Achat: Achat reussi."), "shop buy for screenshot");
                form.InventoryPanelForTest.SelectSlotByIndexForTest(0);
                form.BankQtyNumericForTest.Value = 1;
                form.BankDepositItemButtonForTest.PerformClick();
                Pump(form, () => form.BankItemsCountForTest > 0, "bank deposit for screenshot");
                ClientSmokeTestAccess.SaveScreenshot(form, "06-bank-shop.png");

                Pump(form, () => form.TrySelectWeaponFromCatalogForTest(), "weapon for ground shot");
                form.ShopBuyButtonForTest.PerformClick();
                Pump(form, () => form.InventoryPanelForTest.ListedItemCountForTest > 0, "weapon bought");
                form.InventoryPanelForTest.SelectFirstForTest();
                form.InventoryPanelForTest.ClickDropForTest();
                Pump(form, () => form.GroundItemsCountForTest > 0, "ground item for screenshot");
                ClientSmokeTestAccess.SaveScreenshot(form, "07-ground-drop.png");

                harness.SpawnSlimeAtDefaultSpawnForTest();
                form.MeleeTargetComboForTest.Text = "Slime";
                form.MeleeButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Mêlée → Slime: touche"), "melee for screenshot", TimeSpan.FromSeconds(15));
                form.SpellComboForTest.SelectedIndex = 0;
                form.SpellButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Sort: "), "spell for screenshot", TimeSpan.FromSeconds(15));
                ClientSmokeTestAccess.SaveScreenshot(form, "08-combat-spell.png");

                harness.KillVictimViaPvpForTest();
                Pump(form, () => form.RespawnButtonForTest.Enabled, "death respawn button");
                ClientSmokeTestAccess.SaveScreenshot(form, "09-death-respawn-button.png");
                form.RespawnButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Respawn: Ressuscite."), "respawn after screenshot");
                ClientSmokeTestAccess.SaveScreenshot(form, "10-post-respawn.png");

                AssertScreenshots(
                    "06-bank-shop.png",
                    "07-ground-drop.png",
                    "08-combat-spell.png",
                    "09-death-respawn-button.png",
                    "10-post-respawn.png");
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    /// <summary>
    /// P7-G1 : le jeton de session (LoginResult/ReconnectResult) doit être stocké côté client mais
    /// jamais apparaître dans le log UI, ni via un motif "OK: &lt;jeton&gt;" laissé par erreur.
    /// </summary>
    private static void AssertAuthTokenStoredAndNeverLogged(MainShellForm form, string expectedOkLine)
    {
        var token = form.StoredAuthTokenForTest;
        Assert.NotNull(token);
        Assert.NotEmpty(token!);

        var log = form.LogTextForTest;
        Assert.DoesNotContain(token!, log, StringComparison.Ordinal);
        Assert.Contains(expectedOkLine, log, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedOkLine + ": ", log, StringComparison.Ordinal);
    }

    private static void AssertScreenshots(params string[] names)
    {
        var screenshotDir = ClientSmokeTestAccess.ScreenshotDirectory;
        Assert.True(Directory.Exists(screenshotDir), $"Missing screenshot dir: {screenshotDir}");
        foreach (var name in names)
        {
            var path = Path.Combine(screenshotDir, name);
            Assert.True(File.Exists(path), $"Missing screenshot: {path}");
            Assert.True(new FileInfo(path).Length > 0, $"Empty screenshot: {path}");
        }
    }

    private static void Pump(MainShellForm form, Func<bool> predicate, string step, TimeSpan? timeout = null)
    {
        var limit = timeout ?? ClientSmokeTestAccess.DefaultTimeout;
        var deadline = DateTime.UtcNow + limit;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        if (!predicate())
        {
            var log = form.LogTextForTest;
            var tail = log.Length <= 800 ? log : log[^800..];
            throw new TimeoutException(
                $"Gameplay client smoke timed out at step '{step}'. Log tail:{Environment.NewLine}{tail}");
        }
    }

    private sealed class GameplaySmokeHarness : IDisposable
    {
        private readonly IHost _host;
        private bool _disposed;

        private GameplaySmokeHarness(IHost host, MainShellForm form, string user, string password)
        {
            _host = host;
            Form = form;
            User = user;
            Password = password;
        }

        public MainShellForm Form { get; }

        public string User { get; }

        public string Password { get; }

        public static GameplaySmokeHarness Create()
        {
            ClientSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);
            var port = GetFreePort();
            var host = FrogServerHostFactory
                .CreateHostBuilder(configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                })
                .ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Server:Port"] = port.ToString(),
                        ["Server:BindAddress"] = "127.0.0.1",
                        ["MariaDb:Enabled"] = "false",
                        ["PostgreSql:AllowInMemoryFallback"] = "true",
                    });
                })
                .Build();
            host.Start();

            var user = $"gc-{Guid.NewGuid():N}"[..18];
            const string password = "smoke-pass-7";
            var form = ClientSmokeTestAccess.CreateAndShowMainShell();
            form.HostTextBoxForTest.Text = "127.0.0.1";
            form.PortNumericForTest.Value = port;
            form.UserTextBoxForTest.Text = user;
            form.PassTextBoxForTest.Text = password;
            return new GameplaySmokeHarness(host, form, user, password);
        }

        public void ConnectRegisterLogin()
        {
            Form.ConnectButtonForTest.PerformClick();
            Pump(Form, () => Form.DisconnectButtonForTest.Enabled, "connect TCP");
            Form.RegisterButtonForTest.PerformClick();
            Pump(Form, () => Form.LogContainsForTest("Inscription OK"), "register success");
            Form.LoginButtonForTest.PerformClick();
            Pump(Form, () => Form.StoredAuthTokenForTest is not null, "login token stored");
            Pump(Form, () => Form.CharCreateButtonForTest.Enabled, "character create enabled after login");
        }

        public void CreateCharacter(string charName)
        {
            Form.NewCharNameTextBoxForTest.Text = charName;
            if (Form.ClassesComboForTest.Items.Count > 0)
            {
                Form.ClassesComboForTest.SelectedIndex = 0;
            }

            Form.CharCreateButtonForTest.PerformClick();
            Pump(
                Form,
                () => Form.CharactersComboForTest.Items.Count > 0 && Form.LogContainsForTest("Perso créé"),
                "character created and listed");
        }

        public void EnterPlayingPhase(string charName)
        {
            var pick = Form.CharactersComboForTest.Items.Cast<object>()
                .FirstOrDefault(i => i.ToString()?.Contains(charName, StringComparison.Ordinal) == true);
            Assert.NotNull(pick);
            Form.CharactersComboForTest.SelectedItem = pick;
            Assert.False(string.IsNullOrWhiteSpace(Form.SelectedCharacterIdForTest));
            Form.EnterGameButtonForTest.PerformClick();
            Pump(Form, () => Form.IsPlayingPhaseForTest, "enter playing phase");
            Form.SelectGameplayTabForTest();
            Pump(Form, () => Form.ShopBuyButtonForTest.Enabled && Form.ShopBuyButtonForTest.Visible, "shop buy visible on Gameplay tab");
        }

        public void SetCharacterGoldForTest(int gold)
        {
            var charIdText = Form.SelectedCharacterIdForTest;
            Assert.False(string.IsNullOrWhiteSpace(charIdText));
            var chars = _host.Services.GetRequiredService<Frog.Application.Gameplay.ICharacterRepository>();
            var charId = Guid.Parse(charIdText!);
            var record = chars.FindByIdAsync(charId).GetAwaiter().GetResult();
            Assert.NotNull(record);
            chars.SaveAsync(record! with { Gold = gold }).GetAwaiter().GetResult();
        }

        /// <summary>
        /// P7-G5 : seed direct (hors protocole) d'un monstre "Slime" au point de spawn par défaut
        /// du personnage — l'AllowInMemoryFallback utilisé par ce harness ne fait pas tourner
        /// PublishedWorldBootstrapHostedService (réservé au monde publié PostgreSQL), donc aucun
        /// monstre n'existe sans cet appel explicite.
        /// </summary>
        public void SpawnSlimeAtDefaultSpawnForTest()
        {
            var combat = _host.Services.GetRequiredService<CombatGameplayService>();
            var (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(
                GameplayLimits.DefaultSpawnTileX,
                GameplayLimits.DefaultSpawnTileY);
            var spawned = combat.SpawnMonster(
                GameplayLimits.DefaultSpawnMapId,
                Phase7ContentSeed.DefaultMonsterId,
                pixelX,
                pixelY);
            Assert.NotNull(spawned);
        }

        /// <summary>
        /// Tue le joueur du harness via un second client TCP et le protocole mêlée PvP public.
        /// </summary>
        public void KillVictimViaPvpForTest()
        {
            var port = (int)Form.PortNumericForTest.Value;
            using var attacker = new SmokeTcpClient();
            attacker.Connect("127.0.0.1", port);
            attacker.ReadFrame();
            var attackerUser = $"atk-{Guid.NewGuid():N}"[..16];
            const string password = "smoke-pass-7";
            attacker.Send(SmokeTcpPackets.Register(attackerUser, password));
            attacker.ReadUntil(PacketId.RegisterResult);
            attacker.Send(SmokeTcpPackets.Login(attackerUser, password));
            attacker.ReadUntil(PacketId.LoginResult);
            attacker.Send(SmokeTcpPackets.CharacterCreate("Killer", Phase7ContentSeed.DefaultClassId));
            var create = attacker.ReadUntil(PacketId.CharacterCreateResult);
            var charId = SmokeTcpPackets.DecodeCharacterId(create);
            attacker.Send(SmokeTcpPackets.CharacterSelect(charId));
            attacker.ReadUntil(PacketId.CharacterSelectResult);
            attacker.Drain();

            for (var i = 0; i < 40; i++)
            {
                var hpBefore = Form.CombatHpForTest;
                attacker.Send(SmokeTcpPackets.Melee(User));
                attacker.ReadUntil(PacketId.MeleeAttackResult);
                Pump(
                    Form,
                    () =>
                    {
                        if (Form.CombatIsDeadForTest)
                        {
                            return true;
                        }

                        var hpNow = Form.CombatHpForTest;
                        return hpBefore is not null && hpNow is not null && hpNow < hpBefore;
                    },
                    $"pvp hit {i}",
                    TimeSpan.FromSeconds(3));
                if (Form.CombatIsDeadForTest)
                {
                    return;
                }

                Thread.Sleep(CombatFormulas.BasicAttackCooldownMs + 20);
            }

            throw new TimeoutException("Victim was not killed via public PvP melee.");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ClientSmokeTestAccess.CloseMainShell(Form);
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
            ClientSmokeTestAccess.ResetHooks();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    private sealed class SmokeTcpClient : IDisposable
    {
        private readonly TcpClient _tcp = new();
        private NetworkStream? _stream;

        public void Connect(string host, int port)
        {
            _tcp.ReceiveTimeout = 20_000;
            _tcp.SendTimeout = 20_000;
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();
            _stream.ReadTimeout = 20_000;
            _stream.WriteTimeout = 20_000;
        }

        public void Send(byte[] payload)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            _stream!.Write(frame);
        }

        public byte[] ReadFrame()
        {
            var lenBuf = new byte[4];
            ReadExact(lenBuf);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            ReadExact(payload);
            return payload;
        }

        public byte[] ReadUntil(PacketId id, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
            while (DateTime.UtcNow < deadline)
            {
                var remainingMs = (int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);
                _stream!.ReadTimeout = remainingMs;
                try
                {
                    var frame = ReadFrame();
                    if (frame[0] == (byte)id)
                    {
                        return frame;
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
                {
                    break;
                }
            }

            throw new TimeoutException($"Timed out waiting for {id}.");
        }

        public void Drain()
        {
            if (_stream is null)
            {
                return;
            }

            while (_stream.DataAvailable)
            {
                _ = ReadFrame();
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcp.Dispose();
        }

        private void ReadExact(byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = _stream!.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
            }
        }
    }

    private static class SmokeTcpPackets
    {
        public static byte[] Register(string user, string pass) => Login(user, pass, PacketId.RegisterRequest);

        public static byte[] Login(string user, string pass, PacketId id = PacketId.LoginRequest)
        {
            var u = Encoding.UTF8.GetBytes(user);
            var p = Encoding.UTF8.GetBytes(pass);
            var payload = new byte[1 + 1 + u.Length + 1 + p.Length];
            payload[0] = (byte)id;
            payload[1] = (byte)u.Length;
            u.CopyTo(payload, 2);
            payload[2 + u.Length] = (byte)p.Length;
            p.CopyTo(payload, 3 + u.Length);
            return payload;
        }

        public static byte[] CharacterCreate(string name, Guid classId)
        {
            var n = Encoding.UTF8.GetBytes(name);
            var payload = new byte[1 + 1 + n.Length + 16];
            payload[0] = (byte)PacketId.CharacterCreateRequest;
            payload[1] = (byte)n.Length;
            n.CopyTo(payload, 2);
            classId.TryWriteBytes(payload.AsSpan(2 + n.Length));
            return payload;
        }

        public static byte[] CharacterSelect(string id)
        {
            var b = Encoding.UTF8.GetBytes(id);
            var payload = new byte[1 + 1 + b.Length];
            payload[0] = (byte)PacketId.CharacterSelectRequest;
            payload[1] = (byte)b.Length;
            b.CopyTo(payload, 2);
            return payload;
        }

        public static byte[] Melee(string target)
        {
            var t = Encoding.UTF8.GetBytes(target);
            var payload = new byte[1 + 1 + t.Length];
            payload[0] = (byte)PacketId.MeleeAttackRequest;
            payload[1] = (byte)t.Length;
            t.CopyTo(payload, 2);
            return payload;
        }

        public static string DecodeCharacterId(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 3 || payload[0] != (byte)PacketId.CharacterCreateResult || payload[1] == 0)
            {
                throw new InvalidOperationException("CharacterCreateResult invalide.");
            }

            var len = payload[2];
            return Encoding.UTF8.GetString(payload.Slice(3, len));
        }
    }
}
