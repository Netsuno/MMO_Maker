using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Frog.Core.Security;
using Frog.Server.Config;

namespace Frog.Server.Network;

/// <summary>SslStream.AuthenticateAsServer après accept TCP, avant Hello / framing.</summary>
internal static class TlsServerTransport
{
    internal static bool ShouldWrap(ServerOptions options)
    {
        if (options.Tls.Mode != TlsTransportMode.Required)
        {
            return false;
        }

        return !(options.IsLoopbackBind && options.Tls.AllowCleartextLoopback);
    }

    internal static X509Certificate2 LoadCertificate(TlsOptions tls)
    {
        if (tls.HasPfxPath)
        {
            var password = Environment.GetEnvironmentVariable(TlsOptions.PfxPasswordEnvironmentVariable) ?? string.Empty;
            return CreateSslCertificate(File.ReadAllBytes(tls.PfxPath!), password);
        }

        if (tls.HasPemPaths)
        {
            using var pem = X509Certificate2.CreateFromPemFile(tls.CertificatePath!, tls.PrivateKeyPath!);
            return CreateSslCertificate(pem.Export(X509ContentType.Pfx), string.Empty);
        }

        throw new InvalidOperationException(
            "Server:Tls:Mode=Required requires CertificatePath+PrivateKeyPath or PfxPath.");
    }

    internal static async Task<Stream> WrapAfterAcceptAsync(
        TcpClient client,
        ServerOptions options,
        X509Certificate2? serverCertificate,
        CancellationToken cancellationToken)
    {
        var inner = client.GetStream();
        if (!ShouldWrap(options))
        {
            return inner;
        }

        if (serverCertificate is null)
        {
            throw new InvalidOperationException(
                "Server:Tls:Mode=Required is active but no certificate was loaded. No silent cleartext fallback.");
        }

        var ssl = new SslStream(inner, leaveInnerStreamOpen: false);
        try
        {
            var sslOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = serverCertificate,
                ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            };
            await ssl.AuthenticateAsServerAsync(sslOptions, cancellationToken).ConfigureAwait(false);
            return ssl;
        }
        catch
        {
            await ssl.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static X509Certificate2 CreateSslCertificate(byte[] pfx, string password)
    {
        try
        {
            return new X509Certificate2(
                pfx,
                password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return new X509Certificate2(pfx, password, X509KeyStorageFlags.Exportable);
        }
    }
}
