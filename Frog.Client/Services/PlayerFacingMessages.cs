using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using Frog.Core.Distribution;

namespace Frog.Client.Services;

/// <summary>Libellés joueur (FR) — jamais de jeton, mot de passe, ni dump technique.</summary>
internal static class PlayerFacingMessages
{
    public const string Ready = "Prêt.";
    public const string Connected = "Connecté au serveur.";
    public const string Unavailable = "Serveur indisponible. Vérifiez l'adresse et que le serveur est lancé.";
    public const string BadCertificate = "Certificat invalide. La connexion sécurisée a échoué.";
    public const string ConnectionLost = "Connexion interrompue.";
    public const string BadCredentials = "Identifiants incorrects.";
    public const string AlreadyConnected = "Ce compte est déjà connecté.";
    public const string RegistrationsClosed = "Les inscriptions sont fermées pour cette bêta.";
    public const string Maintenance = MaintenanceMessages.PlayerFacing;
    public const string SessionExpired = "Session expirée. Reconnectez-vous.";
    public const string LoggedIn = "Connexion acceptée.";
    public const string DiagnosticsCopied = "Diagnostics copiés (jeton et mot de passe exclus).";

    private static readonly Regex TokenLike = new("[A-Za-z0-9_-]{40,}", RegexOptions.Compiled);

    public static string FromException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException!)
        {
            if (current is AuthenticationException)
            {
                return BadCertificate;
            }

            if (current is SocketException socket)
            {
                return socket.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused
                        or SocketError.TimedOut
                        or SocketError.HostUnreachable
                        or SocketError.NetworkUnreachable
                        or SocketError.TryAgain
                        or SocketError.HostNotFound => Unavailable,
                    SocketError.ConnectionReset
                        or SocketError.ConnectionAborted
                        or SocketError.Shutdown => ConnectionLost,
                    _ => Unavailable,
                };
            }

            var text = current.Message ?? string.Empty;
            if (LooksLikeTls(text))
            {
                return BadCertificate;
            }
        }

        return Unavailable;
    }

    public static string FromServerOrNetwork(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ConnectionLost;
        }

        if (LooksLikeTls(raw))
        {
            return BadCertificate;
        }

        if (raw.Contains("protocole", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Hello", StringComparison.OrdinalIgnoreCase))
        {
            return Redact(raw);
        }

        if (raw.Contains("Identifiants", StringComparison.OrdinalIgnoreCase))
        {
            return BadCredentials;
        }

        if (raw.Contains("deja connecte", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("déjà connecté", StringComparison.OrdinalIgnoreCase))
        {
            return AlreadyConnected;
        }

        if (raw.Contains("Inscriptions fermees", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Inscriptions fermées", StringComparison.OrdinalIgnoreCase))
        {
            return RegistrationsClosed;
        }

        if (MaintenanceMessages.IsMaintenanceSignal(raw))
        {
            return Maintenance;
        }

        if (raw.Contains("Session invalide", StringComparison.OrdinalIgnoreCase))
        {
            return SessionExpired;
        }

        if (raw.Contains("Connexion interrompue", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Connexion fermée", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Connexion fermee", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionLost;
        }

        return Redact(raw);
    }

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return TokenLike.Replace(text.Replace('\r', ' ').Replace('\n', ' '), "***");
    }

    private static bool LooksLikeTls(string text) =>
        text.Contains("certificate", StringComparison.OrdinalIgnoreCase)
        || text.Contains("certificat", StringComparison.OrdinalIgnoreCase)
        || text.Contains("SSL", StringComparison.OrdinalIgnoreCase)
        || text.Contains("TLS", StringComparison.OrdinalIgnoreCase)
        || text.Contains("AuthenticateAsClient", StringComparison.OrdinalIgnoreCase)
        || text.Contains("RemoteCertificate", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Rapport diagnostics expurgé (jamais jeton / mot de passe).</summary>
internal static class ClientDiagnostics
{
    private static readonly Regex TokenLike = new("[A-Za-z0-9_-]{40,}", RegexOptions.Compiled);

    public static string Build(
        string version,
        string host,
        int port,
        string tlsMode,
        string? tlsTargetHost,
        string phase,
        bool connected,
        string? username)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FRoG — diagnostics (expurgés)");
        sb.AppendLine("Version client: " + version);
        sb.AppendLine("Protocole fil: " + Frog.Core.Constants.FrogWireProtocol.Version);
        sb.AppendLine("OS: " + Environment.OSVersion);
        sb.AppendLine(".NET: " + Environment.Version);
        sb.AppendLine("Hôte: " + host + ":" + port);
        sb.AppendLine("TLS: " + tlsMode + (string.IsNullOrWhiteSpace(tlsTargetHost) ? string.Empty : " SNI=" + tlsTargetHost));
        sb.AppendLine("Phase: " + phase);
        sb.AppendLine("Connecté: " + (connected ? "oui" : "non"));
        if (!string.IsNullOrWhiteSpace(username))
        {
            sb.AppendLine("Compte: " + username);
        }

        return RedactSecrets(sb.ToString());
    }

    public static string RedactSecrets(string text, params string?[] extraSecrets)
    {
        var result = text ?? string.Empty;
        foreach (var secret in extraSecrets)
        {
            if (!string.IsNullOrEmpty(secret) && secret.Length >= 3)
            {
                result = result.Replace(secret, "***", StringComparison.Ordinal);
            }
        }

        return TokenLike.Replace(result, "***");
    }
}
