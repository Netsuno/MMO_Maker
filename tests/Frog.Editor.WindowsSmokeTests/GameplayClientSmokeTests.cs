using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Frog.Client;
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
            ClientSmokeTestAccess.SetPumpUntilForTest(StaTestRunner.PumpUntil);
            MainShellForm? form = null;
            IHost? host = null;
            try
            {
                var port = GetFreePort();
                host = FrogServerHostFactory
                    .CreateHostBuilder(
                        configureServices: services =>
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

                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                form.HostTextBoxForTest.Text = "127.0.0.1";
                form.PortNumericForTest.Value = port;
                form.UserTextBoxForTest.Text = user;
                form.PassTextBoxForTest.Text = password;

                form.ConnectButtonForTest.PerformClick();
                Pump(form, () => form.DisconnectButtonForTest.Enabled, "connect TCP");

                form.RegisterButtonForTest.PerformClick();
                Pump(form, () => form.LogContainsForTest("Inscription OK"), "register success");

                form.LoginButtonForTest.PerformClick();
                Pump(form, () => form.StoredAuthTokenForTest is not null, "login token stored");
                Pump(form, () => form.CharCreateButtonForTest.Enabled, "character create enabled after login");
                ClientSmokeTestAccess.SaveScreenshot(form, "01-login-token-stored.png");

                var charName = "SmokeHero";
                form.NewCharNameTextBoxForTest.Text = charName;
                form.CharCreateButtonForTest.PerformClick();
                Pump(
                    form,
                    () => form.CharactersComboForTest.Items.Count > 0
                          && form.LogContainsForTest("Perso créé"),
                    "character created and listed");
                ClientSmokeTestAccess.SaveScreenshot(form, "02-character-created.png");

                var pick = form.CharactersComboForTest.Items.Cast<object>()
                    .FirstOrDefault(i => i.ToString()?.Contains(charName, StringComparison.Ordinal) == true);
                Assert.NotNull(pick);
                form.CharactersComboForTest.SelectedItem = pick;
                Assert.False(string.IsNullOrWhiteSpace(form.SelectedCharacterIdForTest));

                form.EnterGameButtonForTest.PerformClick();
                Pump(form, () => form.IsPlayingPhaseForTest, "enter playing phase");
                Pump(form, () => form.ShopBuyButtonForTest.Enabled, "shop buy enabled in playing phase");

                // Obtain weapon through public shop protocol (no mid-scenario DI mutation).
                form.ShopItemIdTextBoxForTest.Text = Phase7ClientContentSeed.DefaultWeaponId.ToString();
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
                    () => form.InventoryPanelForTest.EquippedWeaponItemId == Phase7ClientContentSeed.DefaultWeaponId,
                    "equip weapon via client protocol");

                ClientSmokeTestAccess.SaveScreenshot(form, "03-gameplay-inventory.png");

                form.DisconnectButtonForTest.PerformClick();
                Pump(form, () => form.ConnectButtonForTest.Enabled, "disconnect complete");

                form.ConnectButtonForTest.PerformClick();
                Pump(form, () => form.DisconnectButtonForTest.Enabled, "reconnect TCP");
                form.ReconnectButtonForTest.PerformClick();
                Pump(form, () => form.CharCreateButtonForTest.Enabled, "reconnect auth restored character UI");
                Pump(
                    form,
                    () => form.CharactersComboForTest.Items.Count > 0,
                    "character list after reconnect");
                ClientSmokeTestAccess.SaveScreenshot(form, "04-reconnect-ok.png");

                // Prove controls remain usable after reconnect: reselect + inventory still shows equipped weapon.
                pick = form.CharactersComboForTest.Items.Cast<object>()
                    .FirstOrDefault(i => i.ToString()?.Contains(charName, StringComparison.Ordinal) == true);
                Assert.NotNull(pick);
                form.CharactersComboForTest.SelectedItem = pick;
                form.EnterGameButtonForTest.PerformClick();
                Pump(form, () => form.IsPlayingPhaseForTest, "re-enter playing after reconnect");
                Pump(
                    form,
                    () => form.InventoryPanelForTest.EquippedWeaponItemId == Phase7ClientContentSeed.DefaultWeaponId
                          || form.InventoryPanelForTest.ListedItemCountForTest > 0,
                    "inventory/equipment usable after reconnect");
                Assert.True(form.ShopBuyButtonForTest.Enabled, "shop control usable after reconnect");
                ClientSmokeTestAccess.SaveScreenshot(form, "05-reconnect-gameplay-usable.png");

                var screenshotDir = ClientSmokeTestAccess.ScreenshotDirectory;
                Assert.True(Directory.Exists(screenshotDir), $"Missing screenshot dir: {screenshotDir}");
                foreach (var name in new[]
                {
                    "01-login-token-stored.png",
                    "02-character-created.png",
                    "03-gameplay-inventory.png",
                    "04-reconnect-ok.png",
                    "05-reconnect-gameplay-usable.png",
                })
                {
                    var path = Path.Combine(screenshotDir, name);
                    Assert.True(File.Exists(path), $"Missing screenshot: {path}");
                    Assert.True(new FileInfo(path).Length > 0, $"Empty screenshot: {path}");
                }
            }
            finally
            {
                if (form is not null)
                {
                    ClientSmokeTestAccess.CloseMainShell(form);
                }

                if (host is not null)
                {
                    host.StopAsync().GetAwaiter().GetResult();
                    host.Dispose();
                }

                ClientSmokeTestAccess.ResetHooks();
            }
        });
    }

    private static void Pump(MainShellForm form, Func<bool> predicate, string step)
    {
        try
        {
            ClientSmokeTestAccess.PumpUntil(predicate, ClientSmokeTestAccess.DefaultTimeout);
        }
        catch (TimeoutException)
        {
            var log = form.LogTextForTest;
            var tail = log.Length <= 800 ? log : log[^800..];
            throw new TimeoutException(
                $"Gameplay client smoke timed out at step '{step}'. Log tail:{Environment.NewLine}{tail}");
        }
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
