using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;

/// <summary>
/// Production-equivalent headless playtest client: Hello, token login, map load, READY stdout.
/// Same markers as Frog.Client playtest auto-flow (never prints the token).
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var host = "127.0.0.1";
        var port = 6000;
        string? correlation = null;
        string? token = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--host" && i + 1 < args.Length)
            {
                host = args[++i];
            }
            else if (args[i] == "--port" && i + 1 < args.Length)
            {
                port = int.Parse(args[++i]);
            }
            else if (args[i] == "--correlation" && i + 1 < args.Length)
            {
                correlation = args[++i];
            }
            else if (args[i] == "--playtest-token" && i + 1 < args.Length)
            {
                token = args[++i];
            }
            else if (args[i] == "--playtest")
            {
                // accepted no-op (launcher always passes it)
            }
        }

        token ??= Environment.GetEnvironmentVariable("FROG_PLAYTEST_AUTH_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("FROG_PLAYTEST_FAIL missing token");
            return 2;
        }

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port).ConfigureAwait(false);
            await using var stream = tcp.GetStream();
            var hello = await ReadFrameAsync(stream).ConfigureAwait(false);
            if (!WireHello.TryParse(hello, out _, out var ver) || ver != FrogWireProtocol.Version)
            {
                Console.Error.WriteLine("FROG_PLAYTEST_FAIL bad hello");
                return 3;
            }

            await WriteFrameAsync(stream, BuildLogin("__frog_playtest__", token)).ConfigureAwait(false);
            var login = await ReadUntilAsync(stream, PacketId.LoginResult).ConfigureAwait(false);
            if (login.Length < 2 || login[1] == 0)
            {
                Console.Error.WriteLine("FROG_PLAYTEST_FAIL login");
                return 4;
            }

            _ = await ReadUntilAsync(stream, PacketId.PositionUpdate).ConfigureAwait(false);
            await WriteFrameAsync(stream, [(byte)PacketId.MapRequest]).ConfigureAwait(false);
            var map = await ReadUntilAnyAsync(stream, PacketId.MapData, PacketId.MapAlreadySynced)
                .ConfigureAwait(false);
            var mapId = map.Length >= 5
                ? BinaryPrimitives.ReadInt32LittleEndian(map.AsSpan(1))
                : 1;
            var corr = string.IsNullOrWhiteSpace(correlation) ? "-" : correlation;
            Console.WriteLine($"FROG_PLAYTEST_READY correlation={corr} map={mapId} x=0 y=0");
            Console.Out.Flush();
            await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Replace('\r', ' ').Replace('\n', ' ');
            if (!string.IsNullOrEmpty(token))
            {
                msg = msg.Replace(token, "***", StringComparison.Ordinal);
            }

            Console.Error.WriteLine("FROG_PLAYTEST_FAIL " + msg);
            return 5;
        }
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream s)
    {
        var lenBuf = new byte[4];
        await ReadExactAsync(s, lenBuf).ConfigureAwait(false);
        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        if (len is <= 0 or > 1024 * 1024)
        {
            throw new InvalidOperationException("invalid frame length");
        }

        var payload = new byte[len];
        await ReadExactAsync(s, payload).ConfigureAwait(false);
        return payload;
    }

    private static async Task WriteFrameAsync(NetworkStream s, byte[] payload)
    {
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame, 4);
        await s.WriteAsync(frame).ConfigureAwait(false);
    }

    private static async Task ReadExactAsync(NetworkStream s, byte[] buf)
    {
        var n = 0;
        while (n < buf.Length)
        {
            var r = await s.ReadAsync(buf.AsMemory(n, buf.Length - n)).ConfigureAwait(false);
            if (r == 0)
            {
                throw new EndOfStreamException();
            }

            n += r;
        }
    }

    private static async Task<byte[]> ReadUntilAsync(NetworkStream s, PacketId id)
    {
        while (true)
        {
            var f = await ReadFrameAsync(s).ConfigureAwait(false);
            if (f[0] == (byte)id)
            {
                return f;
            }
        }
    }

    private static async Task<byte[]> ReadUntilAnyAsync(NetworkStream s, params PacketId[] ids)
    {
        while (true)
        {
            var f = await ReadFrameAsync(s).ConfigureAwait(false);
            if (ids.Any(i => f[0] == (byte)i))
            {
                return f;
            }
        }
    }

    private static byte[] BuildLogin(string user, string pass)
    {
        var ub = Encoding.UTF8.GetBytes(user);
        var pb = Encoding.UTF8.GetBytes(pass);
        var payload = new byte[1 + 1 + ub.Length + 1 + pb.Length];
        payload[0] = (byte)PacketId.LoginRequest;
        payload[1] = (byte)ub.Length;
        ub.CopyTo(payload, 2);
        payload[2 + ub.Length] = (byte)pb.Length;
        pb.CopyTo(payload, 3 + ub.Length);
        return payload;
    }
}
