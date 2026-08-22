using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Constants;
using Frog.Core.Protocol;
using Frog.Server.Network;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Loopback framing tests against the real <see cref="ClientSession"/> parser.
/// </summary>
public sealed class TcpFramingProtocolTests
{
    private const int MaxFrameBytes = 1024 * 1024;

    [Fact]
    public async Task Fragmented_LengthPrefix_IsAccepted()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, _ => Task.CompletedTask);

        var payload = new byte[] { 0x2A };
        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, payload.Length);
        await pair.ClientStream.WriteAsync(len.AsMemory(0, 2));
        await Task.Delay(20);
        await pair.ClientStream.WriteAsync(len.AsMemory(2, 2));
        await pair.ClientStream.WriteAsync(payload);

        Assert.True(await readTask);
    }

    [Fact]
    public async Task Fragmented_Payload_IsAccepted()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        byte[]? received = null;
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, p =>
        {
            received = p;
            return Task.CompletedTask;
        });

        var payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, payload.Length);
        await pair.ClientStream.WriteAsync(len);
        await pair.ClientStream.WriteAsync(payload.AsMemory(0, 20));
        await Task.Delay(20);
        await pair.ClientStream.WriteAsync(payload.AsMemory(20));

        Assert.True(await readTask);
        Assert.Equal(payload, received);
    }

    [Fact]
    public async Task Maximum_Accepted_Frame_Succeeds()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        byte[]? received = null;
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, p =>
        {
            received = p;
            return Task.CompletedTask;
        });

        var payload = new byte[MaxFrameBytes];
        payload[0] = 0x11;
        payload[^1] = 0x22;
        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, payload.Length);
        await pair.ClientStream.WriteAsync(len);
        // Write in chunks to avoid huge single buffer pressure in the test process.
        const int chunk = 64 * 1024;
        for (var offset = 0; offset < payload.Length; offset += chunk)
        {
            var n = Math.Min(chunk, payload.Length - offset);
            await pair.ClientStream.WriteAsync(payload.AsMemory(offset, n));
        }

        Assert.True(await readTask);
        Assert.NotNull(received);
        Assert.Equal(MaxFrameBytes, received!.Length);
        Assert.Equal(0x11, received[0]);
        Assert.Equal(0x22, received[^1]);
    }

    [Fact]
    public async Task Oversized_Frame_Rejected_WithoutHugeAllocationOrHang()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var sw = Stopwatch.StartNew();
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, _ => Task.CompletedTask);

        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, MaxFrameBytes + 1);
        await pair.ClientStream.WriteAsync(len);
        // Do not send payload — parser must reject on length alone.
        var ok = await readTask.WaitAsync(TimeSpan.FromSeconds(3));
        sw.Stop();

        Assert.False(ok);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), "oversized length must not hang");
    }

    [Fact]
    public async Task Zero_Length_Rejected()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, _ => Task.CompletedTask);
        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, 0);
        await pair.ClientStream.WriteAsync(len);
        Assert.False(await readTask.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Negative_Length_Rejected()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, _ => Task.CompletedTask);
        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, -5);
        await pair.ClientStream.WriteAsync(len);
        Assert.False(await readTask.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Truncated_Frame_ReturnsFalse()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, _ => Task.CompletedTask);
        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, 32);
        await pair.ClientStream.WriteAsync(len);
        await pair.ClientStream.WriteAsync(new byte[8]); // incomplete
        pair.Client.Close();
        Assert.False(await readTask.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Cancellation_Aborts_Read()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        using var cts = new CancellationTokenSource();
        var readTask = pair.Session.TryReadFrameAsync(cts.Token, _ => Task.CompletedTask);
        await Task.Delay(30);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await readTask);
    }

    [Fact]
    public async Task Timeout_On_Incomplete_LengthPrefix()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var readTask = pair.Session.TryReadFrameAsync(cts.Token, _ => Task.CompletedTask);
        await pair.ClientStream.WriteAsync(new byte[] { 0x01, 0x00 }); // half length prefix
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await readTask);
    }

    [Fact]
    public async Task ProtocolVersion_Mismatch_IsDetected_OnHello()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var hello = WireHello.BuildPayload();
        // Corrupt protocol version to Current+1.
        BinaryPrimitives.WriteUInt16LittleEndian(hello.AsSpan(hello.Length - 2), (ushort)(FrogWireProtocol.Version + 1));
        await pair.Session.SendFrameAsync(hello, CancellationToken.None);

        var lenBuf = new byte[4];
        await ReadExactAsync(pair.ClientStream, lenBuf, CancellationToken.None);
        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        var payload = new byte[len];
        await ReadExactAsync(pair.ClientStream, payload, CancellationToken.None);

        Assert.True(WireHello.TryParse(payload, out _, out var ver));
        Assert.NotEqual(FrogWireProtocol.Version, ver);
        Assert.Equal((ushort)(FrogWireProtocol.Version + 1), ver);
    }

    [Fact]
    public async Task Malicious_IntMax_Length_Rejected_Quickly()
    {
        await using var pair = await LoopbackPair.CreateAsync();
        var sw = Stopwatch.StartNew();
        var readTask = pair.Session.TryReadFrameAsync(CancellationToken.None, _ => Task.CompletedTask);
        var len = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, int.MaxValue);
        await pair.ClientStream.WriteAsync(len);
        var ok = await readTask.WaitAsync(TimeSpan.FromSeconds(2));
        sw.Stop();
        Assert.False(ok);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1));
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }
    }

    private sealed class LoopbackPair : IAsyncDisposable
    {
        public required ClientSession Session { get; init; }
        public required TcpClient Client { get; init; }
        public required NetworkStream ClientStream { get; init; }
        public required TcpListener Listener { get; init; }

        public static async Task<LoopbackPair> CreateAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accept = listener.AcceptTcpClientAsync();
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            var serverTcp = await accept;
            return new LoopbackPair
            {
                Listener = listener,
                Client = client,
                ClientStream = client.GetStream(),
                Session = new ClientSession(serverTcp),
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Session.DisposeAsync();
            ClientStream.Dispose();
            Client.Dispose();
            Listener.Stop();
        }
    }
}
