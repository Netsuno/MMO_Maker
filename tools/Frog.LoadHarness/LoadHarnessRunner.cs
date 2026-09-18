using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Core.Enums;
using Frog.Server.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.LoadHarness;

public sealed class LoadHarnessRunner
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public const string Usage =
        """
        Frog.LoadHarness — P9-5 TCP load / observability probe.

        Default: spin up an in-memory Frog.Server on a free loopback port (no PostgreSQL).

          dotnet run --project tools/Frog.LoadHarness -- --scenario mixed --sessions 25
          ./scripts/run-load-harness.sh --sessions 25 --scenario mixed

        Attach to an already-running server (packaged or from-source):

          dotnet run --project tools/Frog.LoadHarness -- --host 127.0.0.1 --port 6000 --sessions 10 --scenario connect

        Options:
          --scenario connect|chat|move|mixed   (default mixed)
          --sessions N                         (default 25, max 500)
          --hold-ms N                          hold open after work (default 3000)
          --chat-burst N                       chat sends per authed session (default 12)
          --move-burst N                       move packets per authed session (default 80)
          --self-host                          in-memory host (default)
          --host ADDR --port N                 attach instead of self-host
          --json-out PATH                      write the JSON report
          --max-parallel-auth N                cap concurrent register/login (default 8)

        mixed = connect + authenticate + chat burst + move burst + oversize reject + login rate-limit probe.
        """;

    public static async Task<LoadHarnessReport> RunAsync(
        LoadHarnessOptions options,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
        IHost? host = null;
        var address = options.Host ?? "127.0.0.1";
        var port = options.Port;
        ServerOpsMetrics? serverMetrics = null;

        try
        {
            if (options.SelfHost)
            {
                port = GetFreePort();
                address = "127.0.0.1";
                host = InMemoryLoadHost.Create(port);
                await host.StartAsync(cancellationToken).ConfigureAwait(false);
                serverMetrics = host.Services.GetRequiredService<ServerOpsMetrics>();
                await WaitForAcceptAsync(address, port, TimeSpan.FromSeconds(8), cancellationToken)
                    .ConfigureAwait(false);
            }

            var client = await ExecuteScenarioAsync(options, address, port, cancellationToken)
                .ConfigureAwait(false);

            // Give the snapshot service / handlers a beat to record rejects.
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);

            var cpuEnd = Process.GetCurrentProcess().TotalProcessorTime;
            var ended = DateTimeOffset.UtcNow;
            var elapsed = ended - started;
            var cpuMs = (cpuEnd - cpuStart).TotalMilliseconds;
            var cpuPct = elapsed.TotalMilliseconds > 0
                ? 100.0 * cpuMs / (elapsed.TotalMilliseconds * Math.Max(1, Environment.ProcessorCount))
                : 0;

            var report = new LoadHarnessReport
            {
                StartedUtc = started,
                EndedUtc = ended,
                ElapsedMs = (long)elapsed.TotalMilliseconds,
                Scenario = options.Scenario,
                RequestedSessions = options.Sessions,
                Host = new LoadHostInfo
                {
                    Mode = options.SelfHost ? "self-host-inmemory" : "attach",
                    Address = address,
                    Port = port,
                },
                Machine = new LoadMachineInfo
                {
                    Os = RuntimeInformation.OSDescription,
                    Framework = RuntimeInformation.FrameworkDescription,
                    ProcessorCount = Environment.ProcessorCount,
                    ProcessCpuPercentEstimate = Math.Round(cpuPct, 1),
                    ProcessWorkingSetBytes = Process.GetCurrentProcess().WorkingSet64,
                    HostName = Environment.MachineName,
                },
                Client = client,
                ServerOps = serverMetrics?.Snapshot(),
            };

            if (!string.IsNullOrWhiteSpace(options.JsonOut))
            {
                var dir = Path.GetDirectoryName(options.JsonOut);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(
                    options.JsonOut,
                    JsonSerializer.Serialize(report, JsonOptions),
                    cancellationToken).ConfigureAwait(false);
            }

            return report;
        }
        finally
        {
            if (host is not null)
            {
                try
                {
                    await host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch
                {
                    // ignore stop races
                }

                host.Dispose();
            }
        }
    }

    private static async Task<LoadClientCounters> ExecuteScenarioAsync(
        LoadHarnessOptions options,
        string address,
        int port,
        CancellationToken cancellationToken)
    {
        var scenario = options.Scenario;
        var needAuth = scenario is "chat" or "move" or "mixed";
        var needChat = scenario is "chat" or "mixed";
        var needMove = scenario is "move" or "mixed";
        var needOversize = scenario is "mixed";
        var needLoginRate = scenario is "mixed";

        var counters = new LoadClientCounters();
        var runId = Guid.NewGuid().ToString("N")[..8];
        var clients = new LoadTcpClient[options.Sessions];
        var gate = new SemaphoreSlim(options.MaxParallelAuth, options.MaxParallelAuth);

        try
        {
            var connectTasks = Enumerable.Range(0, options.Sessions).Select(async i =>
            {
                var tcp = new LoadTcpClient();
                clients[i] = tcp;
                try
                {
                    await tcp.ConnectAsync(address, port, TimeSpan.FromMilliseconds(options.ConnectTimeoutMs))
                        .ConfigureAwait(false);
                    Interlocked.Increment(ref counters.TcpConnectOk);
                    var hello = await tcp.ReadFrameAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    if (LoadPackets.IsHello(hello))
                    {
                        Interlocked.Increment(ref counters.HelloOk);
                    }
                    else
                    {
                        Interlocked.Increment(ref counters.HelloFail);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref counters.TcpConnectFail);
                }
            });
            await Task.WhenAll(connectTasks).ConfigureAwait(false);

            if (needAuth)
            {
                var authTasks = Enumerable.Range(0, options.Sessions).Select(async i =>
                {
                    var tcp = clients[i];
                    if (tcp is null)
                    {
                        return;
                    }

                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var user = $"ld{runId}{i:D3}";
                        const string password = "password123";
                        var charName = $"H{i}";
                        await AuthenticateAsync(tcp, user, password, charName, counters).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                });
                await Task.WhenAll(authTasks).ConfigureAwait(false);
            }

            if (needChat)
            {
                var chatTasks = Enumerable.Range(0, options.Sessions).Select(async i =>
                {
                    var tcp = clients[i];
                    if (tcp is null)
                    {
                        return;
                    }

                    await ChatBurstAsync(tcp, options.ChatBurst, counters).ConfigureAwait(false);
                });
                await Task.WhenAll(chatTasks).ConfigureAwait(false);
            }

            if (needMove)
            {
                var moveTasks = Enumerable.Range(0, options.Sessions).Select(async i =>
                {
                    var tcp = clients[i];
                    if (tcp is null)
                    {
                        return;
                    }

                    await MoveBurstAsync(tcp, options.MoveBurst, counters).ConfigureAwait(false);
                });
                await Task.WhenAll(moveTasks).ConfigureAwait(false);
            }

            if (options.HoldMilliseconds > 0)
            {
                await Task.Delay(options.HoldMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            if (needOversize)
            {
                await OversizeProbeAsync(address, port, options.ConnectTimeoutMs, counters)
                    .ConfigureAwait(false);
            }

            if (needLoginRate)
            {
                await LoginRateProbeAsync(address, port, options.ConnectTimeoutMs, counters)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var tcp in clients)
            {
                if (tcp is not null)
                {
                    await tcp.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        return counters;
    }

    private static async Task AuthenticateAsync(
        LoadTcpClient tcp,
        string user,
        string password,
        string charName,
        LoadClientCounters counters)
    {
        try
        {
            await tcp.SendFrameAsync(LoadPackets.Register(user, password)).ConfigureAwait(false);
            var register = await tcp.ReadUntilAsync(PacketId.RegisterResult, TimeSpan.FromSeconds(20))
                .ConfigureAwait(false);
            if (!LoadPackets.StatusOk(register))
            {
                Interlocked.Increment(ref counters.RegisterFail);
                return;
            }

            Interlocked.Increment(ref counters.RegisterOk);

            await tcp.SendFrameAsync(LoadPackets.Login(user, password)).ConfigureAwait(false);
            var login = await tcp.ReadUntilAsync(PacketId.LoginResult, TimeSpan.FromSeconds(20))
                .ConfigureAwait(false);
            if (!LoadPackets.StatusOk(login))
            {
                Interlocked.Increment(ref counters.LoginFail);
                return;
            }

            Interlocked.Increment(ref counters.LoginOk);
            await tcp.DrainPendingAsync(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);

            await tcp.SendFrameAsync(LoadPackets.CharacterCreate(charName, LoadPackets.DefaultClassId))
                .ConfigureAwait(false);
            var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult, TimeSpan.FromSeconds(15))
                .ConfigureAwait(false);
            if (!LoadPackets.StatusOk(create))
            {
                Interlocked.Increment(ref counters.CharacterCreateFail);
                return;
            }

            Interlocked.Increment(ref counters.CharacterCreateOk);
            var characterId = LoadPackets.StatusMessage(create);

            await tcp.SendFrameAsync(LoadPackets.CharacterSelect(characterId)).ConfigureAwait(false);
            var select = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult, TimeSpan.FromSeconds(15))
                .ConfigureAwait(false);
            if (!LoadPackets.StatusOk(select))
            {
                Interlocked.Increment(ref counters.CharacterSelectFail);
                return;
            }

            Interlocked.Increment(ref counters.CharacterSelectOk);
            await tcp.DrainPendingAsync(TimeSpan.FromMilliseconds(400)).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Increment(ref counters.AuthenticateException);
        }
    }

    private static async Task ChatBurstAsync(LoadTcpClient tcp, int burst, LoadClientCounters counters)
    {
        for (var n = 0; n < burst; n++)
        {
            try
            {
                await tcp.SendFrameAsync(LoadPackets.Chat(ChatChannel.Global, "load " + n))
                    .ConfigureAwait(false);
                Interlocked.Increment(ref counters.ChatSent);
            }
            catch
            {
                Interlocked.Increment(ref counters.ChatSendFail);
            }
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(4);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var frame = await tcp.ReadFrameAsync(deadline - DateTime.UtcNow).ConfigureAwait(false);
                if (frame.Length == 0)
                {
                    continue;
                }

                if (frame[0] == (byte)PacketId.ChatMessage)
                {
                    Interlocked.Increment(ref counters.ChatMessageRecv);
                }
                else if (frame[0] == (byte)PacketId.Error)
                {
                    var msg = LoadPackets.ErrorMessage(frame);
                    if (msg.Contains("Trop de messages", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref counters.ChatRateLimited);
                    }
                    else
                    {
                        Interlocked.Increment(ref counters.OtherErrors);
                    }
                }
            }
            catch
            {
                break;
            }
        }
    }

    private static async Task MoveBurstAsync(LoadTcpClient tcp, int burst, LoadClientCounters counters)
    {
        var sw = Stopwatch.StartNew();
        for (var n = 0; n < burst; n++)
        {
            try
            {
                await tcp.SendFrameAsync(LoadPackets.Move(1, 0)).ConfigureAwait(false);
                Interlocked.Increment(ref counters.MoveSent);
            }
            catch
            {
                Interlocked.Increment(ref counters.MoveSendFail);
            }
        }

        Interlocked.Add(ref counters.MoveBurstElapsedMs, sw.ElapsedMilliseconds);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var frame = await tcp.ReadFrameAsync(deadline - DateTime.UtcNow).ConfigureAwait(false);
                if (frame.Length == 0)
                {
                    continue;
                }

                if (frame[0] == (byte)PacketId.PositionUpdate)
                {
                    Interlocked.Increment(ref counters.PositionUpdateRecv);
                }
                else if (frame[0] == (byte)PacketId.Error)
                {
                    var msg = LoadPackets.ErrorMessage(frame);
                    if (msg.Contains("Trop de mouvements", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref counters.MoveRateLimited);
                    }
                    else
                    {
                        Interlocked.Increment(ref counters.OtherErrors);
                    }
                }
            }
            catch
            {
                break;
            }
        }
    }

    private static async Task OversizeProbeAsync(
        string address,
        int port,
        int connectTimeoutMs,
        LoadClientCounters counters)
    {
        await using var tcp = new LoadTcpClient();
        try
        {
            await tcp.ConnectAsync(address, port, TimeSpan.FromMilliseconds(connectTimeoutMs))
                .ConfigureAwait(false);
            _ = await tcp.ReadFrameAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
            await tcp.SendRawLengthPrefixAsync(1024 * 1024 + 1).ConfigureAwait(false);
            try
            {
                _ = await tcp.ReadFrameAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                Interlocked.Increment(ref counters.OversizeStillConnected);
            }
            catch (EndOfStreamException)
            {
                Interlocked.Increment(ref counters.OversizeDropped);
            }
            catch (IOException)
            {
                Interlocked.Increment(ref counters.OversizeDropped);
            }
            catch (TimeoutException)
            {
                // Server closed without a further frame; treat as drop if the next read fails.
                Interlocked.Increment(ref counters.OversizeDropped);
            }
        }
        catch
        {
            Interlocked.Increment(ref counters.OversizeProbeFail);
        }
    }

    private static async Task LoginRateProbeAsync(
        string address,
        int port,
        int connectTimeoutMs,
        LoadClientCounters counters)
    {
        await using var tcp = new LoadTcpClient();
        try
        {
            await tcp.ConnectAsync(address, port, TimeSpan.FromMilliseconds(connectTimeoutMs))
                .ConfigureAwait(false);
            _ = await tcp.ReadFrameAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
            for (var i = 0; i < 9; i++)
            {
                await tcp.SendFrameAsync(LoadPackets.Login("no-such-user", "password123"))
                    .ConfigureAwait(false);
                var result = await tcp.ReadUntilAsync(PacketId.LoginResult, TimeSpan.FromSeconds(10))
                    .ConfigureAwait(false);
                if (!LoadPackets.StatusOk(result))
                {
                    Interlocked.Increment(ref counters.LoginProbeRejected);
                }
            }
        }
        catch
        {
            Interlocked.Increment(ref counters.LoginProbeFail);
        }
    }

    public static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var p = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return p;
    }

    private static async Task WaitForAcceptAsync(
        string address,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var probe = new System.Net.Sockets.TcpClient();
                await probe.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException("server did not accept TCP within " + timeout, last);
    }
}
