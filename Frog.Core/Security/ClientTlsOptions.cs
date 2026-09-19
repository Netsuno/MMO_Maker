using System.Security.Cryptography.X509Certificates;

namespace Frog.Core.Security;

/// <summary>Client:Tls — Mode=Off|Required et TargetHost (SNI / nom attendu).</summary>
public sealed class ClientTlsOptions
{
    public const string ModeEnvironmentVariable = "FROG_CLIENT_TLS_MODE";
    public const string TargetHostEnvironmentVariable = "FROG_CLIENT_TLS_TARGET_HOST";
    public const string CustomCaPathEnvironmentVariable = "FROG_CLIENT_TLS_CA_PATH";

    public static ClientTlsOptions Off { get; } = new();

    public TlsTransportMode Mode { get; init; } = TlsTransportMode.Off;

    /// <summary>Nom d'hôte présenté en SNI et vérifié sur le certificat. Défaut : hôte de connexion.</summary>
    public string? TargetHost { get; init; }

    /// <summary>Ancres CA supplémentaires (tests / CA privée). Jamais un bypass « tout accepter ».</summary>
    public X509Certificate2Collection? CustomTrustRoots { get; init; }

    public static ClientTlsOptions Required(string targetHost, X509Certificate2Collection? customTrustRoots = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);
        return new ClientTlsOptions
        {
            Mode = TlsTransportMode.Required,
            TargetHost = targetHost,
            CustomTrustRoots = customTrustRoots,
        };
    }

    public static ClientTlsOptions FromEnvironment(string connectHost)
    {
        var modeRaw = Environment.GetEnvironmentVariable(ModeEnvironmentVariable)
            ?? Environment.GetEnvironmentVariable("Client__Tls__Mode");
        var mode = TlsTransportMode.Off;
        if (!string.IsNullOrWhiteSpace(modeRaw)
            && Enum.TryParse(modeRaw, ignoreCase: true, out TlsTransportMode parsed)
            && parsed is TlsTransportMode.Off or TlsTransportMode.Required)
        {
            mode = parsed;
        }

        var target = Environment.GetEnvironmentVariable(TargetHostEnvironmentVariable)
            ?? Environment.GetEnvironmentVariable("Client__Tls__TargetHost");
        if (string.IsNullOrWhiteSpace(target))
        {
            target = connectHost;
        }

        X509Certificate2Collection? roots = null;
        var caPath = Environment.GetEnvironmentVariable(CustomCaPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(caPath) && File.Exists(caPath))
        {
            roots = new X509Certificate2Collection();
            roots.ImportFromPemFile(caPath);
        }

        return new ClientTlsOptions
        {
            Mode = mode,
            TargetHost = target,
            CustomTrustRoots = roots,
        };
    }
}
