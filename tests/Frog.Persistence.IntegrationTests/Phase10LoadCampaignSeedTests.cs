using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// Invoked by <c>scripts/phase10-hosted-load-campaign.sh</c> to migrate + seed a disposable DSN.
/// No-op when <c>FROG_P108_SEED_CONNECTION_STRING</c> is absent (normal CI).
/// </summary>
public sealed class Phase10LoadCampaignSeedTests
{
    [Fact]
    public async Task MigrateAndSeedPhase7_WhenCampaignDsnPresent()
    {
        var dsn = Environment.GetEnvironmentVariable("FROG_P108_SEED_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(dsn))
        {
            return;
        }

        await using (var db = new FrogDbContext(FrogDbContextOptions.Create(dsn)))
        {
            await db.Database.MigrateAsync();
            Assert.Empty((await db.Database.GetPendingMigrationsAsync()).ToArray());
        }

        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(dsn)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        Assert.NotEqual(Guid.Empty, seed.MapId);
        Assert.NotEqual(Guid.Empty, seed.ClassId);
    }
}
