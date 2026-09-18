using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Frog.Core.Security;

/// <summary>SslStream.AuthenticateAsClient après Connect, avant tout framing.</summary>
public static class TlsClientAuthenticator
{
    public static async Task<Stream> WrapAfterConnectAsync(
        Stream inner,
        ClientTlsOptions options,
        string connectHost,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode == TlsTransportMode.Off)
        {
            return inner;
        }

        if (options.Mode != TlsTransportMode.Required)
        {
            throw new InvalidOperationException(
                $"Client:Tls:Mode={options.Mode} is not supported. Use Off or Required (no silent cleartext fallback).");
        }

        var targetHost = string.IsNullOrWhiteSpace(options.TargetHost) ? connectHost : options.TargetHost;
        if (string.IsNullOrWhiteSpace(targetHost))
        {
            throw new InvalidOperationException("Client:Tls:TargetHost is required when Mode=Required.");
        }

        var ssl = new SslStream(inner, leaveInnerStreamOpen: false);
        try
        {
            var authOptions = new SslClientAuthenticationOptions
            {
                TargetHost = targetHost,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = (_, cert, chain, errors) =>
                    TlsCertificateValidator.IsAcceptable(cert, chain, errors, targetHost, options.CustomTrustRoots),
            };

            await ssl.AuthenticateAsClientAsync(authOptions, cancellationToken).ConfigureAwait(false);
            return ssl;
        }
        catch
        {
            await ssl.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
