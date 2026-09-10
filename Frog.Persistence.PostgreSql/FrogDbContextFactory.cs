using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Frog.Persistence.PostgreSql;

/// <summary>Usine EF Tools uniquement — chaîne factice, jamais utilisée à l'exécution.</summary>
public sealed class FrogDbContextFactory : IDesignTimeDbContextFactory<FrogDbContext>
{
    public FrogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FrogDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=frog;Username=frog;Password=frog_dev_only")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new FrogDbContext(options);
    }
}
