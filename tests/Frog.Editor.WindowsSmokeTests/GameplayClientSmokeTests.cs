using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Frog.Client;
using Frog.Core.Gameplay;
using Frog.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Frog.Client MainShellForm against in-memory Frog.Server: register/login, character,
/// shop acquire + equip via public client protocol, reconnect usability.
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
                          && form.LogContainsForTest("Achat:"),
                    "shop buy put weapon in inventory");

                form.InventoryPanelForTest.SelectFirstForTest();
                form.InventoryPanelForTest.ClickEquipForTest();
                Pump(
                    form,
                    () => form.InventoryPanelForTest.EquippedWeaponItemId == weaponId,
                    "equip weapon via client protocol");

                ClientSmokeTestAccess.SaveScreenshot(form, "03-gameplay-inventory.png");

                form.DisconnectForTest();
                Pump(form, () => form.ConnectButtonForTest.Enabled && form.ConnectButtonForTest.Visible, "disconnect complete");

                form.ConnectButtonForTest.PerformClick();
                Pump(form, () => form.DisconnectButtonForTest.Enabled || form.BackDisconnectButtonForTest.Enabled, "reconnect TCP");
                form.ReconnectButtonForTest.PerformClick();
                Pump(form, () => form.CatalogClassesPopulatedForTest, "catalog after reconnect");
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
                Pump(
                    form,
                    () => form.InventoryPanelForTest.EquippedWeaponItemId == weaponId
                          || form.InventoryPanelForTest.ListedItemCountForTest > 0,
                    "inventory/equipment usable after reconnect");
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

                Pump(form, () => form.TrySelectConsumableFromCatalogForTest(), "select consumable");
                form.ShopBuyButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Achat:"), "buy consumable");

                form.BankGoldNumericForTest.Value = 10;
                form.BankDepositGoldButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Banque dépôt: "), "bank gold deposit");

                form.BankWithdrawGoldButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Banque retrait:"), "bank gold withdraw");

                form.ShopSellButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Vente:") || form.LogContainsForTest("Vente refusée:"),
                    "shop sell attempt");
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

    [Fact]
    public void GameplayClient_SpellCastProtocol()
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

                Assert.True(form.SpellComboForTest.Items.Count > 0, "spell catalog empty");
                form.SpellComboForTest.SelectedIndex = 0;
                form.SelectGameplayTabForTest();
                form.MeleeTargetTextBoxForTest.Text = "Slime";

                form.SpellButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.LogContainsForTest("Sort:") || form.LogContainsForTest("Sort refusé:"),
                    "spell cast result logged",
                    TimeSpan.FromSeconds(15));
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    [Fact]
    public void GameplayClient_InventoryDropGround()
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
                form.ShopBuyButtonForTest.PerformClick();
                Pump(form, () => form.InventoryPanelForTest.ListedItemCountForTest > 0, "inventory has item");

                form.InventoryPanelForTest.SelectFirstForTest();
                form.InventoryPanelForTest.ClickDropForTest();
                Pump(
                    form,
                    () => form.LogContainsForTest("Drop:") || form.LogContainsForTest("Drop refusé:"),
                    "drop item result");
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
}
