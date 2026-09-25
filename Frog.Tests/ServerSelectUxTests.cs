using System;
using System.IO;
using System.Net.Sockets;
using Frog.Client.Config;
using Frog.Client.Services;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>Sélecteur de serveur et causes d'échec (FR). Hello reste 11. Tuile TileAsset 48.</summary>
public sealed class ServerSelectUxTests
{
    [Fact]
    public void Protocol_Stays11_AndTileAssetStays48()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    [Fact]
    public void Messages_SplitAuthTimeoutVersionUnreachable()
    {
        Assert.Equal(
            PlayerFacingMessages.BadCredentials,
            PlayerFacingMessages.FromServerOrNetwork("Identifiants invalides."));
        Assert.Equal(ConnectionFailureKind.Auth, PlayerFacingMessages.ClassifyServer("Identifiants invalides."));
        Assert.Equal("identifiants", PlayerFacingMessages.Headline(ConnectionFailureKind.Auth));

        Assert.Equal(
            PlayerFacingMessages.TimedOut,
            PlayerFacingMessages.FromException(new TimeoutException("ignored")));
        Assert.Equal(
            ConnectionFailureKind.Timeout,
            PlayerFacingMessages.ClassifyException(new SocketException((int)SocketError.TimedOut)));
        Assert.Equal(
            PlayerFacingMessages.TimedOut,
            PlayerFacingMessages.FromServerOrNetwork("timeout while connecting"));

        Assert.Equal(
            PlayerFacingMessages.Unreachable,
            PlayerFacingMessages.FromException(new SocketException((int)SocketError.ConnectionRefused)));
        Assert.Equal(
            PlayerFacingMessages.Unreachable,
            PlayerFacingMessages.FromException(new SocketException((int)SocketError.HostNotFound)));
        Assert.Equal(
            ConnectionFailureKind.ConnectionLost,
            PlayerFacingMessages.ClassifyException(new SocketException((int)SocketError.ConnectionReset)));

        const string version =
            "Version protocole incompatible (serveur indique 10, ce client attend 11). Mettez à jour client et serveur ensemble.";
        Assert.Equal(ConnectionFailureKind.Version, PlayerFacingMessages.ClassifyServer(version));
        Assert.Equal(version, PlayerFacingMessages.FromServerOrNetwork(version));
        Assert.Equal("version incompatible", PlayerFacingMessages.Headline(ConnectionFailureKind.Version));
        Assert.Contains("Hello", PlayerFacingMessages.FromServerOrNetwork(
            "Hello serveur incomplet ou obsolète — mettez Frog.Server à jour (même dépôt que le client)."));

        Assert.Equal(
            PlayerFacingMessages.Unavailable,
            PlayerFacingMessages.FromException(new InvalidOperationException("craft")));
        Assert.Equal(
            PlayerFacingMessages.Maintenance,
            PlayerFacingMessages.FromServerOrNetwork("Serveur en maintenance. Reessayez plus tard."));
    }

