using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frog.Application.Playtest;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestChildEnvironmentTests
{
    [Fact]
    public async Task Probe_RemovesDbSecrets_FromServerAndClientChildProcesses()
    {
        var serverLeaks = await PlaytestChildEnvironment.ProbeForbiddenKeysInChildAsync("server");
        var clientLeaks = await PlaytestChildEnvironment.ProbeForbiddenKeysInChildAsync("client");

        Assert.Empty(serverLeaks);
        Assert.Empty(clientLeaks);
    }

    [Fact]
    public void Sanitize_RemovesKnownAndVariantKeys_WithoutLoggingValues()
    {
        var env = new Dictionary<string, string?>
        {
            ["FROG_POSTGRES_CONNECTION_STRING"] = "Host=secret",
            ["FROG_POSTGRES_TEST_CONNECTION_STRING"] = "Host=secret-test",
            ["POSTGRES_PASSWORD"] = "pw",
            ["PGPASSWORD"] = "pw2",
            ["ConnectionStrings__PostgreSql"] = "cs",
            ["ConnectionStrings__DefaultConnection"] = "cs2",
            ["Custom_POSTGRES_CONNECTION_URI"] = "postgres://x",
            ["FROG_PLAYTEST_PORT"] = "7777",
            ["PATH"] = "/usr/bin",
        };

        PlaytestChildEnvironment.Sanitize((IDictionary<string, string?>)env);

        Assert.False(env.ContainsKey("FROG_POSTGRES_CONNECTION_STRING"));
        Assert.False(env.ContainsKey("FROG_POSTGRES_TEST_CONNECTION_STRING"));
        Assert.False(env.ContainsKey("POSTGRES_PASSWORD"));
        Assert.False(env.ContainsKey("PGPASSWORD"));
        Assert.False(env.ContainsKey("ConnectionStrings__PostgreSql"));
        Assert.False(env.ContainsKey("ConnectionStrings__DefaultConnection"));
        Assert.False(env.ContainsKey("Custom_POSTGRES_CONNECTION_URI"));
        Assert.Equal("7777", env["FROG_PLAYTEST_PORT"]);
        Assert.Equal("/usr/bin", env["PATH"]);
    }
}
