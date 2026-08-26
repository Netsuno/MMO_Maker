using System.IO;
using System.Net.Sockets;
using Frog.Server.Models;

namespace Frog.Server.Network;

public sealed class ClientSession : IAsyncDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ClientSession(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        ConnectionId = Guid.NewGuid();
    }

    /// <summary>Identifiant stable pour corrélation des logs sur la durée de la connexion TCP.</summary>
    public Guid ConnectionId { get; }

    public string RemoteEndPoint => _tcpClient.Client.RemoteEndPoint?.ToString() ?? "<unknown>";
    public Session? AuthenticatedSession { get; set; }
    public string? Username => AuthenticatedSession?.Username;

    public async Task<bool> TryReadFrameAsync(CancellationToken cancellationToken, Func<byte[], Task> onFrame)
    {
        var lengthBuffer = new byte[sizeof(int)];
        var lengthRead = await ReadExactAsync(lengthBuffer, cancellationToken);
        if (lengthRead == 0)
        {
            return false;
        }

        if (lengthRead != lengthBuffer.Length)
        {
            return false;
        }

        var length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > 1024 * 1024)
        {
            return false;
        }

        var payload = new byte[length];
        var payloadRead = await ReadExactAsync(payload, cancellationToken);
        if (payloadRead != length)
        {
            return false;
        }

        await onFrame(payload);
        return true;
    }

    public async Task SendFrameAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var frame = new byte[sizeof(int) + payload.Length];
        BitConverter.GetBytes(payload.Length).CopyTo(frame, 0);
        payload.CopyTo(frame, sizeof(int));
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // Zombie / already-closed TCP (e.g. displaced reconnect) — ignore so broadcasts
            // do not abort the sender's DispatchAsync and drop the live connection.
        }
        catch (IOException)
        {
            // Peer reset / half-closed stream during fan-out.
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<int> ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                return totalRead;
            }

            totalRead += read;
        }

        return totalRead;
    }

    /// <summary>
    /// Ferme la connexion TCP (deblocage typique de <see cref="ReadAsync"/> cote lecture).
    /// </summary>
    public void Disconnect()
    {
        try
        {
            _tcpClient.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        _sendLock.Dispose();
        _stream.Dispose();
        _tcpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}
