using System.Net;
using System.Text;

using Frog.Server.Config;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server.Content;

/// <summary>HttpListener loopback (par défaut) pour le canal contenu. Aucun effet si <c>TilePack:Enabled</c> est faux.</summary>
public sealed class TilePackContentHostedService : IHostedService
{
    private readonly TilePackContentHttp _http;
    private readonly IOptions<TilePackOptions> _options;
    private readonly ILogger<TilePackContentHostedService> _logger;
    private TilePackContentServer? _server;

    public TilePackContentHostedService(
        TilePackContentHttp http,
        IOptions<TilePackOptions> options,
        ILogger<TilePackContentHostedService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Canal HTTP tile pack désactivé (TilePack:Enabled=false). Hello reste 11.");
            return Task.CompletedTask;
        }

        _server = TilePackContentServer.Start(_http, options.BindAddress, options.Port);
        _logger.LogInformation(
            "Canal HTTP tile pack sur http://{Bind}:{Port}/content/tile-packs/current (Hello TCP 11 inchangé).",
            options.BindAddress,
            options.Port);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server is not null)
        {
            await _server.DisposeAsync().ConfigureAwait(false);
            _server = null;
        }
    }
}

public sealed class TilePackContentServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    private TilePackContentServer(HttpListener listener, TilePackContentHttp http)
    {
        _listener = listener;
        _loop = Task.Run(() => ListenAsync(http, _stop.Token));
    }

    public static TilePackContentServer Start(TilePackContentHttp http, string bindAddress, int port)
    {
        ArgumentNullException.ThrowIfNull(http);
        var listener = new HttpListener();
        listener.Prefixes.Add("http://" + bindAddress + ":" + port + "/");
        listener.Start();
        return new TilePackContentServer(listener, http);
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Close();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Arrêt demandé.
        }
    }

    private async Task ListenAsync(TilePackContentHttp http, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => WriteAsync(http, context), CancellationToken.None);
        }
    }

    private static async Task WriteAsync(TilePackContentHttp http, HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            byte[] body = Array.Empty<byte>();
            if (request.HasEntityBody)
            {
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await request.InputStream.ReadAsync(chunk).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + read > TilePackPublishService.MaxPackBytes)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                        context.Response.Close();
                        return;
                    }

                    buffer.Write(chunk, 0, read);
                }

                body = buffer.ToArray();
            }

            var path = request.Url?.PathAndQuery ?? request.RawUrl ?? "/";
            var result = await http.HandleAsync(
                request.HttpMethod,
                path,
                request.ContentType,
                request.Headers[TilePackContentHttp.AdminHeaderName],
                body,
                request.Headers["If-None-Match"]).ConfigureAwait(false);

            var response = context.Response;
            response.StatusCode = (int)result.StatusCode;
            response.ContentType = result.ContentType;
            response.SendChunked = false;
            if (!string.IsNullOrEmpty(result.ETag))
            {
                response.Headers["ETag"] = "\"" + result.ETag + "\"";
            }

            if (!string.IsNullOrEmpty(result.ContentDisposition))
            {
                response.Headers["Content-Disposition"] = result.ContentDisposition;
            }

            if (result.ContentType.StartsWith("application/octet-stream", StringComparison.Ordinal))
            {
                response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            }
            else
            {
                response.Headers["Cache-Control"] = "no-store";
            }

            response.ContentLength64 = result.Body.Length;
            if (result.Body.Length > 0)
            {
                await response.OutputStream.WriteAsync(result.Body).ConfigureAwait(false);
            }

            response.Close();
        }
        catch (Exception)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes("{\"error\":\"erreur contenu\"}");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
            catch (HttpListenerException)
            {
                // Le client a déjà coupé.
            }
        }
    }
}
