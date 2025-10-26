using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Frog.Server.Config;
using Frog.Server.Logging; // <= on utilise nos méthodes [LoggerMessage]

namespace Frog.Server.Services
{
    /// <summary>
    /// Service serveur principal (stub réseau). Démarre un TcpListener et accepte les connexions.
    /// Chaque client reçoit un banner puis la connexion est fermée.
    /// </summary>
    public sealed class GameServerService(ILogger<GameServerService> log, IOptions<ServerOptions> options)
        : BackgroundService
    {
        private readonly ILogger<GameServerService> _log = log;
        private readonly ServerOptions _options = options.Value;
        private TcpListener? _listener;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _options.Validate();

            if (!IPAddress.TryParse(_options.BindAddress, out var ip))
            {
                GameServerLogs.BindAddressInvalid(_log, _options.BindAddress);
                throw new ArgumentException("BindAddress invalide.");
            }

            _listener = new TcpListener(ip, _options.Port);
            _listener.Start();

            GameServerLogs.ServerStarted(_log, _options.BindAddress, _options.Port);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!_listener.Pending())
                    {
                        await Task.Delay(50, stoppingToken);
                        continue;
                    }

                    var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    _ = HandleClientAsync(client, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // arrêt normal
            }
            finally
            {
                _listener?.Stop();
                GameServerLogs.ServerStopped(_log);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                var stream = client.GetStream();
                var remote = client.Client.RemoteEndPoint?.ToString() ?? "<unknown>";
                GameServerLogs.ClientConnected(_log, remote);

                var banner = Encoding.UTF8.GetBytes("FROG SERVER READY\r\n");
                await stream.WriteAsync(banner.AsMemory(), ct);
                await stream.FlushAsync(ct);
            }
        }
    }
}
