using System.Net;
using Frog.Core.Security;

namespace Frog.Server.Config
{
    public sealed class ServerOptions
    {
        public string BindAddress { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 6000;

        /// <summary>
        /// Required to bind anything other than IPv4/IPv6 loopback. Without
        /// <see cref="TlsOptions.Mode"/> = Required and a certificate, a non-loopback bind
        /// is still clear-text TCP. See SECURITY_MODEL.md and Server:Tls.
        /// </summary>
        public bool AllowNonLoopbackBind { get; init; }

        /// <summary>Section <c>Server:Tls</c> (Mode=Off|Required, chemins cert, AllowCleartextLoopback).</summary>
        public TlsOptions Tls { get; init; } = new();

        public bool IsLoopbackBind =>
            IPAddress.TryParse(BindAddress, out var ip)
            && (IPAddress.IsLoopback(ip)
                || ip.Equals(IPAddress.Loopback)
                || ip.Equals(IPAddress.IPv6Loopback));

        /// <summary>
        /// Mode=Required est démarrable s'il y a un certificat, ou (loopback seulement)
        /// AllowCleartextLoopback. Jamais de clair silencieux sur un bind non-loopback.
        /// </summary>
        public bool IsTlsStartable
        {
            get
            {
                if (Tls.Mode != TlsTransportMode.Required)
                {
                    return true;
                }

                if (Tls.HasCertificateConfigured)
                {
                    return true;
                }

                return IsLoopbackBind && Tls.AllowCleartextLoopback;
            }
        }

        public void Validate()
        {
            if (Port is <= 0 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(Port), "Le port doit être entre 1 et 65535.");
            }

            if (!IPAddress.TryParse(BindAddress, out _))
            {
                throw new ArgumentException("BindAddress invalide.", nameof(BindAddress));
            }

            if (!IsLoopbackBind && !AllowNonLoopbackBind)
            {
                throw new InvalidOperationException(
                    "Non-loopback bind requires Server:AllowNonLoopbackBind=true. "
                    + "Phase 9 TCP is clear-text; keep the listener on 127.0.0.1 or terminate TLS in front of it. "
                    + "See docs/progress/phase-09-distribution-admin-hardening/SECURITY_MODEL.md.");
            }

            if (!IsTlsStartable)
            {
                throw new InvalidOperationException(
                    "Server:Tls:Mode=Required requires a certificate (CertificatePath+PrivateKeyPath or PfxPath). "
                    + "A non-loopback bind has no silent cleartext fallback. "
                    + "Loopback cleartext requires Server:Tls:AllowCleartextLoopback=true.");
            }
        }
    }
}
