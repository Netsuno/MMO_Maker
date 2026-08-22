using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Config;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestTokenReuseTests
{
    [Fact]
    public async Task PlaytestToken_FirstAuthSucceeds_ReuseAfterDisconnectFails_TokenNeverInLogs()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var port = GetFreePort();
        var prepared = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 0,
                SpawnTileY = 0,
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
            });
        var plan = Assert.IsType<PlaytestPreparationResult.Success>(prepared).Plan;
        Assert.False(string.IsNullOrEmpty(plan.AuthToken));

        var playtestOpts = FrogServerHostFactory.CreatePlaytestOptionsFromPlan(plan);
        using var host = FrogServerHostFactory.Create(playtestOpts);
        await host.StartAsync();

        try
        {
            await using (var tcp1 = new TokenTcpClient())
            {
                await tcp1.ConnectAsync("127.0.0.1", port);
                _ = await tcp1.ReadFrameAsync(); // Hello
                await tcp1.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, plan.AuthToken));
                var login1 = await tcp1.ReadUntilAsync(PacketId.LoginResult);
                Assert.NotEqual(0, login1[1]);
            }

            // After disconnect, same token must fail (single-use consume).
            await using (var tcp2 = new TokenTcpClient())
            {
                await tcp2.ConnectAsync("127.0.0.1", port);
                _ = await tcp2.ReadFrameAsync();
                await tcp2.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, plan.AuthToken));
                var login2 = await tcp2.ReadUntilAsync(PacketId.LoginResult);
                Assert.Equal(0, login2[1]);
            }
        }
        finally
        {
            await host.StopAsync();
            if (Directory.Exists(plan.WorkDirectory))
            {
                PlaytestWorkspacePaths.TryDeleteOwnedWorkspace(plan.WorkDirectory, plan.CorrelationId, out _);
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

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private sealed class TokenTcpClient : IAsyncDisposable
    {
        private TcpClient? _tcp;
        private NetworkStream? _stream;

        public async Task ConnectAsync(string host, int port)
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync(host, port);
            _stream = _tcp.GetStream();
        }

        public async Task<byte[]> ReadFrameAsync()
        {
            var lenBuf = new byte[4];
            await ReadExactAsync(lenBuf);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await ReadExactAsync(payload);
            return payload;
        }

        public async Task SendFrameAsync(byte[] payload)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await _stream!.WriteAsync(frame);
        }

        public async Task<byte[]> ReadUntilAsync(PacketId id)
        {
            while (true)
            {
                var f = await ReadFrameAsync();
                if (f[0] == (byte)id)
                {
                    return f;
                }
            }
        }

        private async Task ReadExactAsync(byte[] buf)
        {
            var n = 0;
            while (n < buf.Length)
            {
                var r = await _stream!.ReadAsync(buf.AsMemory(n, buf.Length - n));
                if (r == 0)
                {
                    throw new EndOfStreamException();
                }

                n += r;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_stream is not null)
            {
                await _stream.DisposeAsync();
            }

            _tcp?.Dispose();
        }
    }
}
