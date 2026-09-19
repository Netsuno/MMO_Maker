using System.Buffers.Binary;
using System.Net.Sockets;
using Frog.Core.Enums;
using Frog.Core.Security;

namespace Frog.LoadHarness;

internal sealed class LoadTcpClient : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private TcpClient? _tcp;
    private Stream? _stream;

    public async Task ConnectAsync(
        string host,
        int port,
        TimeSpan timeout,
        ClientTlsOptions? tls = null)
    {
        _tcp = new TcpClient();
        using var cts = new CancellationTokenSource(timeout);
        await _tcp.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
        var inner = _tcp.GetStream();
        _stream = await TlsClientAuthenticator
            .WrapAfterConnectAsync(inner, tls ?? ClientTlsOptions.Off, host, cts.Token)
            .ConfigureAwait(false);
    }

    public async Task SendFrameAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await _stream!.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Writes a length prefix only (used to probe the 1 MiB frame cap).</summary>
    public async Task SendRawLengthPrefixAsync(int length, CancellationToken cancellationToken = default)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var len = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(len, length);
            await _stream!.WriteAsync(len, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<byte[]> ReadFrameAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var lenBuf = new byte[4];
        await ReadExactAsync(lenBuf, cts.Token).ConfigureAwait(false);
        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        if (len <= 0 || len > 1024 * 1024)
        {
            throw new InvalidOperationException("invalid inbound frame length " + len);
        }

        var payload = new byte[len];
        await ReadExactAsync(payload, cts.Token).ConfigureAwait(false);
        return payload;
    }

    public async Task<byte[]> ReadUntilAsync(PacketId id, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var frame = await ReadFrameAsync(remaining).ConfigureAwait(false);
            if (frame.Length > 0 && frame[0] == (byte)id)
            {
                return frame;
            }
        }

        throw new TimeoutException("expected packet not received: " + id);
    }

    public async Task DrainPendingAsync(TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                _ = await ReadFrameAsync(remaining).ConfigureAwait(false);
            }
            catch
            {
                break;
            }
        }
    }

    public void Close()
    {
        try
        {
            _tcp?.Close();
        }
        catch
        {
            // ignore
        }
    }

    private async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct)
                .ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Close();
        _stream?.Dispose();
        _tcp?.Dispose();
        _sendLock.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
