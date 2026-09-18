using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Server.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase9SecurityGateTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase9SecurityGateTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task OperatorDirectory_UnprivilegedAccountCannotBeOperator_UntilGranted()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var accounts = new PostgresAccountRepository(gate);
        var operators = new PostgresOperatorDirectory(gate);

        var created = await accounts.TryCreateAsync($"p9op-{Guid.NewGuid():N}"[..16], "password12345");
        Assert.Equal(AccountCreateStatus.Created, created.Status);
        Assert.False(await operators.IsOperatorAsync(created.AccountId!.Value));

        var missing = await operators.GrantAsync(Guid.NewGuid(), "bootstrap");
        Assert.Equal(OperatorGrantStatus.AccountNotFound, missing.Status);

        var granted = await operators.GrantAsync(created.AccountId.Value, "bootstrap", "p9-2");
        Assert.Equal(OperatorGrantStatus.Granted, granted.Status);
        Assert.True(await operators.IsOperatorAsync(created.AccountId.Value));
        Assert.True(await operators.RevokeAsync(created.AccountId.Value));
        Assert.False(await operators.IsOperatorAsync(created.AccountId.Value));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task WorldFlagsPatchRequest_RejectedForAuthenticatedPlayer_InPostgresProduction()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(seedGate);
        var port = Phase7TcpTestPorts.GetFreePort();

        var (builder, logs) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            await using var client = new Phase7TcpTestClient();
            var user = $"p9wf-{Guid.NewGuid():N}"[..16];
            await client.ConnectAsync("127.0.0.1", port);
            _ = await client.ReadFrameAsync();
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, "password12345"));
            _ = await client.ReadUntilAsync(PacketId.RegisterResult);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, "password12345"));
            _ = await client.ReadUntilAsync(PacketId.LoginResult);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate("Flags", seed.ClassId));
            var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
            var charId = Phase7WireDecoders.DecodeCharacterId(create);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(charId));
            _ = await client.ReadUntilAsync(PacketId.CharacterSelectResult);
            await client.DrainPendingAsync();

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildWorldFlagsPatch("""{"story_intro":true}"""));
            var result = await client.ReadUntilAsync(PacketId.WorldFlagsPatchResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(result, out var ok, out var message));
            Assert.False(ok);
            Assert.Equal(WorldFlagsPatchPolicy.RejectedMessage, message);

            var directory = host.Services.GetRequiredService<IOperatorDirectory>();
            Assert.IsType<PostgresOperatorDirectory>(directory);
        }
        finally
        {
            await host.StopAsync();
            logs.AssertNoUnexpectedErrors();
        }
    }
}
