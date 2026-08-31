using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Services;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// P7-R2: proves Release packaged Frog.Server can load PostgreSQL via reflection
/// with full runtime assemblies (Npgsql, EF Core) — not the in-process DI test host.
///
/// P7-G6: shutdown must be a genuine graceful stop, not <c>Process.Kill</c>. The packaged
/// server is asked to stop the same way a supervisor would (SIGTERM on Linux, handled by
/// <c>ConsoleLifetime</c>; a shutdown-sentinel file elsewhere), then we wait for a normal
/// process exit (code 0) and let PostgreSQL sessions drain on their own — no
/// <c>pg_terminate_backend</c> in the success path, and <c>Process.Kill</c> is reserved for
/// failure cleanup only if the graceful stop times out.
/// </summary>
[Collection("PostgresIsolated")]
public sealed class PackagedServerPostgreSqlProcessTests
{
    private static readonly string[] RequiredRuntimeAssemblies =
    [
        "Frog.Persistence.PostgreSql.dll",
        "Npgsql.dll",
        "Npgsql.EntityFrameworkCore.PostgreSQL.dll",
        "Microsoft.EntityFrameworkCore.dll",
        "Microsoft.EntityFrameworkCore.Relational.dll",
        "EFCore.NamingConventions.dll",
    ];

    private readonly IsolatedPostgresFixture _fixture;

    public PackagedServerPostgreSqlProcessTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ReleasePackagedServer_PostgreSqlEnabled_LoginAndShopBuy_Persists()
    {
        var publishDir = await PublishReleaseServerAsync();
        AssertPackagedRuntimeAssemblies(publishDir);

        Phase7PostgresContentSeedResult seed;
        using (var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString))))
        {
            seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        }

        NpgsqlConnection.ClearAllPools();

        var port = GetFreePort();
        WritePackagedServerConfig(publishDir, _fixture.ConnectionString, port);
        var shutdownFilePath = Path.Combine(publishDir, ".frog-shutdown-request");

        using var process = StartPackagedServer(publishDir, shutdownFilePath);
        var logLines = new List<string>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (logLines)
                {
                    logLines.Add(e.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (logLines)
                {
                    logLines.Add(e.Data);
                }
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // P7-G6 requirement #5: keep a second, otherwise-idle client connected through the
        // graceful shutdown below so we can assert it observes an orderly closure rather than
        // hanging forever or crashing with a raw socket error mid-operation. Declared outside
        // the try/finally so it survives to the post-shutdown assertions below.
        await using var shutdownProbeClient = new Phase7TcpTestClient();

        int exitCode;
        try
        {
            await WaitForServerReadyAsync(process, port, logLines, TimeSpan.FromSeconds(60));

            var logText = string.Join(Environment.NewLine, logLines);
            Assert.Contains("Published world loaded", logText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Published monster spawns loaded", logText, StringComparison.OrdinalIgnoreCase);

            await using var client = new Phase7TcpTestClient();
            await client.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);

            await shutdownProbeClient.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await shutdownProbeClient.ReadFrameAsync())[0]);

            var user = $"pkg-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.RegisterResult))[1]);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
            var login = await client.ReadUntilAsync(PacketId.LoginResult);
            Assert.NotEqual(0, login[1]);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate("PkgHero", seed.ClassId));
            var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
            Assert.NotEqual(0, create[1]);
            var characterId = Phase7WireDecoders.DecodeCharacterId(create);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);

            var combat = await client.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                combat, out _, out _, out _, out _, out _, out _, out var startGold, out _));
            Assert.Equal(GameplayLimits.StartingGold, startGold);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(
                seed.ShopId, seed.ConsumableId, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var invAfterBuy = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterBuy, out var buySnap));
            Assert.Contains(buySnap.Slots, s => s.ItemId == seed.ConsumableId);

            await client.DisconnectAsync();

            await using var verifyConn = new NpgsqlConnection(_fixture.ConnectionString);
            await verifyConn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """
                SELECT COUNT(*)::int
                FROM player.inventory_slots s
                JOIN player.characters c ON c.id = s.character_id
                JOIN auth.accounts a ON a.id = c.account_id
                WHERE a.username = @user AND s.item_id = @itemId AND s.quantity > 0
                """,
                verifyConn);
            cmd.Parameters.AddWithValue("user", user);
            cmd.Parameters.AddWithValue("itemId", seed.ConsumableId);
            var persistedQty = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            Assert.True(persistedQty > 0, "shop buy must persist to PostgreSQL before server shutdown");
        }
        finally
        {
            exitCode = await StopServerGracefullyAsync(process, shutdownFilePath, logLines);
        }

        NpgsqlConnection.ClearAllPools();
        Assert.True(process.HasExited);
        Assert.Equal(0, exitCode);
        Assert.False(await IsPortOpenAsync(port));
        await AssertClientObservesOrderlyCloseAsync(shutdownProbeClient);
        await AssertNoActiveDbSessionsAsync(_fixture.ConnectionString);
    }

    private static async Task<string> PublishReleaseServerAsync()
    {
        var publishDir = Path.Combine(
            Path.GetTempPath(),
            "frog-packaged-server-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(publishDir);

        var repoRoot = FindRepoRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{Path.Combine(repoRoot, "Frog.Server", "Frog.Server.csproj")}\" -c Release -o \"{publishDir}\"",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var publish = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet publish.");
        await publish.WaitForExitAsync();
        Assert.True(publish.ExitCode == 0, $"dotnet publish failed with exit code {publish.ExitCode}");

        return publishDir;
    }

    private static void AssertPackagedRuntimeAssemblies(string publishDir)
    {
        foreach (var assembly in RequiredRuntimeAssemblies)
        {
            Assert.True(File.Exists(Path.Combine(publishDir, assembly)), $"missing packaged runtime assembly: {assembly}");
        }

        var depsPath = Path.Combine(publishDir, "Frog.Server.deps.json");
        Assert.True(File.Exists(depsPath), "Frog.Server.deps.json missing from packaged output");
        using var doc = JsonDocument.Parse(File.ReadAllText(depsPath));
        Assert.Contains(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            doc.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void WritePackagedServerConfig(string publishDir, string connectionString, int port)
    {
        // Host factory re-loads appsettings.json after env vars; Local file is the production override path.
        var configPath = Path.Combine(publishDir, "appsettings.Local.json");
        var json = JsonSerializer.Serialize(new
        {
            Server = new { Port = port, BindAddress = "127.0.0.1" },
            MariaDb = new { Enabled = false },
            PostgreSql = new
            {
                Enabled = true,
                AllowInMemoryFallback = false,
                ConnectionString = connectionString,
            },
        });
        File.WriteAllText(configPath, json);
    }

    private static Process StartPackagedServer(string publishDir, string shutdownFilePath)
    {
        var dll = Path.Combine(publishDir, "Frog.Server.dll");
        Assert.True(File.Exists(dll), $"missing packaged server: {dll}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dll}\"",
            WorkingDirectory = publishDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // P7-G6: cross-platform graceful-stop path (Frog.Server watches for this file and calls
        // IHostApplicationLifetime.StopApplication() when it appears). See RequestGracefulShutdown.
        startInfo.Environment[ShutdownFileWatcherService.ShutdownFileEnvironmentVariable] = shutdownFilePath;

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start packaged Frog.Server.");
    }

    private static async Task WaitForServerReadyAsync(
        Process process,
        int port,
        List<string> logLines,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Packaged server exited early ({process.ExitCode}):\n{string.Join('\n', logLines)}");
            }

            if (await IsPortOpenAsync(port))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException(
            $"Packaged server did not bind port {port} in time.\n{string.Join('\n', logLines)}");
    }

    /// <summary>
    /// P7-G6: request an orderly stop (platform-appropriate signal on Unix, a shutdown-sentinel
    /// file as the cross-platform fallback) and wait for the process to exit normally. Killing
    /// the process is reserved for failure cleanup only, after the graceful stop has already
    /// timed out — never as part of the success path.
    /// </summary>
    private static async Task<int> StopServerGracefullyAsync(
        Process process,
        string shutdownFilePath,
        IReadOnlyList<string> logLines)
    {
        if (process.HasExited)
        {
            return process.ExitCode;
        }

        RequestGracefulShutdown(process, shutdownFilePath);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            ForceKillAfterGracefulShutdownTimeout(process);
            throw new TimeoutException(
                "Packaged server did not shut down gracefully (SIGTERM / FROG_SHUTDOWN_FILE) "
                + $"within 20s; force-killed as failure cleanup only, this is not a pass.\n"
                + string.Join('\n', logLines));
        }
    }

    /// <summary>
    /// Sends SIGTERM directly via libc on Unix — the same signal a container/systemd supervisor
    /// would send, and the one <c>Host.CreateDefaultBuilder</c>'s <c>ConsoleLifetime</c> handles
    /// by calling <see cref="Microsoft.Extensions.Hosting.IHostApplicationLifetime.StopApplication"/>.
    /// Also writes the shutdown-sentinel file every time: it is the only reliable mechanism on
    /// Windows (delivering Ctrl+C to a separately-started process is not practical there), and on
    /// Unix it is a harmless, redundant confirmation that the file-watch path also works.
    /// </summary>
    private static void RequestGracefulShutdown(Process process, string shutdownFilePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            NativeSignal.TrySendSigterm(process.Id);
        }

        File.WriteAllText(shutdownFilePath, string.Empty);
    }

    private static void ForceKillAfterGracefulShutdownTimeout(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // best-effort; the caller already fails the test via TimeoutException.
        }
    }

    private static class NativeSignal
    {
        private const int Sigterm = 15;

        public static bool TrySendSigterm(int pid)
        {
            try
            {
                return kill(pid, Sigterm) == 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);
    }

    /// <summary>
    /// P7-G6: PostgreSQL sessions must return to zero on their own once the packaged server has
    /// exited gracefully — retry with a delay only. No <c>pg_terminate_backend</c> here: forcing
    /// backends closed would mask a server that isn't actually releasing its connections on a
    /// clean stop.
    /// </summary>
    private static async Task AssertNoActiveDbSessionsAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        builder.Database = "postgres";
        var active = -1;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await using var conn = new NpgsqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """
                SELECT COUNT(*)::int
                FROM pg_stat_activity
                WHERE datname = @db
                  AND pid <> pg_backend_pid()
                  AND application_name NOT LIKE 'pg_%'
                """,
                conn);
            cmd.Parameters.AddWithValue("db", databaseName!);
            active = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            if (active == 0)
            {
                return;
            }

            await Task.Delay(250);
        }

        Assert.Equal(0, active);
    }

    /// <summary>
    /// P7-G6 requirement #5: a client that was still connected when the server shut down
    /// gracefully must see its connection close deterministically (FIN/reset/disposed — any
    /// definite closed-connection signal), not hang forever waiting on a read that never
    /// completes.
    /// </summary>
    private static async Task AssertClientObservesOrderlyCloseAsync(Phase7TcpTestClient probeClient)
    {
        Exception? observed = null;
        try
        {
            await probeClient.ReadFrameAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            observed = ex;
        }

        Assert.True(
            observed is EndOfStreamException or IOException or SocketException or ObjectDisposedException,
            "expected a still-connected client to observe an orderly close when the packaged "
            + $"server shut down gracefully; got: {observed?.GetType().FullName ?? "no exception (a frame was received instead)"}");
    }

    private static async Task<bool> IsPortOpenAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(300);
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Frog.Creator.sln.");
    }
}