    [Fact]
    public void Diagnostics_IncludeProtocolAndRedactFailure()
    {
        var secret = new string('b', 48);
        var report = ClientDiagnostics.Build(
            "10.3.0",
            "192.0.2.10",
            6400,
            "Off",
            null,
            "Login",
            false,
            "demo",
            "échec " + secret);
        Assert.Contains("Protocole fil: 11", report, StringComparison.Ordinal);
        Assert.Contains("192.0.2.10:6400", report, StringComparison.Ordinal);
        Assert.Contains("Dernier échec:", report, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, report, StringComparison.Ordinal);
        Assert.Contains("***", report, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedServers_RoundTrip_Dedupe_Cap_AndLegacyJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-servers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "client-settings.json");
        try
        {
            var fresh = new ClientSettingsStore(path).Load();
            Assert.Single(fresh.SavedServers);
            Assert.Equal("Local", fresh.SavedServers[0].Name);
            Assert.Equal("127.0.0.1", fresh.SavedServers[0].Host);
            Assert.Equal(6000, fresh.SavedServers[0].Port);

            Assert.False(SavedServerList.TryRemember(fresh, "  ", 6000, "x", out var missing));
            Assert.Equal(SavedServerList.MissingHostMessage, missing);

            Assert.True(SavedServerList.TryRemember(fresh, "192.0.2.20", 6100, "Bac à sable", out var added));
            Assert.Equal(string.Empty, added);
            Assert.True(SavedServerList.TryRemember(fresh, "192.0.2.20", 6100, "Bac", out _));
            Assert.Equal(2, fresh.SavedServers.Count);
            var bac = fresh.SavedServers[SavedServerList.IndexOf(fresh.SavedServers, "192.0.2.20", 6100)];
            Assert.Equal("Bac", bac.Name);
            Assert.Equal("Bac — 192.0.2.20:6100", bac.ToString());
            Assert.Equal("192.0.2.20", fresh.LastHost);
            Assert.Equal(6100, fresh.LastPort);

            var longName = new string('N', 40);
            Assert.True(SavedServerList.TryRemember(fresh, "192.0.2.21", 6101, longName, out _));
            var trimmed = fresh.SavedServers[SavedServerList.IndexOf(fresh.SavedServers, "192.0.2.21", 6101)];
            Assert.Equal(32, trimmed.Name.Length);

            for (var i = 0; i < 20; i++)
            {
                Assert.True(SavedServerList.TryRemember(fresh, "198.51.100." + i, 7000 + i, "S" + i, out _));
            }

            fresh.Normalize();
            Assert.Equal(SavedServerList.MaxEntries, fresh.SavedServers.Count);
            Assert.True(SavedServerList.IndexOf(fresh.SavedServers, "198.51.100.19", 7019) >= 0);
            Assert.Equal("198.51.100.19", fresh.LastHost);

            var store = new ClientSettingsStore(path);
            store.Save(fresh);
            var json = File.ReadAllText(path);
            Assert.Contains("198.51.100.19", json, StringComparison.Ordinal);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            var loaded = store.Load();
            Assert.Equal(SavedServerList.MaxEntries, loaded.SavedServers.Count);
            Assert.Equal("198.51.100.19", loaded.LastHost);
            var clone = loaded.Clone();
            clone.SavedServers[0].Name = "muté";
            Assert.NotEqual("muté", loaded.SavedServers[0].Name);

            File.WriteAllText(path, """
                {
                  "schemaVersion": 1,
                  "lastHost": "10.0.0.8",
                  "lastPort": 6123
                }
                """);
            var legacy = new ClientSettingsStore(path).Load();
            Assert.Equal("10.0.0.8", legacy.LastHost);
            Assert.Equal(6123, legacy.LastPort);
            Assert.Contains(legacy.SavedServers, row => row.Host == "10.0.0.8" && row.Port == 6123);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void Client_WiresPicker_Retry_AndKeepsHelloText()
    {
        var root = RepoRoot();
        var login = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "LoginShell.cs"));
        Assert.Contains("FieldLabel(\"Serveur\")", login, StringComparison.Ordinal);
        Assert.Contains("Ajouter", login, StringComparison.Ordinal);
        Assert.Contains("Réessayer", login, StringComparison.Ordinal);
        Assert.Contains("Options → Réseau", login, StringComparison.Ordinal);
        Assert.Contains("SetConnectDiagnostic", login, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version", login, StringComparison.Ordinal);

        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        Assert.Contains("RefreshServerListUi", shell, StringComparison.Ordinal);
        Assert.Contains("AddServerFromFields", shell, StringComparison.Ordinal);
        Assert.Contains("RetryAsync", shell, StringComparison.Ordinal);
        Assert.Contains("NoteConnectFailure", shell, StringComparison.Ordinal);
        Assert.Contains("_cmbServers", shell, StringComparison.Ordinal);
        Assert.Contains("Text = \"Connexion\"", shell, StringComparison.Ordinal);
        Assert.Contains("Keys.F9", shell, StringComparison.Ordinal);

        var net = File.ReadAllText(Path.Combine(root, "Frog.Client", "Network", "FrogGameClient.cs"));
        Assert.Contains("ConnectTimeout", net, StringComparison.Ordinal);
        Assert.Contains("TimeoutException", net, StringComparison.Ordinal);
        Assert.Contains("RejectProtocolAsync", net, StringComparison.Ordinal);
        Assert.Contains(
            "Version protocole incompatible (serveur indique {helloVer}, ce client attend {FrogWireProtocol.Version})",
            net,
            StringComparison.Ordinal);
        Assert.Contains("public const ushort Version = 11;", File.ReadAllText(
            Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs")), StringComparison.Ordinal);

        var help = File.ReadAllText(Path.Combine(root, "Frog.Client", "Forms", "HelpForm.cs"));
        Assert.Contains("Réessayer", help, StringComparison.Ordinal);
        Assert.Contains("version incompatible", help, StringComparison.Ordinal);
        Assert.Contains("Délai dépassé", help, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
