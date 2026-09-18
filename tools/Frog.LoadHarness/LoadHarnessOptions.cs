using System.Security.Cryptography.X509Certificates;
using Frog.Core.Security;

namespace Frog.LoadHarness;

public sealed class LoadHarnessOptions
{
    public string Scenario { get; init; } = "mixed";
    public int Sessions { get; init; } = 25;
    public int HoldMilliseconds { get; init; } = 3000;
    public int ChatBurst { get; init; } = 12;
    public int MoveBurst { get; init; } = 80;
    public string? Host { get; init; }
    public int Port { get; init; }
    public bool SelfHost { get; init; } = true;
    public string? JsonOut { get; init; }
    public int ConnectTimeoutMs { get; init; } = 15_000;
    public int MaxParallelAuth { get; init; } = 8;

    /// <summary>Off (défaut, tests Phase 9 clair) ou Required. Pas de mode optionnel / AcceptAll.</summary>
    public TlsTransportMode TlsMode { get; init; } = TlsTransportMode.Off;

    /// <summary>SNI / nom vérifié. Requis si Mode=Required (défaut : Host ou localhost).</summary>
    public string? TlsTargetHost { get; init; }

    /// <summary>PEM CA de test confinée. Si vide en Required, ancre système (jamais AcceptAll).</summary>
    public string? TlsCaPath { get; init; }

    /// <summary>PEM feuille serveur pour --self-host Mode=Required.</summary>
    public string? TlsCertificatePath { get; init; }

    public string? TlsPrivateKeyPath { get; init; }

