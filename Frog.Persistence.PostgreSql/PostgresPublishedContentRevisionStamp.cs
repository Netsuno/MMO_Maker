using Frog.Application.Content;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

/// <summary>
/// Publication-history row counts. Any editor publish (dialogue, map event, NPC, …)
/// increments the stamp so the running server can refresh connected sessions.
/// </summary>
public sealed class PostgresPublishedContentRevisionStamp(FrogDbContextGate gate) : IPublishedContentRevisionStamp
{
    private readonly FrogDbContextGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public Task<long> GetStampAsync(CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            var phase8 = await db.Phase8ContentPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            var mapEvents = await db.MapEventPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            var maps = await db.MapPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            var items = await db.ItemPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            var npcs = await db.NpcPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            var classes = await db.ClassPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            var spells = await db.SpellPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            var shops = await db.ShopPublicationHistory.AsNoTracking().LongCountAsync(ct)
                .ConfigureAwait(false);
            return phase8 + mapEvents + maps + items + npcs + classes + spells + shops;
        }, cancellationToken);
}
