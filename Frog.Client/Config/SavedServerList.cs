using System;
using System.Collections.Generic;

namespace Frog.Client.Config;

/// <summary>Point d'accès TCP mémorisé. Jamais de compte ni de mot de passe.</summary>
public sealed class SavedServerEndpoint
{
    public const int MaxNameLength = 32;

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 6000;

    public string EndpointText => Host + ":" + Port;

    public override string ToString() =>
        string.IsNullOrEmpty(Name) ? EndpointText : Name + " — " + EndpointText;

    public SavedServerEndpoint Copy() => new()
    {
        Name = Name,
        Host = Host,
        Port = Port,
    };
}

/// <summary>
/// Liste locale des serveurs (écran de connexion). L'hôte/port courant reste
/// <see cref="UserSettings.LastHost"/> / <see cref="UserSettings.LastPort"/>.
/// </summary>
public static class SavedServerList
{
    public const int MaxEntries = 12;

    public const string LocalName = "Local";

    public const string MissingHostMessage = "Indiquez un hôte.";

    public const string HostTooLongMessage = "Hôte trop long.";

    public static void Normalize(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var incoming = settings.SavedServers ?? new List<SavedServerEndpoint>();
        var cleaned = new List<SavedServerEndpoint>();
        foreach (var row in incoming)
        {
            if (!TrySanitize(row?.Host, row?.Port ?? 0, row?.Name, out var host, out var port, out var name, out _))
            {
                continue;
            }

            Push(cleaned, host, port, name, overwriteName: name.Length > 0);
        }

        if (!Push(cleaned, settings.LastHost, settings.LastPort, DefaultName(settings.LastHost, settings.LastPort), overwriteName: false)
            && cleaned.Count == 0)
        {
            Push(cleaned, "127.0.0.1", 6000, LocalName, overwriteName: true);
        }

        TrimOldest(cleaned, settings.LastHost, settings.LastPort);
        settings.SavedServers = cleaned;
    }

    /// <summary>Ajoute ou met à jour l'adresse et la sélectionne. Un nom vide conserve le libellé déjà enregistré.</summary>
    public static bool TryRemember(UserSettings settings, string? host, int port, string? name, out string error)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!TrySanitize(host, port, name, out var cleanHost, out var cleanPort, out var cleanName, out error))
        {
            return false;
        }

        settings.SavedServers ??= new List<SavedServerEndpoint>();
        Push(settings.SavedServers, cleanHost, cleanPort, cleanName, overwriteName: cleanName.Length > 0);
        settings.LastHost = cleanHost;
        settings.LastPort = cleanPort;
        TrimOldest(settings.SavedServers, cleanHost, cleanPort);
        error = string.Empty;
        return true;
    }

    public static int IndexOf(IReadOnlyList<SavedServerEndpoint>? servers, string? host, int port)
    {
        if (servers is null || !TrySanitize(host, port, null, out var cleanHost, out var cleanPort, out _, out _))
        {
            return -1;
        }

        for (var i = 0; i < servers.Count; i++)
        {
            var row = servers[i];
            if (row is not null
                && row.Port == cleanPort
                && string.Equals(row.Host, cleanHost, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Push(
        List<SavedServerEndpoint> servers,
        string host,
        int port,
        string name,
        bool overwriteName)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var index = IndexOf(servers, host, port);
        SavedServerEndpoint row;
        if (index >= 0)
        {
            row = servers[index];
            servers.RemoveAt(index);
            if (overwriteName)
            {
                row.Name = name;
            }
        }
        else
        {
            row = new SavedServerEndpoint();
            if (name.Length > 0)
            {
                row.Name = name;
            }
            else
            {
                row.Name = DefaultName(host, port);
            }
        }

        row.Host = host;
        row.Port = port;
        servers.Add(row);
        return true;
    }

    private static void TrimOldest(List<SavedServerEndpoint> servers, string host, int port)
    {
        while (servers.Count > MaxEntries)
        {
            servers.RemoveAt(0);
        }

        if (IndexOf(servers, host, port) >= 0 || string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        if (servers.Count == MaxEntries)
        {
            servers.RemoveAt(0);
        }

        servers.Add(new SavedServerEndpoint
        {
            Name = DefaultName(host, port),
            Host = host,
            Port = port,
        });
    }

    private static bool TrySanitize(
        string? host,
        int port,
        string? name,
        out string cleanHost,
        out int cleanPort,
        out string cleanName,
        out string error)
    {
        cleanHost = (host ?? string.Empty).Trim();
        cleanPort = Math.Clamp(port <= 0 ? 6000 : port, 1, 65535);
        cleanName = SanitizeName(name);
        if (cleanHost.Length == 0 || ContainsWhitespace(cleanHost))
        {
            error = MissingHostMessage;
            cleanHost = string.Empty;
            return false;
        }

        if (cleanHost.Length > 253)
        {
            error = HostTooLongMessage;
            cleanHost = string.Empty;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string SanitizeName(string? name)
    {
        var text = (name ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length > SavedServerEndpoint.MaxNameLength)
        {
            text = text[..SavedServerEndpoint.MaxNameLength].Trim();
        }

        return text;
    }

    private static string DefaultName(string host, int port) =>
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) && port == 6000
            ? LocalName
            : string.Empty;

    private static bool ContainsWhitespace(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                return true;
            }
        }

        return false;
    }
}
