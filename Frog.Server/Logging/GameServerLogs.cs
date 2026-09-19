using Microsoft.Extensions.Logging;

namespace Frog.Server.Logging;

internal static class GameServerLogs
{
    public static void ServerStarted(ILogger logger, string BindAddress, int Port)
        => logger.LogInformation("Serveur démarré sur {BindAddress}:{Port}", BindAddress, Port);

    public static void ServerStopped(ILogger logger)
        => logger.LogInformation("Serveur arrêté.");

    public static void BindAddressInvalid(ILogger logger, string BindAddress)
        => logger.LogError("BindAddress invalide: {BindAddress}", BindAddress);

    public static void ClientHandlerFaulted(ILogger logger, Exception exception)
        => logger.LogError(exception, "Client handler task faulted unexpectedly.");

    public static void TlsRequired(ILogger logger, string certificateSource)
        => logger.LogInformation("TLS Required (SslStream AuthenticateAsServer before framing). cert={CertificateSource}", certificateSource);

    public static void TlsHandshakeFailed(ILogger logger, string remoteEndPoint, Exception exception)
        => logger.LogWarning(exception, "TLS handshake failed remote={RemoteEndPoint}", remoteEndPoint);
}
