using System.Net;

namespace Frog.Server.Config
{
    public sealed class ServerOptions
    {
        public string BindAddress { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 6000;

        /// <summary>
        /// Required to bind anything other than IPv4/IPv6 loopback. Phase 9 has no TLS;
        /// a non-loopback bind is clear-text TCP. See SECURITY_MODEL.md.
        /// </summary>
        public bool AllowNonLoopbackBind { get; init; }

        public bool IsLoopbackBind =>
            IPAddress.TryParse(BindAddress, out var ip)
            && (IPAddress.IsLoopback(ip)
                || ip.Equals(IPAddress.Loopback)
                || ip.Equals(IPAddress.IPv6Loopback));

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
        }
    }
}
