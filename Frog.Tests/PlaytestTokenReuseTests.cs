using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server;
using Frog.Server.Config;
using Frog.Server.Database;
using Frog.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestTokenReuseTests
{
    [Theory]
    [InlineData("__frog_playtest__")]
    [InlineData("__FROG_PLAYTEST__")]
    [InlineData("__FRoG_PlayTest__")]
    public async Task Tcp_ReservedRegistration_Rejected_AllCasings(string reservedUsername)
    {
        var ctx = await StartPlaytestHostAsync();
        try
        {
            await using var tcp = new TokenTcpClient();
            await tcp.ConnectAsync("127.0.0.1", ctx.Port);
            _ = await tcp.ReadFrameAsync();
            await tcp.SendFrameAsync(BuildRegister(reservedUsername, ctx.Plan.AuthToken));
            var reg = await tcp.ReadUntilAsync(PacketId.RegisterResult);
            Assert.Equal(0, reg[1]);
            var payload = Encoding.UTF8.GetString(reg);
            Assert.DoesNotContain(ctx.Plan.AuthToken, payload, StringComparison.Ordinal);
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_FirstAuthSucceeds_ReuseAfterDisconnectFails()
    {
        var ctx = await StartPlaytestHostAsync();
        try
        {
            await using (var tcp1 = new TokenTcpClient())
            {
                await tcp1.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp1.ReadFrameAsync();
                await tcp1.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                var login1 = await tcp1.ReadUntilAsync(PacketId.LoginResult);
                Assert.NotEqual(0, login1[1]);
            }

            await using (var tcp2 = new TokenTcpClient())
            {
                await tcp2.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp2.ReadFrameAsync();
                await tcp2.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                var login2 = await tcp2.ReadUntilAsync(PacketId.LoginResult);
                Assert.Equal(0, login2[1]);
            }
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_MixedCaseSeededAccount_CannotReuseTokenAfterConsume()
    {
        const string mixedCase = "__FRoG_PlayTest__";
        var ctx = await StartPlaytestHostAsync();
        try
        {
            var accounts = ctx.Host.Services.GetRequiredService<InMemoryAccountRepository>();
            var create = await accounts.TryCreateAsync(mixedCase, ctx.Plan.AuthToken!);
            Assert.Equal(Frog.Application.Identity.AccountCreateStatus.Created, create.Status);

            await using (var tcp1 = new TokenTcpClient())
            {
                await tcp1.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp1.ReadFrameAsync();
                await tcp1.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                Assert.NotEqual(0, (await tcp1.ReadUntilAsync(PacketId.LoginResult))[1]);
            }

            var gate = ctx.Host.Services.GetRequiredService<PlaytestAuthTokenGate>();
            Assert.False(gate.HasRemainingToken);

            await using (var tcp2 = new TokenTcpClient())
            {
                await tcp2.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp2.ReadFrameAsync();
                await tcp2.SendFrameAsync(BuildLogin(mixedCase, ctx.Plan.AuthToken));
                var login2 = await tcp2.ReadUntilAsync(PacketId.LoginResult);
                Assert.Equal(0, login2[1]);
                var payload = Encoding.UTF8.GetString(login2);
                Assert.DoesNotContain(ctx.Plan.AuthToken, payload, StringComparison.Ordinal);
            }
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_NoNormalAuthFallback_AfterTokenConsumed_EvenIfAccountExists()
    {
        var ctx = await StartPlaytestHostAsync();
        try
        {
            var auth = ctx.Host.Services.GetRequiredService<AuthService>();
            var reg = await auth.RegisterAccountAsync(PlaytestAuthToken.Username, ctx.Plan.AuthToken!);
            Assert.Equal(Frog.Application.Identity.AccountCreateStatus.Created, reg.Status);

            await using (var tcp1 = new TokenTcpClient())
            {
                await tcp1.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp1.ReadFrameAsync();
                await tcp1.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                Assert.NotEqual(0, (await tcp1.ReadUntilAsync(PacketId.LoginResult))[1]);
            }

            await using (var tcp2 = new TokenTcpClient())
            {
                await tcp2.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp2.ReadFrameAsync();
                await tcp2.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                Assert.Equal(0, (await tcp2.ReadUntilAsync(PacketId.LoginResult))[1]);
            }
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_ConcurrentAuth_ExactlyOneSuccess()
    {
        var ctx = await StartPlaytestHostAsync();
        try
        {
            var barrier = new Barrier(2);
            var successes = 0;
            async Task<bool> TryLoginOnceAsync()
            {
                await using var tcp = new TokenTcpClient();
                await tcp.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp.ReadFrameAsync();
                barrier.SignalAndWait();
                await tcp.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                var login = await tcp.ReadUntilAsync(PacketId.LoginResult);
                return login.Length >= 2 && login[1] != 0;
            }

            var t1 = Task.Run(TryLoginOnceAsync);
            var t2 = Task.Run(TryLoginOnceAsync);
            await Task.WhenAll(t1, t2);
            if (t1.Result)
            {
                Interlocked.Increment(ref successes);
            }

            if (t2.Result)
            {
                Interlocked.Increment(ref successes);
            }

            Assert.Equal(1, successes);
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_SessionCreationFailure_DoesNotConsumeToken()
    {
        var ctx = await StartPlaytestHostAsync();
        try
        {
            var connectionManager = ctx.Host.Services.GetRequiredService<ConnectionManager>();
            Assert.True(
                connectionManager.TryCreateSession(PlaytestAuthToken.Username, out var blockingSession));
            Assert.NotNull(blockingSession);

            await using (var blocked = new TokenTcpClient())
            {
                await blocked.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await blocked.ReadFrameAsync();
                await blocked.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                Assert.Equal(0, (await blocked.ReadUntilAsync(PacketId.LoginResult))[1]);
            }

            var gate = ctx.Host.Services.GetRequiredService<PlaytestAuthTokenGate>();
            Assert.True(gate.HasRemainingToken);

            connectionManager.RemoveSession(blockingSession!.Id);

            await using (var retry = new TokenTcpClient())
            {
                await retry.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await retry.ReadFrameAsync();
                await retry.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                Assert.NotEqual(0, (await retry.ReadUntilAsync(PacketId.LoginResult))[1]);
            }
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_AbortAfterPositiveLoginResult_TokenRemainsConsumed()
    {
        var ctx = await StartPlaytestHostAsync();
        try
        {
            await using (var tcp1 = new TokenTcpClient())
            {
                await tcp1.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp1.ReadFrameAsync();
                await tcp1.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                var login1 = await tcp1.ReadUntilAsync(PacketId.LoginResult);
                Assert.NotEqual(0, login1[1]);
                tcp1.Abort();
            }

            var gate = ctx.Host.Services.GetRequiredService<PlaytestAuthTokenGate>();
            Assert.False(gate.HasRemainingToken);

            await using (var tcp2 = new TokenTcpClient())
            {
                await tcp2.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp2.ReadFrameAsync();
                await tcp2.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                Assert.Equal(0, (await tcp2.ReadUntilAsync(PacketId.LoginResult))[1]);
            }
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_InjectedFailureAfterLoginResult_TokenRemainsConsumed()
    {
        var ctx = await StartPlaytestHostAsync(o => o with { FailAfterSuccessfulLoginResult = true });
        try
        {
            await using (var tcp1 = new TokenTcpClient())
            {
                await tcp1.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp1.ReadFrameAsync();
                await tcp1.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                var login1 = await tcp1.ReadUntilAsync(PacketId.LoginResult);
                Assert.NotEqual(0, login1[1]);
            }

            // Laisser le serveur traiter l’échec injecté post-LoginResult.
            await Task.Delay(200);

            var gate = ctx.Host.Services.GetRequiredService<PlaytestAuthTokenGate>();
            Assert.False(gate.HasRemainingToken);

            await using (var tcp2 = new TokenTcpClient())
            {
                await tcp2.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp2.ReadFrameAsync();
                await tcp2.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                Assert.Equal(0, (await tcp2.ReadUntilAsync(PacketId.LoginResult))[1]);
            }
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tcp_TokenNeverAppearsInLoginFailureMessage()
    {
        var ctx = await StartPlaytestHostAsync();
        try
        {
            await using (var tcp1 = new TokenTcpClient())
            {
                await tcp1.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp1.ReadFrameAsync();
                await tcp1.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                _ = await tcp1.ReadUntilAsync(PacketId.LoginResult);
            }

            await using (var tcp2 = new TokenTcpClient())
            {
                await tcp2.ConnectAsync("127.0.0.1", ctx.Port);
                _ = await tcp2.ReadFrameAsync();
                await tcp2.SendFrameAsync(BuildLogin(PlaytestAuthToken.Username, ctx.Plan.AuthToken));
                var frames = await tcp2.ReadUntilAsync(PacketId.LoginResult);
                var payload = Encoding.UTF8.GetString(frames);
                Assert.DoesNotContain(ctx.Plan.AuthToken, payload, StringComparison.Ordinal);
            }
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    private static async Task<PlaytestHostContext> StartPlaytestHostAsync(
        Func<PlaytestRuntimeOptions, PlaytestRuntimeOptions>? mutateOptions = null)
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
        var playtestOpts = FrogServerHostFactory.CreatePlaytestOptionsFromPlan(plan);
        if (mutateOptions is not null)
        {
            playtestOpts = mutateOptions(playtestOpts);
        }

        var host = FrogServerHostFactory.Create(playtestOpts);
        await host.StartAsync();
        return new PlaytestHostContext(host, plan, port);
    }

    private sealed class PlaytestHostContext(IHost host, PlaytestLaunchPlan plan, int port) : IAsyncDisposable
    {
        public IHost Host { get; } = host;
        public PlaytestLaunchPlan Plan { get; } = plan;
        public int Port { get; } = port;

        public async ValueTask DisposeAsync()
        {
            await Host.StopAsync();
            Host.Dispose();
            if (Directory.Exists(Plan.WorkDirectory))
            {
                PlaytestWorkspacePaths.TryDeleteOwnedWorkspace(Plan.WorkDirectory, Plan.CorrelationId, out _);
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

    private static byte[] BuildRegister(string user, string pass)
    {
        var payload = BuildLogin(user, pass);
        payload[0] = (byte)PacketId.RegisterRequest;
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

        public void Abort()
        {
            try
            {
                if (_tcp?.Client is { } socket)
                {
                    socket.LingerState = new LingerOption(true, 0);
                    socket.Close();
                }
            }
            catch
            {
                // ignore
            }

            _stream = null;
            _tcp = null;
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
