using Frog.Core.Maps;

namespace Frog.Application.Maps;

/// <summary>Zone publiée, liée à l’identifiant runtime déjà utilisé par les monstres.</summary>
public sealed record PublishedMobSpawnZone(Guid MapId, int RuntimeMapId, MobSpawnZone Zone);

/// <summary>Ordre d’apparition. Le serveur le réalise avec <c>SpawnMonster</c>, pas un second type de monstre.</summary>
public sealed record MobSpawnOrder(
    Guid ReservationId,
    Guid MapId,
    int RuntimeMapId,
    Guid ZoneId,
    Guid EntryId,
    Guid MonsterId,
    int TileX,
    int TileY);

/// <summary>
/// Maintient la quantité vivante et le délai. Une tuile hors zone n’est jamais ordonnée.
/// Le délai part à la mort observée : avant l’échéance, aucune réapparition.
/// </summary>
public sealed class MobSpawnZoneKeeper
{
    private sealed class Slot
    {
        public Guid ZoneId { get; init; }

        public Guid EntryId { get; init; }

        public Guid ReservationId { get; init; }

        public Guid? InstanceId { get; set; }

        public int TileX { get; init; }

        public int TileY { get; init; }
    }

    private sealed class Pending
    {
        public Guid ZoneId { get; init; }

        public Guid EntryId { get; init; }

        public DateTime DueUtc { get; init; }
    }

    private readonly List<Slot> _slots = new();
    private readonly List<Pending> _pending = new();
    private readonly Func<MobSpawnZone, (int X, int Y)> _pick;

    public MobSpawnZoneKeeper(Func<MobSpawnZone, (int X, int Y)> pickTile)
    {
        _pick = pickTile ?? throw new ArgumentNullException(nameof(pickTile));
    }

    public IReadOnlyList<MobSpawnOrder> Tick(
        IReadOnlyList<PublishedMobSpawnZone> zones,
        IReadOnlySet<Guid> aliveInstanceIds,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(aliveInstanceIds);

        for (var i = _slots.Count - 1; i >= 0; i--)
        {
            var slot = _slots[i];
            if (slot.InstanceId is not Guid instanceId)
            {
                continue;
            }

            if (aliveInstanceIds.Contains(instanceId))
            {
                continue;
            }

            _slots.RemoveAt(i);
            var entry = FindEntry(zones, slot.ZoneId, slot.EntryId);
            if (entry is null)
            {
                continue;
            }

            _pending.Add(new Pending
            {
                ZoneId = slot.ZoneId,
                EntryId = slot.EntryId,
                DueUtc = utcNow.AddSeconds(entry.RespawnSeconds),
            });
        }

        DropMissing(zones);

        var orders = new List<MobSpawnOrder>();
        foreach (var published in zones)
        {
            var zone = published.Zone;
            if (zone is null)
            {
                continue;
            }

            foreach (var entry in zone.Entries)
            {
                if (entry.Quantity < MobSpawnZoneDocument.MinQuantity || entry.MonsterId == Guid.Empty)
                {
                    continue;
                }

                var due = new List<Pending>();
                foreach (var pending in _pending)
                {
                    if (pending.ZoneId == zone.Id && pending.EntryId == entry.Id && pending.DueUtc <= utcNow)
                    {
                        due.Add(pending);
                    }
                }

                foreach (var pending in due)
                {
                    if (CountOccupied(zone.Id, entry.Id) >= entry.Quantity)
                    {
                        _pending.Remove(pending);
                        continue;
                    }

                    if (!TryOrder(published, entry, orders))
                    {
                        break;
                    }

                    _pending.Remove(pending);
                }

                while (CountOccupied(zone.Id, entry.Id) + CountPending(zone.Id, entry.Id) < entry.Quantity)
                {
                    if (!TryOrder(published, entry, orders))
                    {
                        break;
                    }
                }
            }
        }

        return orders;
    }

    public void Commit(Guid reservationId, Guid instanceId)
    {
        if (reservationId == Guid.Empty || instanceId == Guid.Empty)
        {
            return;
        }

        foreach (var slot in _slots)
        {
            if (slot.ReservationId != reservationId)
            {
                continue;
            }

            slot.InstanceId = instanceId;
            return;
        }
    }