    public static LoadHarnessOptions Parse(string[] args)
    {
        var scenario = "mixed";
        var sessions = 25;
        var holdMs = 3000;
        var chatBurst = 12;
        var moveBurst = 80;
        string? host = null;
        var port = 0;
        var selfHost = true;
        string? jsonOut = null;
        var connectTimeoutMs = 15_000;
        var maxParallelAuth = 8;
        var tlsMode = TlsTransportMode.Off;
        string? tlsTargetHost = null;
        string? tlsCaPath = null;
        string? tlsCertificatePath = null;
        string? tlsPrivateKeyPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario":
                    scenario = Require(args, ref i, "--scenario").ToLowerInvariant();
                    break;
                case "--sessions":
                    sessions = int.Parse(Require(args, ref i, "--sessions"));
                    break;
                case "--hold-ms":
                    holdMs = int.Parse(Require(args, ref i, "--hold-ms"));
                    break;
                case "--chat-burst":
                    chatBurst = int.Parse(Require(args, ref i, "--chat-burst"));
                    break;
                case "--move-burst":
                    moveBurst = int.Parse(Require(args, ref i, "--move-burst"));
                    break;
                case "--host":
                    host = Require(args, ref i, "--host");
                    selfHost = false;
                    break;
                case "--port":
                    port = int.Parse(Require(args, ref i, "--port"));
                    break;
                case "--self-host":
                    selfHost = true;
                    break;
                case "--json-out":
                    jsonOut = Require(args, ref i, "--json-out");
                    break;
                case "--connect-timeout-ms":
                    connectTimeoutMs = int.Parse(Require(args, ref i, "--connect-timeout-ms"));
                    break;
                case "--max-parallel-auth":
                    maxParallelAuth = int.Parse(Require(args, ref i, "--max-parallel-auth"));
                    break;
                case "--tls-mode":
                    tlsMode = ParseTlsMode(Require(args, ref i, "--tls-mode"));
                    break;
                case "--tls-target-host":
                    tlsTargetHost = Require(args, ref i, "--tls-target-host");
                    break;
                case "--tls-ca-path":
                    tlsCaPath = Require(args, ref i, "--tls-ca-path");
                    break;
                case "--tls-cert-path":
                    tlsCertificatePath = Require(args, ref i, "--tls-cert-path");
                    break;
                case "--tls-key-path":
                    tlsPrivateKeyPath = Require(args, ref i, "--tls-key-path");
                    break;
                case "-h":
                case "--help":
                    throw new LoadHarnessHelpException();
                default:
                    throw new ArgumentException("unknown argument: " + args[i]);
            }
        }

        if (sessions is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(sessions), "sessions must be 1–500");
        }

        if (!selfHost && (string.IsNullOrWhiteSpace(host) || port is <= 0 or > 65535))
        {
            throw new ArgumentException("--host and --port are required when not --self-host");
        }

        var parsed = new LoadHarnessOptions
        {
            Scenario = scenario,
            Sessions = sessions,
            HoldMilliseconds = holdMs,
            ChatBurst = chatBurst,
            MoveBurst = moveBurst,
            Host = host,
            Port = port,
            SelfHost = selfHost,
            JsonOut = jsonOut,
            ConnectTimeoutMs = connectTimeoutMs,
            MaxParallelAuth = Math.Max(1, maxParallelAuth),
            TlsMode = tlsMode,
            TlsTargetHost = tlsTargetHost,
            TlsCaPath = tlsCaPath,
            TlsCertificatePath = tlsCertificatePath,
            TlsPrivateKeyPath = tlsPrivateKeyPath,
        };
        parsed.ValidateTls();
        return parsed;
    }

    public void ValidateTls()
    {
        if (TlsMode is not TlsTransportMode.Off and not TlsTransportMode.Required)
        {
            throw new InvalidOperationException(
                "LoadHarness TlsMode must be Off or Required (no silent cleartext fallback, no AcceptAll).");
        }

        if (TlsMode != TlsTransportMode.Required)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TlsTargetHost)
            && !SelfHost
            && (string.IsNullOrWhiteSpace(Host) || IPAddressLooksLike(Host)))
        {
            throw new InvalidOperationException("--tls-target-host is required when --tls-mode Required.");
        }

        if (string.IsNullOrWhiteSpace(ResolveTargetHost()))
        {
            throw new InvalidOperationException("--tls-target-host is required when --tls-mode Required.");
        }

        if (SelfHost
            && (string.IsNullOrWhiteSpace(TlsCertificatePath) || string.IsNullOrWhiteSpace(TlsPrivateKeyPath)))
        {
            throw new InvalidOperationException(
                "self-host --tls-mode Required requires --tls-cert-path and --tls-key-path (no silent cleartext fallback).");
        }

        if (!string.IsNullOrWhiteSpace(TlsCaPath) && !File.Exists(TlsCaPath))
        {
            throw new InvalidOperationException("TLS CA file not found: " + TlsCaPath);
        }
    }

    public string ResolveTargetHost()
    {
        if (!string.IsNullOrWhiteSpace(TlsTargetHost))
        {
            return TlsTargetHost;
        }

        if (!string.IsNullOrWhiteSpace(Host) && !IPAddressLooksLike(Host))
        {
            return Host;
        }

        return SelfHost ? "localhost" : Host ?? "";
    }

    public ClientTlsOptions CreateClientTlsOptions()
    {
        if (TlsMode == TlsTransportMode.Off)
        {
            return ClientTlsOptions.Off;
        }

        X509Certificate2Collection? roots = null;
        if (!string.IsNullOrWhiteSpace(TlsCaPath))
        {
            roots = new X509Certificate2Collection();
            roots.ImportFromPemFile(TlsCaPath);
        }

        return ClientTlsOptions.Required(ResolveTargetHost(), roots);
    }

    private static TlsTransportMode ParseTlsMode(string raw)
    {
        if (!Enum.TryParse(raw, ignoreCase: true, out TlsTransportMode mode)
            || mode is not TlsTransportMode.Off and not TlsTransportMode.Required)
        {
            throw new ArgumentException("--tls-mode must be Off or Required");
        }

        return mode;
    }

    private static bool IPAddressLooksLike(string host)
        => System.Net.IPAddress.TryParse(host, out _);

    private static string Require(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException(name + " requires a value");
        }

        return args[++i];
    }
}

public sealed class LoadHarnessHelpException : Exception
{
    public LoadHarnessHelpException()
        : base("usage")
    {
    }
}
