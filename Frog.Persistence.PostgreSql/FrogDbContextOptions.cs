using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public static class FrogDbContextOptions
{
    public static DbContextOptions<FrogDbContext> Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return new DbContextOptionsBuilder<FrogDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
    }
}
