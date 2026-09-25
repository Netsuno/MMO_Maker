using System;
using System.IO;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Liste de serveurs visible, hôte/port toujours hors carte tant que F9 est fermé, réessai après échec.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class ClientServerSelectSmokeTests
{
    [Fact]
    public void Login_ShowsServerList_HidesHostUntilF9_RetryAfterFailure()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-server-select-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                Assert.True(form.ServerListComboForTest.Visible);
                Assert.Contains("127.0.0.1:6000", form.ServerListComboForTest.Text, StringComparison.Ordinal);
                Assert.Equal("Connecter", form.ConnectButtonForTest.Text);
                Assert.Equal("Réessayer", form.RetryConnectButtonForTest.Text);
                Assert.False(form.RetryConnectButtonForTest.Enabled);
                Assert.False(form.HostTextBoxForTest.Visible, "host leaves the player card");
                Assert.False(form.PortNumericForTest.Visible, "port leaves the player card");
                Assert.False(form.AddServerButtonForTest.Visible);
                Assert.Contains("Protocole " + FrogWireProtocol.Version, form.ConnectDiagnosticTextForTest, StringComparison.Ordinal);
                Assert.Contains("prêt", form.ConnectDiagnosticTextForTest, StringComparison.Ordinal);
                Assert.Contains("127.0.0.1:6000", form.ConnectDiagnosticTextForTest, StringComparison.Ordinal);

                form.ToggleLoginOpsForTest();
                Assert.True(form.HostTextBoxForTest.Visible);
                Assert.True(form.PortNumericForTest.Visible);
                Assert.True(form.AddServerButtonForTest.Visible);
                Assert.Equal("Ajouter", form.AddServerButtonForTest.Text);

                form.ServerNameTextBoxForTest.Text = "Bac à sable";
                form.HostTextBoxForTest.Text = "192.0.2.20";
                form.PortNumericForTest.Value = 6100;
                form.AddServerButtonForTest.PerformClick();
                Assert.True(form.ServerListComboForTest.Items.Count >= 2);
                Assert.Contains("Bac à sable", form.ServerListComboForTest.Text, StringComparison.Ordinal);
                Assert.Contains("192.0.2.20:6100", form.ServerListComboForTest.Text, StringComparison.Ordinal);
                Assert.Equal("192.0.2.20", form.HostTextBoxForTest.Text);
                Assert.Equal(6100, (int)form.PortNumericForTest.Value);
                Assert.Contains("Serveur enregistré.", form.PlayerStatusTextForTest, StringComparison.Ordinal);
                var json = File.ReadAllText(path);
                Assert.Contains("192.0.2.20", json, StringComparison.Ordinal);
                Assert.Contains("Bac", json, StringComparison.Ordinal);
                Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);

                var localIndex = -1;
                for (var i = 0; i < form.ServerListComboForTest.Items.Count; i++)
                {
                    if (form.ServerListComboForTest.Items[i] is SavedServerEndpoint row
                        && row.Host == "127.0.0.1"
                        && row.Port == 6000)
                    {
                        localIndex = i;
                        break;
                    }
                }

                Assert.True(localIndex >= 0);
                form.ServerListComboForTest.SelectedIndex = localIndex;
                Assert.Equal("127.0.0.1", form.HostTextBoxForTest.Text);
                Assert.Equal(6000, (int)form.PortNumericForTest.Value);

                form.NoteConnectFailureForTest(
                    "Version protocole incompatible (serveur indique 10, ce client attend 11). Mettez à jour client et serveur ensemble.");
                Assert.True(form.RetryConnectButtonForTest.Enabled);
                Assert.Contains("version incompatible", form.ConnectDiagnosticTextForTest, StringComparison.Ordinal);
                Assert.Contains("protocole " + FrogWireProtocol.Version, form.ConnectDiagnosticTextForTest, StringComparison.Ordinal);
                Assert.Contains("Version protocole incompatible", form.PlayerStatusTextForTest, StringComparison.Ordinal);
                Assert.Contains("11", form.PlayerStatusTextForTest, StringComparison.Ordinal);

                form.NoteConnectFailureForTest("Identifiants invalides.");
                Assert.Contains("Identifiants incorrects.", form.PlayerStatusTextForTest, StringComparison.Ordinal);
                Assert.Contains("identifiants", form.ConnectDiagnosticTextForTest, StringComparison.Ordinal);
                Assert.True(form.RetryConnectButtonForTest.Enabled);
                Assert.True(form.ConnectButtonForTest.Enabled);
            }
            finally
            {
                if (form is not null)
                {
                    ClientSmokeTestAccess.CloseMainShell(form);
                }

                Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, previous);
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        });
    }
}
