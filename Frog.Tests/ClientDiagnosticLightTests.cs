using System;
using System.IO;
using Frog.Client.Assets;
using Frog.Client.Services;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Xunit;

namespace Frog.Tests;

/// <summary>Panneau diagnostic léger. Hello reste 11. Tuile TileAsset 48. Pas de nouveau format de paquet.</summary>
public sealed class ClientDiagnosticLightTests
{
    [Fact]
    public void Protocol_Stays11_AndTilePackStays48()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((ushort)1, FrogPackFormat.FormatVersion);
    }

    [Fact]
    public void OverlayText_ShowsHelloEndpointLinkTilesAndError()
    {
        var cached = TilePackSyncResult.FromCache(
            new TilePackManifest { Version = "1.4", ProtocolVersion = FrogWireProtocol.Version, TileSizePixels = 48 },
            tileCount: 3);
        var snapshot = ClientDiagnosticLight.Create(
            "192.0.2.10",
            6400,
            connected: true,
            connecting: false,
            rttMilliseconds: 15,
            cached,
            lastError: null);
        Assert.Equal((ushort)11, snapshot.ProtocolVersion);
        Assert.Equal(DiagnosticLinkKind.Connected, snapshot.Link);

        var text = ClientDiagnosticLight.Format(snapshot);
        Assert.Contains("Hello : 11", text, StringComparison.Ordinal);
        Assert.Contains("Serveur : 192.0.2.10:6400", text, StringComparison.Ordinal);
        Assert.Contains("Lien : connecté · 15 ms", text, StringComparison.Ordinal);
        Assert.Contains("Tuiles : en cache · 3 tuile(s) · 48 px · 1.4", text, StringComparison.Ordinal);
        Assert.Contains("Dernière erreur : aucune", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OverlayText_ConnectingHidesRtt_AndDisconnectedIsFrench()
    {
        var connecting = ClientDiagnosticLight.Create(
            "127.0.0.1",
            6000,
            connected: false,
            connecting: true,
            rttMilliseconds: 40,
            tilePack: null,
            lastError: null);
        Assert.Equal(DiagnosticLinkKind.Connecting, connecting.Link);
        Assert.Null(connecting.RttMilliseconds);
        var connectingText = ClientDiagnosticLight.Format(connecting);
        Assert.Contains("Hello : 11", connectingText, StringComparison.Ordinal);
        Assert.Contains("connexion en cours", connectingText, StringComparison.Ordinal);
        Assert.Contains("pas encore chargé · 48 px", connectingText, StringComparison.Ordinal);
        Assert.DoesNotContain("40 ms", connectingText, StringComparison.Ordinal);

        var down = ClientDiagnosticLight.Create("  ", 6000, false, false, 8, null, "délai dépassé");
        Assert.Equal(DiagnosticLinkKind.Disconnected, down.Link);
        Assert.Equal("—", down.Endpoint);
        var downText = ClientDiagnosticLight.Format(down);
        Assert.Contains("Lien : déconnecté", downText, StringComparison.Ordinal);
        Assert.Contains("Dernière erreur : délai dépassé", downText, StringComparison.Ordinal);

        var waiting = ClientDiagnosticLight.Create("jeu.exemple", 6000, true, false, null, null, null);
        Assert.Contains("connecté · RTT en attente", ClientDiagnosticLight.Format(waiting), StringComparison.Ordinal);

        Assert.Contains("téléchargé · 2 tuile(s) · 48 px", ClientDiagnosticLight.FormatTilePack(
            TilePackSyncResult.FromDownload(
                new TilePackManifest { Version = "9", ProtocolVersion = 11, TileSizePixels = 48 },
                2)), StringComparison.Ordinal);
        Assert.Contains("refusé · signature", ClientDiagnosticLight.FormatTilePack(
            TilePackSyncResult.Rejected("signature")), StringComparison.Ordinal);
        Assert.Contains("indisponible · hors ligne", ClientDiagnosticLight.FormatTilePack(
            TilePackSyncResult.Unavailable("hors ligne")), StringComparison.Ordinal);
    }

    [Fact]
    public void OverlayText_RedactsTokenAndPassword()
    {
        var secret = new string('c', 48);
        var text = ClientDiagnosticLight.Format(
            ClientDiagnosticLight.Create(
                "192.0.2.10",
                6000,
                connected: false,
                connecting: false,
                rttMilliseconds: null,
                tilePack: null,
                lastError: "échec " + secret + " mot secret-demo"),
            "secret-demo");
        Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-demo", text, StringComparison.Ordinal);
        Assert.Contains("***", text, StringComparison.Ordinal);
        Assert.Contains("Hello : 11", text, StringComparison.Ordinal);
    }

    [Fact]
    public void HeartbeatProbe_MeasuresAck_AndIgnoresUnmatched()
    {
        var probe = new HeartbeatRttProbe();
        var sent = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);
        probe.NoteAck(sent.AddMilliseconds(5));
        Assert.Null(probe.LastMilliseconds);

        probe.NoteSent(sent);
        probe.NoteAck(sent.AddMilliseconds(42));
        Assert.Equal(42, probe.LastMilliseconds);

        probe.NoteAck(sent.AddMilliseconds(99));
        Assert.Equal(42, probe.LastMilliseconds);

        probe.NoteSent(sent);
        probe.CancelPending();
        probe.NoteAck(sent.AddSeconds(3));
        Assert.Equal(42, probe.LastMilliseconds);

        probe.Clear();
        Assert.Null(probe.LastMilliseconds);
    }

    [Fact]
    public void Client_WiresF3Overlay_ClosedByDefault_AndKeepsHello11()
    {
        var root = RepoRoot();
        var overlay = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "DiagnosticOverlayPanel.cs"));
        Assert.Contains("Keys.F3", overlay, StringComparison.Ordinal);
        Assert.Contains("Visible = false", overlay, StringComparison.Ordinal);
        Assert.Contains("Text = \"Fermer\"", overlay, StringComparison.Ordinal);
        Assert.Contains("Diagnostic · F3", overlay, StringComparison.Ordinal);

        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        Assert.Contains("DiagnosticOverlayPanel.ToggleKey", shell, StringComparison.Ordinal);
        Assert.Contains("ToggleDiagnosticOverlay", shell, StringComparison.Ordinal);
        Assert.Contains("DiagnosticDismissRequested", shell, StringComparison.Ordinal);
        Assert.Contains("ClientDiagnosticLight.Create", shell, StringComparison.Ordinal);
        Assert.Contains("HeartbeatAckReceived += OnHeartbeatAck", shell, StringComparison.Ordinal);
        Assert.Contains("_rtt.NoteSent", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("FrogWireProtocol.Version = 12", shell, StringComparison.Ordinal);

        var help = File.ReadAllText(Path.Combine(root, "Frog.Client", "Forms", "HelpForm.cs"));
        Assert.Contains("F3 affiche ou masque le panneau diagnostic", help, StringComparison.Ordinal);
        Assert.Contains("paquet de tuiles", help, StringComparison.Ordinal);

        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        Assert.Contains("public const ushort Version = 11;", protocol, StringComparison.Ordinal);

        var light = File.ReadAllText(Path.Combine(root, "Frog.Client", "Services", "ClientDiagnosticLight.cs"));
        Assert.Contains("FrogWireProtocol.Version", light, StringComparison.Ordinal);
        Assert.Contains("TileAssetMetrics.TargetTileSizePixels", light, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", light, StringComparison.Ordinal);
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
