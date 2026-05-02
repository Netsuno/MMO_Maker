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

    public static void ClientConnected(ILogger logger, string RemoteEndPoint)
        => logger.LogInformation("Client connecté: {RemoteEndPoint}", RemoteEndPoint);
}
