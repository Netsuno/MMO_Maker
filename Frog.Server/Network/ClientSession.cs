using System.IO;
using System.Net.Sockets;
using Frog.Server.Models;

namespace Frog.Server.Network;

public sealed class ClientSession : IAsyncDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _closed;
    private int _disposing;
    private int _activeSends;

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

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public async Task<bool> TryReadFrameAsync(CancellationToken cancellationToken, Func<byte[], Task> onFrame)
    {
        var lengthBuffer = new byte[sizeof(int)];
        int lengthRead;
        try
        {
            lengthRead = await ReadExactAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

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
        int payloadRead;
        try
        {
            payloadRead = await ReadExactAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        if (payloadRead != length)
        {
            return false;
        }

        await onFrame(payload).ConfigureAwait(false);
        return true;
    }

    public async Task SendFrameAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (IsClosed || Volatile.Read(ref _disposing) != 0)
        {
            return;
        }

        var acquired = false;
        try
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        Interlocked.Increment(ref _activeSends);
        try
        {
            if (IsClosed)
            {
                return;
            }

            var frame = new byte[sizeof(int) + payload.Length];
            BitConverter.GetBytes(payload.Length).CopyTo(frame, 0);
            payload.CopyTo(frame, sizeof(int));
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
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
        catch (SocketException)
        {
            // Peer reset during fan-out.
        }
        finally
        {
            Interlocked.Decrement(ref _activeSends);
            if (acquired)
            {
                try
                {
                    _sendLock.Release();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }

    private async Task<int> ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken)
                .ConfigureAwait(false);
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
        if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _tcpClient.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposing, 1);
        Disconnect();

        for (var i = 0; i < 10_000 && Volatile.Read(ref _activeSends) > 0; i++)
        {
            await Task.Delay(1).ConfigureAwait(false);
        }

        var acquired = false;
        try
        {
            await _sendLock.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            acquired = true;
        }
        catch
        {
            // Best effort — another send may hold or have disposed the lock.
        }

        if (acquired)
        {
            try
            {
                _sendLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _sendLock.Dispose();
        _stream.Dispose();
        _tcpClient.Dispose();
    }
}
