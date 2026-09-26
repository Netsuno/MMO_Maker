using System;
using System.Text;
using Frog.Client.Assets;
using Frog.Core.Constants;

namespace Frog.Client.Services;

/// <summary>Lien affiché par le panneau diagnostic. N’entre pas dans le Hello.</summary>
internal enum DiagnosticLinkKind
{
    Disconnected,
    Connecting,
    Connected,
}

/// <summary>Instantané léger (Hello, serveur, ping, tuiles, dernière erreur).</summary>
internal readonly record struct ClientDiagnosticSnapshot(
    ushort ProtocolVersion,
    string Endpoint,
    DiagnosticLinkKind Link,
    int? RttMilliseconds,
    string TilePackStatus,
    string? LastError);

/// <summary>
/// Texte du panneau diagnostic client. Affiche <see cref="FrogWireProtocol.Version"/>
/// et la taille tuile <see cref="TileAssetMetrics.TargetTileSizePixels"/> sans les modifier.
/// </summary>
internal static class ClientDiagnosticLight
{
    public static ClientDiagnosticSnapshot Create(
        string? host,
        int port,
        bool connected,
        bool connecting,
        int? rttMilliseconds,
        TilePackSyncResult? tilePack,
        string? lastError)
    {
        var link = connected
            ? DiagnosticLinkKind.Connected
            : connecting
                ? DiagnosticLinkKind.Connecting
                : DiagnosticLinkKind.Disconnected;
        int? rtt = link == DiagnosticLinkKind.Connected ? rttMilliseconds : null;
        return new ClientDiagnosticSnapshot(
            FrogWireProtocol.Version,
            FormatEndpoint(host, port),
            link,
            rtt,
            FormatTilePack(tilePack),
            string.IsNullOrWhiteSpace(lastError) ? null : lastError.Trim());
    }

    public static string Format(ClientDiagnosticSnapshot snapshot, params string?[] secrets)
    {
        var sb = new StringBuilder();
        sb.Append("Hello : ").Append(snapshot.ProtocolVersion).AppendLine();
        sb.Append("Serveur : ").Append(snapshot.Endpoint).AppendLine();
        sb.Append("Lien : ").Append(FormatLink(snapshot.Link, snapshot.RttMilliseconds)).AppendLine();
        sb.Append("Tuiles : ").Append(snapshot.TilePackStatus).AppendLine();
        sb.Append("Dernière erreur : ");
        sb.Append(string.IsNullOrWhiteSpace(snapshot.LastError) ? "aucune" : snapshot.LastError);
        return ClientDiagnostics.RedactSecrets(sb.ToString(), secrets);
    }

    public static string FormatEndpoint(string? host, int port)
    {
        var trimmed = (host ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "—";
        }

        if (port is < 1 or > 65535)
        {
            return trimmed;
        }

        return trimmed + ":" + port;
    }

    public static string FormatLink(DiagnosticLinkKind link, int? rttMilliseconds)
    {
        switch (link)
        {
            case DiagnosticLinkKind.Connecting:
                return "connexion en cours";
            case DiagnosticLinkKind.Connected when rttMilliseconds is int ms:
                return "connecté · " + ms + " ms";
            case DiagnosticLinkKind.Connected:
                return "connecté · RTT en attente";
            default:
                return "déconnecté";
        }
    }

    /// <summary>Statut seulement. Le format <c>.frogpack</c> et le côté 48 px ne changent pas.</summary>
    public static string FormatTilePack(TilePackSyncResult? result)
    {
        var size = TileAssetMetrics.TargetTileSizePixels;
        if (result is null)
        {
            return "pas encore chargé · " + size + " px";
        }

        switch (result.Kind)
        {
            case TilePackSyncKind.Cached:
                return "en cache · " + result.TileCount + " tuile(s) · " + size + " px" + VersionSuffix(result.Version);
            case TilePackSyncKind.Downloaded:
                return "téléchargé · " + result.TileCount + " tuile(s) · " + size + " px" + VersionSuffix(result.Version);
            case TilePackSyncKind.Rejected:
                return "refusé · " + Clip(result.Detail);
            default:
                return "indisponible · " + Clip(result.Detail);
        }
    }

    private static string VersionSuffix(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        return " · " + version.Trim();
    }

    private static string Clip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "—";
        }

        var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        const int max = 160;
        return flat.Length <= max ? flat : flat[..max] + "…";
    }
}

/// <summary>RTT d’un battement de cœur déjà sur le fil. Pas d’opcode nouveau.</summary>
internal sealed class HeartbeatRttProbe
{
    private long _sentTicks;

    public int? LastMilliseconds { get; private set; }

    public void NoteSent(DateTime utc) => _sentTicks = utc.Ticks;

    public void NoteAck(DateTime utc)
    {
        var sent = _sentTicks;
        if (sent == 0)
        {
            return;
        }

        _sentTicks = 0;
        var ms = (utc.Ticks - sent) / TimeSpan.TicksPerMillisecond;
        if (ms < 0)
        {
            ms = 0;
        }
        else if (ms > 60_000)
        {
            ms = 60_000;
        }

        LastMilliseconds = (int)ms;
    }

    public void CancelPending() => _sentTicks = 0;

    public void Clear()
    {
        _sentTicks = 0;
        LastMilliseconds = null;
    }
}
