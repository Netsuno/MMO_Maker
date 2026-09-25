using System;
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
    public const string Unreachable = "Serveur injoignable. Vérifiez l'adresse et que le serveur est lancé.";
    public const string TimedOut = "Délai dépassé. Le serveur ne répond pas.";
    public const string VersionMismatch = "Version incompatible. Mettez à jour le client et le serveur ensemble.";
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

    public static ConnectionFailureKind ClassifyException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException!)
        {
            if (current is AuthenticationException)
            {
                return ConnectionFailureKind.Certificate;
            }

            if (current is TimeoutException)
            {
                return ConnectionFailureKind.Timeout;
            }

            if (current is SocketException socket)
            {
                return socket.SocketErrorCode switch
                {
                    SocketError.TimedOut => ConnectionFailureKind.Timeout,
                    SocketError.ConnectionRefused
                        or SocketError.HostUnreachable
                        or SocketError.NetworkUnreachable
                        or SocketError.TryAgain
                        or SocketError.HostNotFound => ConnectionFailureKind.Unreachable,
                    SocketError.ConnectionReset
                        or SocketError.ConnectionAborted
                        or SocketError.Shutdown => ConnectionFailureKind.ConnectionLost,
                    _ => ConnectionFailureKind.Unreachable,
                };
            }

            var text = current.Message ?? string.Empty;
            if (LooksLikeTls(text))
            {
                return ConnectionFailureKind.Certificate;
            }
        }

        return ConnectionFailureKind.Other;
    }

    public static string FromException(Exception ex) => Present(ClassifyException(ex));

    public static ConnectionFailureKind ClassifyServer(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ConnectionFailureKind.ConnectionLost;
        }

        if (LooksLikeTls(raw))
        {
            return ConnectionFailureKind.Certificate;
        }

        if (raw.Contains("protocole", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Hello", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("version incompatible", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.Version;
        }

        if (raw.Contains("Identifiants", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.Auth;
        }

        if (raw.Contains("deja connecte", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("déjà connecté", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.AlreadyConnected;
        }

        if (raw.Contains("Inscriptions fermees", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Inscriptions fermées", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.Other;
        }

        if (MaintenanceMessages.IsMaintenanceSignal(raw))
        {
            return ConnectionFailureKind.Maintenance;
        }

        if (raw.Contains("Session invalide", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.SessionExpired;
        }

        if (raw.Contains("Délai dépassé", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Delai depasse", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.Timeout;
        }

        if (raw.Contains("injoignable", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("indisponible", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.Unreachable;
        }

        if (raw.Contains("Connexion interrompue", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Connexion fermée", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Connexion fermee", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionFailureKind.ConnectionLost;
        }

        return ConnectionFailureKind.Other;
    }

    public static string FromServerOrNetwork(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ConnectionLost;
        }

        return ClassifyServer(raw) switch
        {
            ConnectionFailureKind.Certificate => BadCertificate,
            ConnectionFailureKind.Version => Redact(raw),
            ConnectionFailureKind.Auth => BadCredentials,
            ConnectionFailureKind.AlreadyConnected => AlreadyConnected,
            ConnectionFailureKind.Maintenance => Maintenance,
            ConnectionFailureKind.SessionExpired => SessionExpired,
            ConnectionFailureKind.ConnectionLost => ConnectionLost,
            ConnectionFailureKind.Timeout => TimedOut,
            ConnectionFailureKind.Unreachable => Unreachable,
            _ when raw.Contains("Inscriptions fermees", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("Inscriptions fermées", StringComparison.OrdinalIgnoreCase) => RegistrationsClosed,
            _ => Redact(raw),
        };
    }

    public static string Present(ConnectionFailureKind kind) => kind switch
    {
        ConnectionFailureKind.Auth => BadCredentials,
        ConnectionFailureKind.Timeout => TimedOut,
        ConnectionFailureKind.Version => VersionMismatch,
        ConnectionFailureKind.Unreachable => Unreachable,
        ConnectionFailureKind.Certificate => BadCertificate,
        ConnectionFailureKind.AlreadyConnected => AlreadyConnected,
        ConnectionFailureKind.Maintenance => Maintenance,
        ConnectionFailureKind.SessionExpired => SessionExpired,
        ConnectionFailureKind.ConnectionLost => ConnectionLost,
        _ => Unavailable,
    };

    /// <summary>Libellé court du bandeau diagnostic (la phrase complète reste dans le statut).</summary>
    public static string Headline(ConnectionFailureKind kind) => kind switch
    {
        ConnectionFailureKind.Auth => "identifiants",
        ConnectionFailureKind.Timeout => "délai dépassé",
        ConnectionFailureKind.Version => "version incompatible",
        ConnectionFailureKind.Unreachable => "serveur injoignable",
        ConnectionFailureKind.Certificate => "certificat",
        ConnectionFailureKind.AlreadyConnected => "compte déjà connecté",
        ConnectionFailureKind.Maintenance => "maintenance",
        ConnectionFailureKind.SessionExpired => "session expirée",
        ConnectionFailureKind.ConnectionLost => "connexion interrompue",
        _ => "erreur",
    };

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

/// <summary>Cause affichée au joueur. Le fil Hello ne change pas.</summary>
internal enum ConnectionFailureKind
{
    None,
    Auth,
    Timeout,
    Version,
    Unreachable,
    Certificate,
    AlreadyConnected,
    Maintenance,
    SessionExpired,
    ConnectionLost,
    Other,
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
        string? username,
        string? lastFailure = null)
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
        sb.AppendLine("Dernier échec: " + (string.IsNullOrWhiteSpace(lastFailure) ? "aucun" : lastFailure));
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
