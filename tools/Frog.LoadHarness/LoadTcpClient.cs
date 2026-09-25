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
        var lenBuf = new byte[4];
        await ReadExactAsync(lenBuf, timeout).ConfigureAwait(false);
        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        if (len <= 0 || len > 1024 * 1024)
        {
            throw new InvalidOperationException("invalid inbound frame length " + len);
        }

        var payload = new byte[len];
        // The length prefix is already consumed. Finish the body even when the caller's
        // budget was only long enough to start the frame, or the next read desyncs.
        var bodyBudget = timeout < TimeSpan.FromSeconds(8) ? TimeSpan.FromSeconds(8) : timeout;
        await ReadExactAsync(payload, bodyBudget).ConfigureAwait(false);
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
            if (!InboundQueued())
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero)
                {
                    break;
                }

                var wait = left > TimeSpan.FromMilliseconds(20) ? TimeSpan.FromMilliseconds(20) : left;
                await Task.Delay(wait).ConfigureAwait(false);
                continue;
            }

            try
            {
                // A post-login catalog (or select snapshot) can still be arriving when the
                // drain budget ends. Cancelling mid-frame made the next read parse JSON as
                // a length prefix and drop that session before character select.
                _ = await ReadFrameAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidOperationException or TimeoutException or OperationCanceledException)
            {
                break;
            }
        }
    }

    private bool InboundQueued()
    {
        try
        {
            if (_stream is NetworkStream network && network.DataAvailable)
            {
                return true;
            }

            var socket = _tcp?.Client;
            return socket is { Connected: true } && socket.Available > 0;
        }
        catch (ObjectDisposedException)
        {
            return false;
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

    private async Task ReadExactAsync(byte[] buffer, TimeSpan timeout)
    {
        var read = 0;
        var deadline = DateTime.UtcNow + timeout;
        DateTime? completionDeadline = null;
        while (read < buffer.Length)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                if (read == 0)
                {
                    throw new TimeoutException("timed out waiting for frame bytes");
                }

                completionDeadline ??= DateTime.UtcNow.AddSeconds(15);
                remaining = completionDeadline.Value - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException("timed out finishing frame");
                }
            }

            using var cts = new CancellationTokenSource(remaining);
            int n;
            try
            {
                n = await _stream!.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (read == 0)
            {
                throw new TimeoutException("timed out waiting for frame bytes");
            }
            catch (OperationCanceledException)
            {
                completionDeadline ??= DateTime.UtcNow.AddSeconds(15);
                deadline = completionDeadline.Value;
                continue;
            }

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
