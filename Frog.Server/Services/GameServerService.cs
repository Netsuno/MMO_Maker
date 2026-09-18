using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Frog.Server.Config;
using Frog.Server.Logging;
using Frog.Server.Network;
using Frog.Server.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    SessionTeardown sessionTeardown,
    ServerOpsMetrics opsMetrics)
        : BackgroundService
    {
        private readonly ILogger<GameServerService> _log = log;
        private readonly ServerOptions _options = options.Value;
        private readonly PacketSender _packetSender = packetSender;
        private readonly PacketDispatcher _packetDispatcher = packetDispatcher;
        private readonly SessionTeardown _sessionTeardown = sessionTeardown;
        private readonly ServerOpsMetrics _opsMetrics = opsMetrics;
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

            X509Certificate2? tlsCertificate = null;
            if (TlsServerTransport.ShouldWrap(_options))
            {
                tlsCertificate = TlsServerTransport.LoadCertificate(_options.Tls);
            }

            _serverSocket = new ServerSocket(ip, _options.Port);
            _serverSocket.Start();

            GameServerLogs.ServerStarted(_log, _options.BindAddress, _options.Port);
            if (tlsCertificate is not null)
            {
                var certSource = !string.IsNullOrWhiteSpace(_options.Tls.PfxPath)
                    ? _options.Tls.PfxPath!
                    : !string.IsNullOrWhiteSpace(_options.Tls.CertificatePath)
                        ? _options.Tls.CertificatePath!
                        : "<configured>";
                GameServerLogs.TlsRequired(_log, certSource);
            }

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

                    Stream? transport = null;
                    var remoteEndPoint = "<unknown>";
                    try
                    {
                        remoteEndPoint = client.Client?.RemoteEndPoint?.ToString() ?? "<unknown>";
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (SocketException)
                    {
                    }

                    try
                    {
                        transport = await TlsServerTransport
                            .WrapAfterAcceptAsync(client, _options, tlsCertificate, stoppingToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (
                        ClientNetworkExceptions.IsExpectedTermination(ex) || ex is AuthenticationException)
                    {
                        GameServerLogs.TlsHandshakeFailed(_log, remoteEndPoint, ex);
                        if (transport is not null)
                        {
                            await transport.DisposeAsync().ConfigureAwait(false);
                        }

                        client.Dispose();
                        continue;
                    }

                    var handlerTask = HandleClientAsync(new ClientSession(client, transport), stoppingToken);
                    _opsMetrics.RecordConnectionAccepted();
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
                                    if (ClientNetworkExceptions.IsExpectedTermination(ex))
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
                tlsCertificate?.Dispose();
            }
        }

        private static async Task AwaitHandlerObservingExceptions(Task handlerTask)
        {
            try
            {
                await handlerTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ClientNetworkExceptions.IsExpectedTermination(ex))
            {
                // Expected during host shutdown, peer disconnect, or displaced reconnect.
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
                                if (_opsMetrics.RecordIfPostgresError(ex))
                                {
                                    ServerNetworkLogs.PostgresError(
                                        _log,
                                        ex,
                                        clientSession.ConnectionId,
                                        clientSession.RemoteEndPoint);
                                }

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
                            if (clientSession.LastFrameRejectReason is { } rejectReason)
                            {
                                _opsMetrics.RecordConnectionRejected(rejectReason);
                                ServerNetworkLogs.ConnectionRejected(
                                    _log,
                                    clientSession.ConnectionId,
                                    clientSession.RemoteEndPoint,
                                    rejectReason);
                            }

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
                catch (Exception ex) when (ClientNetworkExceptions.IsExpectedTermination(ex))
                {
                    // Normal peer disconnect during read/send.
                }

                ServerNetworkLogs.TcpClientDisconnected(
                    _log,
                    clientSession.ConnectionId,
                    clientSession.RemoteEndPoint,
                    clientSession.Username ?? string.Empty);

                if (clientSession.AuthenticatedSession is not null)
                {
                    var options = ct.IsCancellationRequested
                        ? SessionTeardownOptions.HostShutdown
                        : SessionTeardownOptions.PeerDisconnect;
                    await _sessionTeardown
                        .TearDownAsync(clientSession.AuthenticatedSession.Id, options, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