    public void Abort(Guid reservationId)
    {
        for (var i = _slots.Count - 1; i >= 0; i--)
        {
            if (_slots[i].ReservationId == reservationId && _slots[i].InstanceId is null)
            {
                _slots.RemoveAt(i);
            }
        }
    }

    private bool TryOrder(PublishedMobSpawnZone published, MobSpawnEntry entry, List<MobSpawnOrder> orders)
    {
        var zone = published.Zone;
        var tile = _pick(zone);
        if (!MobSpawnZoneEdit.Contains(zone, tile.X, tile.Y))
        {
            return false;
        }

        var reservation = Guid.NewGuid();
        _slots.Add(new Slot
        {
            ZoneId = zone.Id,
            EntryId = entry.Id,
            ReservationId = reservation,
            TileX = tile.X,
            TileY = tile.Y,
        });
        orders.Add(new MobSpawnOrder(
            reservation,
            published.MapId,
            published.RuntimeMapId,
            zone.Id,
            entry.Id,
            entry.MonsterId,
            tile.X,
            tile.Y));
        return true;
    }

    private int CountOccupied(Guid zoneId, Guid entryId)
    {
        var count = 0;
        foreach (var slot in _slots)
        {
            if (slot.ZoneId == zoneId && slot.EntryId == entryId)
            {
                count++;
            }
        }

        return count;
    }

    private int CountPending(Guid zoneId, Guid entryId)
    {
        var count = 0;
        foreach (var pending in _pending)
        {
            if (pending.ZoneId == zoneId && pending.EntryId == entryId)
            {
                count++;
            }
        }

        return count;
    }

    private void DropMissing(IReadOnlyList<PublishedMobSpawnZone> zones)
    {
        for (var i = _slots.Count - 1; i >= 0; i--)
        {
            if (FindEntry(zones, _slots[i].ZoneId, _slots[i].EntryId) is null && _slots[i].InstanceId is null)
            {
                _slots.RemoveAt(i);
            }
        }

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (FindEntry(zones, _pending[i].ZoneId, _pending[i].EntryId) is null)
            {
                _pending.RemoveAt(i);
            }
        }
    }

    private static MobSpawnEntry? FindEntry(IReadOnlyList<PublishedMobSpawnZone> zones, Guid zoneId, Guid entryId)
    {
        foreach (var published in zones)
        {
            if (published.Zone is null || published.Zone.Id != zoneId)
            {
                continue;
            }

            foreach (var entry in published.Zone.Entries)
            {
                if (entry.Id == entryId)
                {
                    return entry;
                }
            }
        }

        return null;
    }
}

/// <summary>Applique les ordres. Un échec libère la place pour le prochain passage.</summary>
public static class MobSpawnZoneMaintenance
{
    public static async Task<int> ApplyAsync(
        MobSpawnZoneKeeper keeper,
        IReadOnlyList<PublishedMobSpawnZone> zones,
        IReadOnlySet<Guid> aliveInstanceIds,
        DateTime utcNow,
        Func<MobSpawnOrder, CancellationToken, Task<Guid?>> spawn,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keeper);
        ArgumentNullException.ThrowIfNull(spawn);
        var orders = keeper.Tick(zones, aliveInstanceIds, utcNow);
        var spawned = 0;
        foreach (var order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var zone = ZoneOf(zones, order.ZoneId);
            if (zone is null || !MobSpawnZoneEdit.Contains(zone, order.TileX, order.TileY))
            {
                keeper.Abort(order.ReservationId);
                continue;
            }

            var instanceId = await spawn(order, cancellationToken).ConfigureAwait(false);
            if (instanceId is Guid id && id != Guid.Empty)
            {
                keeper.Commit(order.ReservationId, id);
                spawned++;
            }
            else
            {
                keeper.Abort(order.ReservationId);
            }
        }

        return spawned;
    }

    private static MobSpawnZone? ZoneOf(IReadOnlyList<PublishedMobSpawnZone> zones, Guid zoneId)
    {
        foreach (var published in zones)
        {
            if (published.Zone is not null && published.Zone.Id == zoneId)
            {
                return published.Zone;
            }
        }

        return null;
    }
}
