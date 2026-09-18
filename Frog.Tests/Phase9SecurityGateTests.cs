using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Core.Security;
using Frog.Server.Config;
using Frog.Server.Database;
using Frog.Server.Security;
using Xunit;

namespace Frog.Tests;

public sealed class Phase9SecurityGateTests
{
    [Fact]
    public void PasswordHasher_RejectsMalformedV1AndBlankStoredHash()
    {
        Assert.False(PasswordHasher.VerifyPassword("secret", "$frog-v1$pbkdf2-sha256$600000$not-base64$also-bad"));
        Assert.False(PasswordHasher.VerifyPassword("secret", "$frog-v1$pbkdf2-sha256$0$YWFh$YmJi"));
        Assert.False(PasswordHasher.VerifyOrTimingSafeReject("probe", "   ", null));
    }

    [Fact]
    public void AccountInputRules_RegisterIsStricterThanLogin()
    {
        Assert.False(AccountInputRules.IsValidUsername("ab"));
        Assert.False(AccountInputRules.IsValidUsername("user name"));
        Assert.False(AccountInputRules.IsValidUsername("bad!"));
        Assert.True(AccountInputRules.IsValidUsername("gm-op_01"));
        Assert.False(AccountInputRules.IsValidPassword("short"));
        Assert.True(AccountInputRules.IsValidLoginPassword("short"));
        Assert.True(AccountInputRules.IsValidPassword(new string('a', 8)));
        Assert.False(AccountInputRules.IsValidPassword(new string('a', 129)));
        Assert.False(AccountInputRules.IsValidLoginPassword(new string('a', 129)));
    }

    [Fact]
    public void ChatRateLimiter_BlocksAfterWindowCap_AndResetClears()
    {
        var limiter = new ChatRateLimiter(maxMessages: 3, window: TimeSpan.FromMinutes(5));
        var session = Guid.NewGuid();
        Assert.True(limiter.TryAllow(session));
        Assert.True(limiter.TryAllow(session));
        Assert.True(limiter.TryAllow(session));
        Assert.False(limiter.TryAllow(session));
        limiter.Reset(session);
        Assert.True(limiter.TryAllow(session));
    }

    [Fact]
    public async Task OperatorDirectory_DefaultAccountIsNotOperator_GrantRevokeRoundTrip()
    {
        var accounts = new InMemoryAccountRepository();
        var ops = new InMemoryOperatorDirectory(accounts);
        var created = await accounts.TryCreateAsync("plain-player", "password123");
        Assert.Equal(AccountCreateStatus.Created, created.Status);
        Assert.False(await ops.IsOperatorAsync(created.AccountId!.Value));

        var missing = await ops.GrantAsync(Guid.NewGuid(), "bootstrap");
        Assert.Equal(OperatorGrantStatus.AccountNotFound, missing.Status);

        var granted = await ops.GrantAsync(created.AccountId.Value, "bootstrap", "p9-2 test");
        Assert.Equal(OperatorGrantStatus.Granted, granted.Status);
        Assert.True(await ops.IsOperatorAsync(created.AccountId.Value));
        Assert.True(await ops.RevokeAsync(created.AccountId.Value));
        Assert.False(await ops.IsOperatorAsync(created.AccountId.Value));
    }

    [Fact]
    public async Task DemoBootstrapAccount_IsNotAnOperator()
    {
        var accounts = new InMemoryAccountRepository();
        var ops = new InMemoryOperatorDirectory(accounts);
        var demo = await accounts.FindByUsernameAsync("demo");
        Assert.NotNull(demo);
        Assert.False(await ops.IsOperatorAsync(demo!.Id));
    }

    [Fact]
    public void PacketId_HasModerationOpcodes_ButNoGrantRevoke()
    {
        var names = Enum.GetNames<PacketId>();
        Assert.Contains("ModerateRequest", names);
        Assert.Contains("ModerateResult", names);
        Assert.DoesNotContain("GrantOperator", names);
        Assert.DoesNotContain("RevokeOperator", names);
        Assert.DoesNotContain("AdminCommand", names);
        Assert.Contains("WorldFlagsPatchRequest", names);
    }

