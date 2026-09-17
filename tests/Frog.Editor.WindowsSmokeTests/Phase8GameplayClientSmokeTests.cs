using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Frog.Client;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Config;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Phase 8 functional client smoke: real server packets and UI controls (no ApplyState injection).
/// Screenshots → artifacts/phase-08-gameplay-client/.
/// </summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class Phase8GameplayClientSmokeTests
{
    [Fact]
    public void Phase8Client_NetworkPanelsAndReconnect_Screenshots()
    {
        StaTestRunner.Run(() =>
        {
            using var harness = Phase8SmokeHarness.Create();
            var form = harness.Form;
            var opts = new Phase8SmokeBootstrapOptions();
            harness.ConnectRegisterLogin();
            harness.CreateCharacter("P8Hero");
            harness.EnterPlayingPhase("P8Hero");
            form.SelectPhase8TabForTest();
            Pump(
                form,
                () => form.IsPhase8TabSelectedForTest && form.DialoguePanelForTest.Width > 8,
                "phase 8 tab selected and laid out");
            Pump(
                form,
                () => form.DialoguePanelForTest.ChoiceButtonCountForTest > 0
                      && !string.Equals(form.DialoguePanelForTest.SpeakerTextForTest, "—", StringComparison.Ordinal),
                "dialogue push from server");
            Pump(
                form,
                () => form.EnvironmentPanelForTest.MapLabelTextForTest.Contains("Carte: 1", StringComparison.Ordinal),
                "environment push");
            AssertPhase8TabSurfaceLaidOut(form);
            WaitForPaint(form.GameplayTabsForTest);
            ClientSmokeTestAccess.SavePhase8Screenshot(form.GameplayTabsForTest, "01-phase8-tab.png");

            WaitForPaint(form.DialoguePanelForTest);
            ClientSmokeTestAccess.SavePhase8Screenshot(form.DialoguePanelForTest, "02-dialogue-choices.png");
            form.DialoguePanelForTest.ClickFirstChoiceForTest();
            Pump(form, () => form.LogContainsForTest("Journal quêtes:"), "quest journal after choice");
            Pump(form, () => form.QuestJournalPanelForTest.EntryCountForTest > 0, "quest journal entries");
            form.QuestJournalPanelForTest.SelectFirstForTest();
            WaitForPaint(form.QuestJournalPanelForTest);
            ClientSmokeTestAccess.SavePhase8Screenshot(form.QuestJournalPanelForTest, "03-quest-journal.png");

            Pump(
                form,
                () => form.EnvironmentPanelForTest.MapLabelTextForTest.Contains("Carte: 1", StringComparison.Ordinal),
                "environment push");
            WaitForPaint(form.EnvironmentPanelForTest);
            ClientSmokeTestAccess.SavePhase8Screenshot(form.EnvironmentPanelForTest, "04-environment.png");

            form.SelectGameplayTabForTest();
            Pump(form, () => form.ShopBuyButtonForTest.Enabled, "shop buy enabled on gameplay tab");
            form.ShopBuyButtonForTest.PerformClick();
            Pump(form, () => form.LogContainsForTest("Achat: Achat reussi"), "shop buy for craft ingredient");
            form.AcquireProfessionForTest(opts.ProfessionId);
            Pump(form, () => form.LogContainsForTest("Métier:") && form.LogContainsForTest("acquis"), "profession acquired");
            form.SelectPhase8TabForTest();
            Pump(form, () => form.IsPhase8TabSelectedForTest, "phase 8 tab after shop");
            form.CraftPanelForTest.RecipeIdTextBoxForTest.Text = opts.RecipeId.ToString();
            Pump(form, () => form.CraftPanelForTest.CraftButtonForTest.Enabled, "craft button enabled in playing phase");
            form.CraftPanelForTest.ClickCraftForTest();
            Pump(
                form,
                () => form.LogContainsForTest("Craft:") || form.CraftPanelForTest.StatusTextForTest.Contains("Craft"),
                "craft result");
            WaitForPaint(form.CraftPanelForTest);
            ClientSmokeTestAccess.SavePhase8Screenshot(form.CraftPanelForTest, "05-craft-panel.png");

            form.DisconnectForTest();
            Pump(form, () => form.ConnectButtonForTest.Enabled && form.ConnectButtonForTest.Visible, "disconnect complete");
            form.ConnectButtonForTest.PerformClick();
            Pump(form, () => form.DisconnectButtonForTest.Enabled || form.BackDisconnectButtonForTest.Enabled, "reconnect TCP");
            form.ReconnectButtonForTest.PerformClick();
            Pump(form, () => form.LogContainsForTest("Reconnect OK"), "reconnect success");
            Pump(form, () => form.CharCreateButtonForTest.Enabled && form.CharCreateButtonForTest.Visible, "reconnect auth restored");
            var pick = form.CharactersComboForTest.Items.Cast<object>()
                .FirstOrDefault(i => i.ToString()?.Contains("P8Hero", StringComparison.Ordinal) == true);
            Assert.NotNull(pick);
            form.CharactersComboForTest.SelectedItem = pick;
            form.EnterGameButtonForTest.PerformClick();
            Pump(form, () => form.IsPlayingPhaseForTest, "reconnect playing");
            form.SelectPhase8TabForTest();
            Pump(form, () => form.DialoguePanelForTest.ChoiceButtonCountForTest > 0, "dialogue after reconnect");
            AssertPhase8TabSurfaceLaidOut(form);
            WaitForPaint(form.GameplayTabsForTest);
            ClientSmokeTestAccess.SavePhase8Screenshot(form.GameplayTabsForTest, "06-reconnect-usable.png");

            AssertDistinctPhase8Screenshots();
        });
    }

    private static void Pump(MainShellForm form, Func<bool> predicate, string step)
    {
        try
        {
            ClientSmokeTestAccess.PumpUntil(predicate, ClientSmokeTestAccess.DefaultTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Phase 8 smoke timed out at '{step}': {ex.Message}", ex);
        }
    }

    private static void AssertPhase8TabSurfaceLaidOut(MainShellForm form)
    {
        var tabs = form.GameplayTabsForTest;
        Assert.True(
            tabs.Width >= 300 && tabs.Width <= 400 && tabs.Height >= 250 && tabs.Height <= 700,
            $"Gameplay TabControl screenshot surface not laid out as a tab crop ({tabs.Width}×{tabs.Height}); expected 300–400×250–700.");
    }

    private static void WaitForPaint(Control control)
    {
        control.PerformLayout();
        control.Refresh();
        for (var i = 0; i < 8; i++)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(15);
        }
    }

    private static void AssertDistinctPhase8Screenshots()
    {
        var names = new[]
        {
            "01-phase8-tab.png",
            "02-dialogue-choices.png",
            "03-quest-journal.png",
            "04-environment.png",
            "05-craft-panel.png",
            "06-reconnect-usable.png",
        };
        var dir = ClientSmokeTestAccess.Phase8ScreenshotDirectory;
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var path = Path.Combine(dir, name);
            Assert.True(File.Exists(path), $"Missing Phase 8 screenshot {path}");
            hashes[name] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        }

        Assert.NotEqual(hashes["01-phase8-tab.png"], hashes["02-dialogue-choices.png"]);
        Assert.NotEqual(hashes["03-quest-journal.png"], hashes["04-environment.png"]);
        var duplicates = hashes
            .GroupBy(kv => kv.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ← {string.Join(", ", g.Select(kv => kv.Key))}")
            .ToArray();
        Assert.True(
            duplicates.Length == 0,
            "Phase 8 client screenshots must be hash-distinct for claimed-different states: "
            + string.Join("; ", duplicates));
    }

    private sealed class Phase8SmokeHarness : IDisposable
    {
        private readonly IHost _host;
        private bool _disposed;

        private Phase8SmokeHarness(IHost host, MainShellForm form, string user, string password)
        {
            _host = host;
            Form = form;
            User = user;
            Password = password;
        }

        public MainShellForm Form { get; }

        public string User { get; }

        public string Password { get; }

        public static Phase8SmokeHarness Create()
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
                        ["Phase8Smoke:Enabled"] = "true",
                    });
                })
                .Build();
            host.Start();
            var phase8 = host.Services.GetRequiredService<Phase8InMemoryPublishedContent>();
            Phase8SmokeContentRegistrar.Register(phase8);
            var user = $"p8-{Guid.NewGuid():N}"[..18];
            const string password = "smoke-pass-8";
            var form = ClientSmokeTestAccess.CreateAndShowMainShell();
            form.HostTextBoxForTest.Text = "127.0.0.1";
            form.PortNumericForTest.Value = port;
            form.UserTextBoxForTest.Text = user;
            form.PassTextBoxForTest.Text = password;
            return new Phase8SmokeHarness(host, form, user, password);
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
            Pump(Form, () => Form.CatalogClassesPopulatedForTest, "catalog classes after login");
        }

        public void CreateCharacter(string name)
        {
            Form.NewCharNameTextBoxForTest.Text = name;
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

        public void EnterPlayingPhase(string name)
        {
            var pick = Form.CharactersComboForTest.Items.Cast<object>()
                .FirstOrDefault(i => i.ToString()?.Contains(name, StringComparison.Ordinal) == true);
            Assert.NotNull(pick);
            Form.CharactersComboForTest.SelectedItem = pick;
            Assert.False(string.IsNullOrWhiteSpace(Form.SelectedCharacterIdForTest));
            Form.EnterGameButtonForTest.PerformClick();
            Pump(Form, () => Form.IsPlayingPhaseForTest, "enter playing phase");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!Form.IsDisposed)
            {
                Form.DisconnectForTest();
                for (var i = 0; i < 30; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
            }

            ClientSmokeTestAccess.CloseMainShell(Form);
            try
            {
                _host.StopAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Phase 8 smoke host StopAsync failed.", ex);
            }

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
