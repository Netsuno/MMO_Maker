namespace Frog.Server.Config;

/// <summary>
/// Server:Tls — Mode=Off|Required, PEM (CertificatePath+PrivateKeyPath) ou PFX + env
/// <c>FROG_TLS_PFX_PASSWORD</c>. AllowCleartextLoopback n'est pas un repli silencieux.
/// </summary>
public sealed class TlsOptions
{
    public const string PfxPasswordEnvironmentVariable = "FROG_TLS_PFX_PASSWORD";

    public Frog.Core.Security.TlsTransportMode Mode { get; set; }

    public string? CertificatePath { get; set; }

    public string? PrivateKeyPath { get; set; }

    public string? PfxPath { get; set; }

    /// <summary>
    /// Opt-in explicite : Mode=Required peut rester en clair uniquement sur un bind loopback.
    /// Ignoré (et non autorisé) sur un bind non-loopback.
    /// </summary>
    public bool AllowCleartextLoopback { get; set; }

    public bool HasPemPaths =>
        !string.IsNullOrWhiteSpace(CertificatePath) && !string.IsNullOrWhiteSpace(PrivateKeyPath);

    public bool HasPfxPath => !string.IsNullOrWhiteSpace(PfxPath);

    public bool HasCertificateConfigured => HasPemPaths || HasPfxPath;
}
