using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Frog.Server.Config;
using Frog.Server.Network;
using Frog.Server.Logging; // <= on utilise nos méthodes [LoggerMessage]
using Frog.Server.Persistence;

namespace Frog.Server.Services
{
    /// <summary>
    /// Service serveur principal (stub réseau). Démarre un TcpListener et accepte les connexions.
    /// Chaque client reçoit un banner puis la connexion est fermée.
    /// </summary>
public sealed class GameServerService(
    ILogger<GameServerService> log,
    IOptions<ServerOptions> options,
    PacketSender packetSender,
    PacketDispatcher packetDispatcher,
    ConnectionManager connectionManager,
    ClientRegistry clientRegistry,
    PlayerLifecycleNotifier playerLifecycleNotifier,
    IPlayerStateStore playerStateStore)
        : BackgroundService
    {
        private readonly ILogger<GameServerService> _log = log;
        private readonly ServerOptions _options = options.Value;
        private readonly PacketSender _packetSender = packetSender;
        private readonly PacketDispatcher _packetDispatcher = packetDispatcher;
        private readonly ConnectionManager _connectionManager = connectionManager;
        private readonly ClientRegistry _clientRegistry = clientRegistry;
        private readonly PlayerLifecycleNotifier _playerLifecycleNotifier = playerLifecycleNotifier;
        private readonly IPlayerStateStore _playerStateStore = playerStateStore;
        private ServerSocket? _serverSocket;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _options.Validate();

            if (!IPAddress.TryParse(_options.BindAddress, out var ip))
            {
                GameServerLogs.BindAddressInvalid(_log, _options.BindAddress);
                throw new ArgumentException("BindAddress invalide.");
            }

            _serverSocket = new ServerSocket(ip, _options.Port);
            _serverSocket.Start();

            GameServerLogs.ServerStarted(_log, _options.BindAddress, _options.Port);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var client = await _serverSocket.AcceptClientAsync(stoppingToken);
                    _ = HandleClientAsync(new ClientSession(client), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // arrêt normal
            }
            finally
            {
                if (_serverSocket is not null)
                {
                    await _serverSocket.DisposeAsync();
                }

                GameServerLogs.ServerStopped(_log);
            }
        }

        private async Task HandleClientAsync(ClientSession clientSession, CancellationToken ct)
        {
            await using (clientSession)
            {
                ServerNetworkLogs.TcpClientConnected(_log, clientSession.ConnectionId, clientSession.RemoteEndPoint);
                await _packetSender.SendHelloAsync(clientSession, ct);

                while (!ct.IsCancellationRequested)
                {
                    var hasFrame = await clientSession.TryReadFrameAsync(ct, async payload =>
                    {
                        await _packetDispatcher.DispatchAsync(clientSession, payload, ct);
                    });

                    if (!hasFrame)
                    {
                        break;
                    }
                }

                ServerNetworkLogs.TcpClientDisconnected(
                    _log,
                    clientSession.ConnectionId,
                    clientSession.RemoteEndPoint,
                    clientSession.Username ?? string.Empty);

                if (clientSession.AuthenticatedSession is not null)
                {
                    var sessionId = clientSession.AuthenticatedSession.Id;
                    var username = clientSession.AuthenticatedSession.Username;
                    var s = clientSession.AuthenticatedSession;
                    _playerStateStore.Upsert(username, s.CurrentMapId, s.PositionX, s.PositionY);
                    _clientRegistry.Unregister(sessionId);
                    await _playerLifecycleNotifier.NotifyPlayerLeftAsync(username, ct);
                    _connectionManager.RemoveSession(sessionId);
                }
            }
        }
    }
}