    [Fact]
    public void WorldFlagsPatchPolicy_RejectsPostgresAndProductionComposition()
    {
        Assert.True(WorldFlagsPatchPolicy.IsRejected(postgresEnabled: true, playtestEnabled: false, allowInMemoryFallback: false));
        Assert.True(WorldFlagsPatchPolicy.IsRejected(postgresEnabled: false, playtestEnabled: false, allowInMemoryFallback: false));
        Assert.False(WorldFlagsPatchPolicy.IsRejected(postgresEnabled: false, playtestEnabled: true, allowInMemoryFallback: true));
        Assert.False(WorldFlagsPatchPolicy.IsRejected(postgresEnabled: false, playtestEnabled: false, allowInMemoryFallback: true));
    }

    [Fact]
    public void ServerOptions_RejectsNonLoopbackWithoutExplicitFlag()
    {
        var loopback = new ServerOptions { BindAddress = "127.0.0.1", Port = 6000 };
        loopback.Validate();
        Assert.True(loopback.IsLoopbackBind);

        var publicBind = new ServerOptions { BindAddress = "0.0.0.0", Port = 6000 };
        Assert.False(publicBind.IsLoopbackBind);
        Assert.Throws<InvalidOperationException>(() => publicBind.Validate());

        var allowed = new ServerOptions
        {
            BindAddress = "0.0.0.0",
            Port = 6000,
            AllowNonLoopbackBind = true,
        };
        allowed.Validate();

        var badIp = new ServerOptions { BindAddress = "not-an-ip", Port = 6000 };
        Assert.Throws<ArgumentException>(() => badIp.Validate());
    }

    [Fact]
    public void PlaceholderSecretPolicy_RejectsPublicBindWithCommittedTokens()
    {
        Assert.True(PlaceholderSecretPolicy.ContainsKnownPlaceholder(
            "Host=127.0.0.1;Password=NOT_A_PRODUCTION_SECRET"));
        Assert.True(PlaceholderSecretPolicy.ContainsKnownPlaceholder(
            "Host=127.0.0.1;Password=changeme"));
        Assert.False(PlaceholderSecretPolicy.ContainsKnownPlaceholder(
            "Host=db.example;Password=unique-hosted-secret-value"));
        Assert.True(PlaceholderSecretPolicy.MustRejectPublicBind(
            playtestEnabled: false,
            bindIsLoopback: false,
            postgreSqlConnectionString: "Password=frog_dev_only",
            mariaDbConnectionString: null));
        Assert.False(PlaceholderSecretPolicy.MustRejectPublicBind(
            playtestEnabled: false,
            bindIsLoopback: true,
            postgreSqlConnectionString: "Password=frog_dev_only",
            mariaDbConnectionString: null));
        Assert.False(PlaceholderSecretPolicy.MustRejectPublicBind(
            playtestEnabled: true,
            bindIsLoopback: false,
            postgreSqlConnectionString: "Password=changeme",
            mariaDbConnectionString: null));
    }

    [Fact]
    public void CommittedAppsettings_UsePlaceholdersAndLoopbackBind()
    {
        var root = RepoRoot();
        var json = File.ReadAllText(Path.Combine(root, "Frog.Server", "appsettings.json"));
        using var doc = JsonDocument.Parse(json);
        var server = doc.RootElement.GetProperty("Server");
        Assert.Equal("127.0.0.1", server.GetProperty("bindAddress").GetString());
        Assert.False(server.GetProperty("allowNonLoopbackBind").GetBoolean());
        var pg = doc.RootElement.GetProperty("PostgreSql").GetProperty("connectionString").GetString();
        Assert.True(PlaceholderSecretPolicy.ContainsKnownPlaceholder(pg));
        Assert.DoesNotContain("Password=changeme", json, StringComparison.Ordinal);
        var gitignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        Assert.Contains("appsettings.Local.json", gitignore, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "Frog.Server", "appsettings.Local.json.example")));
        Assert.False(File.Exists(Path.Combine(root, "Frog.Server", "appsettings.Local.json")));
    }

    [Fact]
    public void Ipv6Loopback_IsAllowedWithoutFlag()
    {
        var v6 = new ServerOptions { BindAddress = IPAddress.IPv6Loopback.ToString(), Port = 6000 };
        v6.Validate();
        Assert.True(v6.IsLoopbackBind);
    }

    private static string RepoRoot()
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

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
