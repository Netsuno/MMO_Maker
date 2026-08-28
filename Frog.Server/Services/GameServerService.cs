using System.Collections.Concurrent;
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
        private readonly object _clientTasksLock = new();
        private readonly List<Task> _clientTasks = new();
        private int _acceptingClients = 1;
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

            using var stopAcceptingRegistration = stoppingToken.Register(() =>
                Interlocked.Exchange(ref _acceptingClients, 0));

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var client = await _serverSocket.AcceptClientAsync(stoppingToken);
                    if (Volatile.Read(ref _acceptingClients) == 0)
                    {
                        client.Dispose();
                        continue;
                    }

                    var handlerTask = HandleClientAsync(new ClientSession(client), stoppingToken);
                    lock (_clientTasksLock)
                    {
                        _clientTasks.Add(handlerTask);
                    }

                    _ = handlerTask.ContinueWith(
                        static (task, state) =>
                        {
                            var self = (GameServerService)state!;
                            lock (self._clientTasksLock)
                            {
                                self._clientTasks.Remove(task);
                            }

                            if (task.IsFaulted && task.Exception is not null)
                            {
                                foreach (var ex in task.Exception.InnerExceptions)
                                {
                                    if (ex is OperationCanceledException)
                                    {
                                        continue;
                                    }

                                    GameServerLogs.ClientHandlerFaulted(self._log, ex);
                                }
                            }
                        },
                        this,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }
            catch (OperationCanceledException)
            {
                // arrêt normal
            }
            finally
            {
                Interlocked.Exchange(ref _acceptingClients, 0);
                if (_serverSocket is not null)
                {
                    await _serverSocket.DisposeAsync();
                }

                Task[] pending;
                lock (_clientTasksLock)
                {
                    pending = _clientTasks.ToArray();
                }

                await Task.WhenAll(pending.Select(AwaitHandlerObservingExceptions)).ConfigureAwait(false);

                GameServerLogs.ServerStopped(_log);
            }
        }

        private static async Task AwaitHandlerObservingExceptions(Task handlerTask)
        {
            try
            {
                await handlerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during host shutdown.
            }
        }

        private async Task HandleClientAsync(ClientSession clientSession, CancellationToken ct)
        {
            await using (clientSession)
            {
                ServerNetworkLogs.TcpClientConnected(_log, clientSession.ConnectionId, clientSession.RemoteEndPoint);

                try
                {
                    await _packetSender.SendHelloAsync(clientSession, ct);

                    while (!ct.IsCancellationRequested)
                    {
                        var hasFrame = await clientSession.TryReadFrameAsync(ct, async payload =>
                        {
                            try
                            {
                                await _packetDispatcher.DispatchAsync(clientSession, payload, ct);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                // Keep the TCP alive: a fan-out failure must not drop the sender.
                                _log.LogError(
                                    ex,
                                    "Dispatch failed connection={ConnectionId} remote={Remote}",
                                    clientSession.ConnectionId,
                                    clientSession.RemoteEndPoint);
                                try
                                {
                                    await _packetSender.SendErrorAsync(
                                        clientSession,
                                        "Erreur serveur lors du traitement du paquet.",
                                        ct);
                                }
                                catch
                                {
                                    // ignore secondary send failures
                                }
                            }
                        });

                        if (!hasFrame)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Host is shutting down gracefully (SIGTERM / FROG_SHUTDOWN_FILE / Ctrl+C via
                    // ConsoleLifetime): stop reading rather than let this fault the discarded
                    // per-client task. The `await using` above still closes the socket in an
                    // orderly fashion, and the session cleanup below still runs.
                }

                ServerNetworkLogs.TcpClientDisconnected(
                    _log,
                    clientSession.ConnectionId,
                    clientSession.RemoteEndPoint,
                    clientSession.Username ?? string.Empty);

                // Prefer the live session snapshot; if reconnect already nulled AuthenticatedSession
                // and Unregister'd, these are no-ops. If only AuthenticatedSession was cleared by a
                // buggy path, we still avoid leaving zombies when we can resolve the session id.
                if (clientSession.AuthenticatedSession is not null)
                {
                    var sessionId = clientSession.AuthenticatedSession.Id;
                    var s = clientSession.AuthenticatedSession;
                    var username = s.Username;
                    if (!string.IsNullOrWhiteSpace(s.CharacterId))
                    {
                        _playerStateStore.UpsertForCharacter(
                            s.CharacterId,
                            s.CurrentMapId,
                            s.PixelX,
                            s.PixelY);
                    }

                    _clientRegistry.Unregister(sessionId);
                    if (!ct.IsCancellationRequested)
                    {
                        // During a host-wide graceful shutdown every other client is being torn
                        // down concurrently; skip the fan-out (it would call SendFrameAsync with
                        // an already-cancelled token) but still finish local bookkeeping below.
                        await _playerLifecycleNotifier.NotifyPlayerLeftAsync(username, ct);
                    }

                    _connectionManager.RemoveSession(sessionId);
                }
            }
        }
    }
}
