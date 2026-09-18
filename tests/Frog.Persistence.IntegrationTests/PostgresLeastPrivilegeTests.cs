using System.Security.Cryptography;
using Frog.Persistence.IntegrationTests.Support;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// P10-5 D : frog_runtime (noms uniques par test) n'est pas superuser, DML sans DDL.
/// CREATE TABLE / DROP TABLE / CREATE ROLE refusés.
/// </summary>
[Collection("PostgresIsolated")]
public sealed class PostgresLeastPrivilegeTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresLeastPrivilegeTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task RuntimeRole_IsNotSuperuser_DmlAllowed_DdlAndCreateRoleDenied()
    {
        Assert.False(string.IsNullOrWhiteSpace(_fixture.ConnectionString));

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var runtime = "frog_rt_" + suffix;
        var migrate = "frog_mg_" + suffix;
        var publish = "frog_pb_" + suffix;
        var ops = "frog_op_" + suffix;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        await using var admin = new NpgsqlConnection(_fixture.ConnectionString);
        await admin.OpenAsync();

        try
        {
            await CreateLoginRoleAsync(admin, runtime, password);
            await CreateLoginRoleAsync(admin, migrate, password);
            await CreateLoginRoleAsync(admin, publish, password);
            await CreateLoginRoleAsync(admin, ops, password);
            await ApplyGrantsAsync(admin, runtime, migrate, publish, ops);

            await using var probe = new NpgsqlCommand(
                """
                SELECT rolsuper, rolcreatedb, rolcreaterole
                FROM pg_roles
                WHERE rolname = @name;
                """,
                admin);
            probe.Parameters.AddWithValue("name", runtime);
            await using (var reader = await probe.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync(), "runtime role missing");
                Assert.False(reader.GetBoolean(0), "frog_runtime must not be superuser");
                Assert.False(reader.GetBoolean(1), "frog_runtime must not CREATEDB");
                Assert.False(reader.GetBoolean(2), "frog_runtime must not CREATEROLE");
            }

            var runtimeCs = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
            {
                Username = runtime,
                Password = password,
            }.ConnectionString;

            await using var runtimeConn = new NpgsqlConnection(runtimeCs);
            await runtimeConn.OpenAsync();

            await using (var super = new NpgsqlCommand("SHOW is_superuser;", runtimeConn))
            {
                var flag = (string)(await super.ExecuteScalarAsync() ?? "on");
                Assert.Equal("off", flag);
            }

            var accountId = Guid.NewGuid();
            var username = "p10d_" + suffix;
            await using (var insert = new NpgsqlCommand(
                """
                INSERT INTO auth.accounts (id, username, password_hash, created_at_utc)
                VALUES (@id, @user, @hash, @created);
                """,
                runtimeConn))
            {
                insert.Parameters.AddWithValue("id", accountId);
                insert.Parameters.AddWithValue("user", username);
                insert.Parameters.AddWithValue("hash", "not-a-real-hash");
                insert.Parameters.AddWithValue("created", DateTimeOffset.UtcNow);
                Assert.Equal(1, await insert.ExecuteNonQueryAsync());
            }

            await using (var select = new NpgsqlCommand(
                "SELECT username FROM auth.accounts WHERE id = @id;",
                runtimeConn))
            {
                select.Parameters.AddWithValue("id", accountId);
                Assert.Equal(username, await select.ExecuteScalarAsync());
            }

            await using (var update = new NpgsqlCommand(
                "UPDATE auth.accounts SET password_hash = @hash WHERE id = @id;",
                runtimeConn))
            {
                update.Parameters.AddWithValue("hash", "rotated-hash");
                update.Parameters.AddWithValue("id", accountId);
                Assert.Equal(1, await update.ExecuteNonQueryAsync());
            }

            await using (var delete = new NpgsqlCommand(
                "DELETE FROM auth.accounts WHERE id = @id;",
                runtimeConn))
            {
                delete.Parameters.AddWithValue("id", accountId);
                Assert.Equal(1, await delete.ExecuteNonQueryAsync());
            }

            var createTableEx = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(runtimeConn, "CREATE TABLE auth.p10_priv_probe (id int);"));
            AssertDeniedPrivilege(createTableEx);

            await ExecuteAsync(admin, "CREATE TABLE IF NOT EXISTS auth.p10_priv_drop_probe (id int);");
            try
            {
                var dropTableEx = await Assert.ThrowsAsync<PostgresException>(() =>
                    ExecuteAsync(runtimeConn, "DROP TABLE auth.p10_priv_drop_probe;"));
                AssertDeniedPrivilege(dropTableEx);
            }
            finally
            {
                await ExecuteAsync(admin, "DROP TABLE IF EXISTS auth.p10_priv_drop_probe;");
            }

            var createRoleEx = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(runtimeConn, "CREATE ROLE p10_priv_probe_role;"));
            AssertDeniedPrivilege(createRoleEx);
        }
        finally
        {
            await DropRoleQuietlyAsync(admin, runtime);
            await DropRoleQuietlyAsync(admin, migrate);
            await DropRoleQuietlyAsync(admin, publish);
            await DropRoleQuietlyAsync(admin, ops);
        }
    }

    private static async Task ApplyGrantsAsync(
        NpgsqlConnection admin,
        string runtime,
        string migrate,
        string publish,
        string ops)
    {
        await SetGucAsync(admin, "frog.runtime_role", runtime);
        await SetGucAsync(admin, "frog.migrate_role", migrate);
        await SetGucAsync(admin, "frog.publish_role", publish);
        await SetGucAsync(admin, "frog.ops_role", ops);

        var sqlPath = Path.Combine(PostgresBackupScripts.RepoRoot, "scripts", "postgres-role-grants.sql");
        Assert.True(File.Exists(sqlPath), "missing " + sqlPath);
        var grantsSql = await File.ReadAllTextAsync(sqlPath);
        await using var apply = new NpgsqlCommand(grantsSql, admin);
        await apply.ExecuteNonQueryAsync();
    }

    private static async Task SetGucAsync(NpgsqlConnection admin, string name, string value)
    {
        await using var cmd = new NpgsqlCommand("SELECT set_config(@name, @value, false);", admin);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("value", value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateLoginRoleAsync(NpgsqlConnection admin, string role, string password)
    {
        // PASSWORD is a SQL literal (not a bind parameter). Test passwords are hex-only.
        Assert.Matches("^[0-9A-F]+$", password);
        await ExecuteAsync(
            admin,
            $"CREATE ROLE {QuoteIdent(role)} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT PASSWORD '{password}';");
    }

    private static async Task DropRoleQuietlyAsync(NpgsqlConnection admin, string role)
    {
        try
        {
            await ExecuteAsync(admin, $"DROP OWNED BY {QuoteIdent(role)};");
        }
        catch (PostgresException)
        {
        }

        try
        {
            await ExecuteAsync(admin, $"DROP ROLE IF EXISTS {QuoteIdent(role)};");
        }
        catch (PostgresException)
        {
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AssertDeniedPrivilege(PostgresException ex)
    {
        var text = ex.Message + " " + ex.SqlState;
        Assert.True(
            text.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || text.Contains("must be owner", StringComparison.OrdinalIgnoreCase)
            || text.Contains("must be superuser", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not allowed to create", StringComparison.OrdinalIgnoreCase)
            || text.Contains("CREATEROLE", StringComparison.OrdinalIgnoreCase)
            || ex.SqlState is "42501" or "42500",
            "expected privilege denial, got: " + ex.Message);
    }

    private static string QuoteIdent(string name)
    {
        Assert.Matches("^[A-Za-z_][A-Za-z0-9_]*$", name);
        return name;
    }
}
