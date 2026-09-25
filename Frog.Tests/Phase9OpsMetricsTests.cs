using System;
using System.Threading.Tasks;
using Frog.LoadHarness;
using Frog.Server.Database;
using Frog.Server.Observability;
using Frog.Server.Security;
using Frog.Server.Services;
using Xunit;

namespace Frog.Tests;

public sealed class Phase9OpsMetricsTests
{
    [Fact]
    public void IsPostgresError_MatchesTypeName_NotGenericFailures()
    {
        Assert.True(ServerOpsMetrics.IsPostgresError(new FakeNpgsqlException()));
        Assert.True(ServerOpsMetrics.IsPostgresError(new InvalidOperationException("wrap", new FakePostgresException())));
        Assert.False(ServerOpsMetrics.IsPostgresError(new InvalidOperationException("disk full")));
        Assert.False(ServerOpsMetrics.IsPostgresError(new TimeoutException()));
    }

    [Fact]
    public void Counters_IncrementIndependently()
    {
        var m = new ServerOpsMetrics();
        m.RecordConnectionAccepted();
        m.RecordConnectionAccepted();
        m.RecordConnectionRejected("oversize_frame");
        m.RecordRateLimitHit("chat");
        m.RecordRateLimitHit("movement");
        m.RecordRateLimitHit("login");
        m.RecordPostgresError();
        var snap = m.Snapshot(activeSessions: 3);
        Assert.Equal(2, snap.ConnectionsAccepted);
        Assert.Equal(1, snap.ConnectionsRejected);
        Assert.Equal(3, snap.RateLimitHits);
        Assert.Equal(1, snap.RateLimitHitsChat);
        Assert.Equal(1, snap.RateLimitHitsMovement);
        Assert.Equal(1, snap.RateLimitHitsLogin);
        Assert.Equal(1, snap.PostgresErrors);
        Assert.Equal(3, snap.ActiveSessions);
        Assert.Equal(1, snap.RejectReasons["oversize_frame"]);
    }

    [Fact]
    public async Task AuthService_RecordsLoginRateLimit()
    {
        var metrics = new ServerOpsMetrics();
        var auth = new AuthService(new InMemoryAccountRepository(), new AuthRateLimiter(ipUserMaxFailures: 2), metrics);
        Assert.False((await auth.TryAuthenticateAsync("ghost-user", "password123", "k")).Success);
        Assert.False((await auth.TryAuthenticateAsync("ghost-user", "password123", "k")).Success);
        var third = await auth.TryAuthenticateAsync("ghost-user", "password123", "k");
        Assert.False(third.Success);
        Assert.True(third.RateLimited);
        Assert.Equal(1, metrics.Snapshot().RateLimitHitsLogin);
    }

    [Fact]
    [Trait("Category", "Load")]
    public async Task LoadHarness_Mixed_FourSessions_HelloChatMoveOversize()
    {
        var report = await LoadHarnessRunner.RunAsync(new LoadHarnessOptions
        {
            SelfHost = true,
            Scenario = "mixed",
            Sessions = 4,
            HoldMilliseconds = 400,
            ChatBurst = 12,
            MoveBurst = 80,
            MaxParallelAuth = 4,
            ConnectTimeoutMs = 10_000,
        });

        Assert.Equal(4, report.Client.HelloOk);
        Assert.Equal(4, report.Client.TcpConnectOk);
        Assert.Equal(4, report.Client.RegisterOk);
        Assert.Equal(4, report.Client.LoginOk);
        Assert.True(
            report.Client.CharacterSelectOk == 4,
            "CharacterSelectOk=" + report.Client.CharacterSelectOk
            + " createOk=" + report.Client.CharacterCreateOk
            + " createFail=" + report.Client.CharacterCreateFail
            + " selectFail=" + report.Client.CharacterSelectFail
            + " authEx=" + report.Client.AuthenticateException
            + " loginOk=" + report.Client.LoginOk);
        Assert.True(report.Client.ChatRateLimited >= 4, "each session should exceed 8/10s chat cap");
        Assert.True(report.Client.MoveRateLimited >= 4, "each session should exceed 50/s movement cap");
        Assert.Equal(1, report.Client.OversizeDropped);
        Assert.NotNull(report.ServerOps);
        Assert.True(report.ServerOps!.ConnectionsAccepted >= 6, "4 workers + oversize + login probe");
        Assert.True(report.ServerOps.ConnectionsRejected >= 1, "oversize frame must increment rejects");
        Assert.True(report.ServerOps.RateLimitHitsChat >= 1);
        Assert.True(report.ServerOps.RateLimitHitsMovement >= 1);
        Assert.True(report.ServerOps.RateLimitHitsLogin >= 1);
        Assert.Equal(0, report.ServerOps.PostgresErrors);
    }

    private sealed class FakeNpgsqlException : Exception;

    private sealed class FakePostgresException : Exception;
}
