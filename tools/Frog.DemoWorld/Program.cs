using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Demo;
using Microsoft.EntityFrameworkCore;

namespace Frog.DemoWorld;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help"))
        {
            Console.WriteLine("Usage: Frog.DemoWorld publish [--connection-string DSN]");
            Console.WriteLine("DSN: FROG_POSTGRES_CONNECTION_STRING or --connection-string.");
            Console.WriteLine("Migrate puis publie le monde démo P10-4 (chemins Save/Publish éditeur).");
            return 0;
        }

        if (args.Length == 0 || !string.Equals(args[0], "publish", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("expected: publish");
            return 2;
        }

        var connection = ReadConnectionString(args);
        if (string.IsNullOrWhiteSpace(connection))
        {
            Console.Error.WriteLine("FROG_POSTGRES_CONNECTION_STRING or --connection-string is required.");
            return 2;
        }

        try
        {
            var db = new FrogDbContext(FrogDbContextOptions.Create(connection));
            await db.Database.MigrateAsync().ConfigureAwait(false);
            using var gate = new FrogDbContextGate(db);
            var result = await Phase10DemoWorldPublisher.PublishAsync(gate).ConfigureAwait(false);
            Console.WriteLine("demo-world published");
            Console.WriteLine("village=" + result.VillageMapId + " runtime=" + result.VillageRuntimeMapId);
            Console.WriteLine("outskirts=" + result.OutskirtsMapId + " runtime=" + result.OutskirtsRuntimeMapId);
            Console.WriteLine("arena=" + result.ArenaMapId + " runtime=" + result.ArenaRuntimeMapId);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? ReadConnectionString(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--connection-string" or "--connection")
            {
                return args[i + 1];
            }
        }

        return Environment.GetEnvironmentVariable("FROG_POSTGRES_CONNECTION_STRING")
               ?? Environment.GetEnvironmentVariable("FROG_POSTGRES_TEST_CONNECTION_STRING");
    }
}
