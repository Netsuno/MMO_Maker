using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Client;
using Frog.Server;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Frog.Client MainShellForm against in-memory Frog.Server : register/login, perso, inventaire, reconnect.
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
                ClientSmokeTestAccess.PumpUntil(() => form.DisconnectButtonForTest.Enabled, ClientSmokeTestAccess.DefaultTimeout);

                form.RegisterButtonForTest.PerformClick();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.CharCreateButtonForTest.Enabled,
                    ClientSmokeTestAccess.DefaultTimeout);

                form.LoginButtonForTest.PerformClick();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.StoredAuthTokenForTest is not null,
                    ClientSmokeTestAccess.DefaultTimeout);
                ClientSmokeTestAccess.SaveScreenshot(form, "01-login-token-stored.png");

                var charName = "SmokeHero";
                form.NewCharNameTextBoxForTest.Text = charName;
                form.CharCreateButtonForTest.PerformClick();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.CharactersComboForTest.Items.Count > 0,
                    ClientSmokeTestAccess.DefaultTimeout);
                ClientSmokeTestAccess.SaveScreenshot(form, "02-character-created.png");

                var pick = form.CharactersComboForTest.Items.Cast<object>()
                    .FirstOrDefault(i => i.ToString()?.Contains(charName, StringComparison.Ordinal) == true);
                Assert.NotNull(pick);
                form.CharactersComboForTest.SelectedItem = pick;
                var characterId = form.SelectedCharacterIdForTest;
                Assert.False(string.IsNullOrWhiteSpace(characterId));

                var invSvc = host.Services.GetRequiredService<InventoryGameplayService>();
                var charGuid = Guid.Parse(characterId!);
                var addResult = invSvc.TryAddItemAsync(charGuid, Phase7ContentSeed.DefaultWeaponId, 1)
                    .GetAwaiter()
                    .GetResult();
                Assert.Equal(InventoryMutationStatus.Ok, addResult.Status);

                form.EnterGameButtonForTest.PerformClick();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.IsPlayingPhaseForTest,
                    ClientSmokeTestAccess.DefaultTimeout);
                ClientSmokeTestAccess.PumpUntil(
                    () => form.InventoryPanelForTest.ListedItemCountForTest > 0,
                    ClientSmokeTestAccess.DefaultTimeout);

                form.InventoryPanelForTest.SelectFirstForTest();
                form.InventoryPanelForTest.ClickEquipForTest();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.InventoryPanelForTest.EquippedWeaponItemId is not null,
                    ClientSmokeTestAccess.DefaultTimeout);

                ClientSmokeTestAccess.SaveScreenshot(form, "03-gameplay-inventory.png");

                form.DisconnectButtonForTest.PerformClick();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.ConnectButtonForTest.Enabled,
                    ClientSmokeTestAccess.DefaultTimeout);

                form.ConnectButtonForTest.PerformClick();
                ClientSmokeTestAccess.PumpUntil(() => form.DisconnectButtonForTest.Enabled, ClientSmokeTestAccess.DefaultTimeout);
                form.ReconnectButtonForTest.PerformClick();
                ClientSmokeTestAccess.PumpUntil(
                    () => form.CharCreateButtonForTest.Enabled,
                    ClientSmokeTestAccess.DefaultTimeout);
                ClientSmokeTestAccess.SaveScreenshot(form, "04-reconnect-ok.png");

                var screenshotDir = ClientSmokeTestAccess.ScreenshotDirectory;
                Assert.True(Directory.Exists(screenshotDir), $"Missing screenshot dir: {screenshotDir}");
                foreach (var name in new[]
                {
                    "01-login-token-stored.png",
                    "02-character-created.png",
                    "03-gameplay-inventory.png",
                    "04-reconnect-ok.png",
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

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
