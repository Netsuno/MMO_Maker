using Frog.Core.Enums;
using Frog.Core.Security;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// P9-3: empty DB migrates; pg_dump of a seeded world restores; PostgresDatabaseHealth is OK;
/// Phase 7 auth still works against the restored database.
/// </summary>
[Collection("PostgresIsolated")]
public sealed class PostgresBackupRestoreTests
{
    private const string Password = "password12345";

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EmptyMigrate_DumpSeededWorld_Restore_HealthOk_Phase7LoginWorks()
    {
        PostgresBackupScripts.AssertClientToolsAvailable();

        var admin = Environment.GetEnvironmentVariable(IsolatedPostgresFixture.ConnectionEnvironmentVariable);
        Assert.False(string.IsNullOrWhiteSpace(admin));

        await using var source = await DisposablePostgresDatabase.CreateAsync(admin!, "frog_p93s_");
        await using var dest = await DisposablePostgresDatabase.CreateAsync(admin!, "frog_p93d_");

        int appliedCount;
        string username;
        Guid mapId;
        Guid classId;

        await using (var sourceDb = new FrogDbContext(FrogDbContextOptions.Create(source.ConnectionString)))
        {
            await sourceDb.Database.MigrateAsync();
            var pending = (await sourceDb.Database.GetPendingMigrationsAsync()).ToArray();
            Assert.Empty(pending);
            appliedCount = (await sourceDb.Database.GetAppliedMigrationsAsync()).ToArray().Length;
            Assert.True(appliedCount > 0, "expected EF migrations to apply on an empty database");

            var health = new PostgresDatabaseHealth(sourceDb);
            var healthResult = await health.CheckAsync();
            Assert.True(healthResult.Ok, "empty migrate health: " + healthResult.Detail);
            Assert.Contains("OK", healthResult.Detail, StringComparison.Ordinal);
        }

        using (var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(source.ConnectionString))))
        {
            var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
            mapId = seed.MapId;
            classId = seed.ClassId;

            var accounts = new PostgresAccountRepository(gate);
            username = "br" + Guid.NewGuid().ToString("N")[..14];
            var created = await accounts.TryCreateAsync(username, Password);
            Assert.Equal(Frog.Application.Identity.AccountCreateStatus.Created, created.Status);
        }

        NpgsqlConnection.ClearAllPools();

        var dumpPath = Path.Combine(Path.GetTempPath(), "frog_p93_" + Guid.NewGuid().ToString("N") + ".dump");
        try
        {
            await PostgresBackupScripts.BackupAsync(source.ConnectionString, dumpPath);
            Assert.True(new FileInfo(dumpPath).Length > 0);

            await PostgresBackupScripts.RestoreAsync(dest.ConnectionString, dumpPath);
            await PostgresBackupScripts.VerifyAsync(dest.ConnectionString);

            var restoreAgain = await Assert.ThrowsAsync<InvalidOperationException>(
                () => PostgresBackupScripts.RestoreAsync(dest.ConnectionString, dumpPath));
            Assert.Contains("already has", restoreAgain.Message, StringComparison.OrdinalIgnoreCase);

            await using (var destDb = new FrogDbContext(FrogDbContextOptions.Create(dest.ConnectionString)))
            {
                var pending = (await destDb.Database.GetPendingMigrationsAsync()).ToArray();
                Assert.Empty(pending);
                var applied = (await destDb.Database.GetAppliedMigrationsAsync()).ToArray();
                Assert.Equal(appliedCount, applied.Length);

                await destDb.Database.MigrateAsync();
                Assert.Empty((await destDb.Database.GetPendingMigrationsAsync()).ToArray());

                var health = new PostgresDatabaseHealth(destDb);
                var healthResult = await health.CheckAsync();
                Assert.True(healthResult.Ok, "post-restore health: " + healthResult.Detail);
                Assert.Contains("OK", healthResult.Detail, StringComparison.Ordinal);
                Assert.Contains(
                    "migrations=" + appliedCount,
                    healthResult.Detail,
                    StringComparison.Ordinal);

                var schemaNames = await ListFrogSchemasAsync(dest.ConnectionString);
                Assert.Equal(PostgresBackupScripts.ProductSchemas, schemaNames);

                Assert.True(await destDb.Maps.AsNoTracking().AnyAsync(m => m.Id == mapId));
                Assert.True(await destDb.AuthAccounts.AsNoTracking().AnyAsync(a => a.Username == username));
                // P9-2 table must be present after restore (empty until operators are granted).
                Assert.Equal(0, await destDb.AuthOperators.AsNoTracking().CountAsync());
            }

            using (var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(dest.ConnectionString))))
            {
                var accounts = new PostgresAccountRepository(gate);
                var found = await accounts.FindByUsernameAsync(username);
                Assert.NotNull(found);
                Assert.True(PasswordHasher.VerifyPassword(Password, found!.PasswordHash));
            }

            var port = Phase7TcpTestPorts.GetFreePort();
            using var host = Phase7PostgresE2EHost.CreateBuilder(dest.ConnectionString, port).Build();
            await host.StartAsync();
            try
            {
                await using var client = new Phase7TcpTestClient();
                await client.ConnectAsync("127.0.0.1", port);
                Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);

                await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(username, Password));
                var login = await client.ReadUntilAsync(PacketId.LoginResult);
                var token = Phase7WireDecoders.DecodeLoginToken(login);
                Assert.False(string.IsNullOrWhiteSpace(token));

                await using var registerClient = new Phase7TcpTestClient();
                await registerClient.ConnectAsync("127.0.0.1", port);
                Assert.Equal((byte)PacketId.Hello, (await registerClient.ReadFrameAsync())[0]);
                var newUser = "bn" + Guid.NewGuid().ToString("N")[..14];
                await registerClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(newUser, Password));
                Assert.NotEqual(0, (await registerClient.ReadUntilAsync(PacketId.RegisterResult))[1]);
                await registerClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(newUser, Password));
                Assert.False(string.IsNullOrWhiteSpace(
                    Phase7WireDecoders.DecodeLoginToken(await registerClient.ReadUntilAsync(PacketId.LoginResult))));
                await registerClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate("BrHero", classId));
                Assert.NotEqual(0, (await registerClient.ReadUntilAsync(PacketId.CharacterCreateResult))[1]);
            }
            finally
            {
                await host.StopAsync();
            }
        }
        finally
        {
            try
            {
                File.Delete(dumpPath);
            }
            catch
            {
                // temp cleanup
            }
        }
    }

    private static async Task<string[]> ListFrogSchemasAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT nspname
            FROM pg_namespace
            WHERE nspname IN ('auth', 'content', 'ops', 'player', 'world')
            ORDER BY 1;
            """,
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }
}
