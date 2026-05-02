using System.Buffers.Binary;
using System.Net.Sockets;

namespace Frog.Client.Network;

internal static class TcpFrameCodec
{
    public static async Task<byte[]?> ReadFramePayloadAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var lenBuf = new byte[sizeof(int)];
        if (!await ReadExactAsync(stream, lenBuf, cancellationToken))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        if (length <= 0 || length > 1024 * 1024)
        {
            return null;
        }

        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, cancellationToken))
        {
            return null;
        }

        return payload;
    }

    public static async Task WriteFrameAsync(NetworkStream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var frame = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame.AsMemory(sizeof(int)));
        await stream.WriteAsync(frame, cancellationToken);
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var readTotal = 0;
        while (readTotal < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(readTotal, buffer.Length - readTotal), cancellationToken);
            if (n == 0)
            {
                return false;
            }

            readTotal += n;
        }

        return true;
    }
}
