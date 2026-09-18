using Frog.LoadHarness;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresLoadObservabilityTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresLoadObservabilityTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    [Trait("Category", "Load")]
    public async Task AttachedHarness_FourAuthedSessions_ZeroPostgresErrors()
    {
        using (var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString))))
        {
            await Phase7PostgresContentSeed.PublishAsync(gate);
        }

        var port = Phase7TcpTestPorts.GetFreePort();
        var (builder, _) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var report = await LoadHarnessRunner.RunAsync(new LoadHarnessOptions
            {
                SelfHost = false,
                Host = "127.0.0.1",
                Port = port,
                Scenario = "mixed",
                Sessions = 4,
                HoldMilliseconds = 400,
                ChatBurst = 12,
                MoveBurst = 80,
                MaxParallelAuth = 4,
                ConnectTimeoutMs = 20_000,
            });

            Assert.Equal(4, report.Client.HelloOk);
            Assert.Equal(4, report.Client.RegisterOk);
            Assert.Equal(4, report.Client.LoginOk);
            Assert.Equal(4, report.Client.CharacterSelectOk);
            Assert.True(report.Client.ChatRateLimited >= 4);
            Assert.True(report.Client.OversizeDropped >= 1);

            var ops = host.Services.GetRequiredService<ServerOpsMetrics>().Snapshot();
            Assert.True(ops.ConnectionsAccepted >= 6);
            Assert.True(ops.ConnectionsRejected >= 1);
            Assert.True(ops.RateLimitHitsChat >= 1);
            Assert.True(ops.RateLimitHitsMovement >= 1);
            Assert.Equal(0, ops.PostgresErrors);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
