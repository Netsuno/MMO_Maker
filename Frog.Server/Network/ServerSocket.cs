using System.Net;
using System.Net.Sockets;

namespace Frog.Server.Network;

/// <summary>
/// Acceptation TCP uniquement. L'enveloppe TLS (<c>SslStream.AuthenticateAsServer</c>)
/// est appliquée après accept et avant le framing <see cref="ClientSession"/>.
/// </summary>
public sealed class ServerSocket : IAsyncDisposable
{
    private readonly TcpListener _listener;

    public ServerSocket(IPAddress bindAddress, int port)
    {
        _listener = new TcpListener(bindAddress, port);
    }

    public void Start() => _listener.Start();

    public Task<TcpClient> AcceptClientAsync(CancellationToken cancellationToken)
        => _listener.AcceptTcpClientAsync(cancellationToken).AsTask();

    public ValueTask DisposeAsync()
    {
        _listener.Stop();
        return ValueTask.CompletedTask;
    }
}
