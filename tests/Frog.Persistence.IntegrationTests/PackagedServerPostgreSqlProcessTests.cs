using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.IntegrationTests.Support;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// P7-R2: proves Release packaged Frog.Server can load PostgreSQL via reflection
/// with full runtime assemblies (Npgsql, EF Core) — not the in-process DI test host.
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

        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);

        var port = GetFreePort();
        WritePackagedServerConfig(publishDir, _fixture.ConnectionString, port);

        using var process = StartPackagedServer(publishDir);
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

        try
        {
            await WaitForServerReadyAsync(process, port, logLines, TimeSpan.FromSeconds(60));

            var logText = string.Join(Environment.NewLine, logLines);
            Assert.Contains("Published world loaded", logText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Published monster spawns loaded", logText, StringComparison.OrdinalIgnoreCase);

            await using var client = new Phase7TcpTestClient();
            await client.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);

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
            await StopServerAsync(process);
        }

        Assert.True(process.HasExited);
        Assert.False(await IsPortOpenAsync(port));
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

    private static Process StartPackagedServer(string publishDir)
    {
        var dll = Path.Combine(publishDir, "Frog.Server.dll");
        Assert.True(File.Exists(dll), $"missing packaged server: {dll}");

        return Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dll}\"",
            WorkingDirectory = publishDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Failed to start packaged Frog.Server.");
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

    private static async Task StopServerAsync(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // best-effort
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // already killed
        }
    }

    private static async Task AssertNoActiveDbSessionsAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        builder.Database = "postgres";
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
        var active = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        Assert.Equal(0, active);
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
