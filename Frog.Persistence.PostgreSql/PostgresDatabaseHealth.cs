using Frog.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresDatabaseHealth : IDatabaseHealth
{
    private readonly FrogDbContext _db;

    public PostgresDatabaseHealth(FrogDbContext db)
    {
        _db = db;
    }

    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!canConnect)
            {
                return new DatabaseHealthResult(false, "Connexion PostgreSQL refusée.");
            }

            var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            if (pending.Length > 0)
            {
                return new DatabaseHealthResult(false, "Migrations en attente: " + string.Join(", ", pending));
            }

            var applied = (await _db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            return new DatabaseHealthResult(true, "OK; migrations=" + applied.Length);
        }
        catch (Exception ex)
        {
            return new DatabaseHealthResult(false, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
