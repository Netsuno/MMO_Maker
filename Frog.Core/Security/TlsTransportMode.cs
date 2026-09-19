namespace Frog.Core.Security;

/// <summary>
/// Transport TLS in-process (P10-5 lot A). Pas de mode optionnel : le clair n'est jamais un repli silencieux.
/// </summary>
public enum TlsTransportMode
{
    /// <summary>TCP clair (défaut local / tests Phase 7–9).</summary>
    Off = 0,

    /// <summary>SslStream obligatoire. Échec si la poignée de main TLS ne peut pas aboutir.</summary>
    Required = 1,
}
