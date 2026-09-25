using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Models;

namespace Frog.Editor.Config;

/// <summary>
/// Mémo des entités posées (apparition, PNJ, objet) dans <c>editor-workstate.json</c>.
/// Même clé que le départ playtest. Pas de SQL.
/// </summary>
public static class EditorMapPlacedEntityWorkstate
{
    public static bool TryRead(Guid? mapId, Map map, out List<MapPlacedEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(map);
        var key = MapPlaytestSpawn.BuildWorkstateKey(mapId, map.Name, map.Width, map.Height);
        if (!EditorLocalWorkstate.TryReadMapPlacedEntities(key, out var stored))
        {
            entities = new List<MapPlacedEntity>();
            return false;
        }

        entities = new List<MapPlacedEntity>();
        var seenIds = new HashSet<Guid>();
        foreach (var entity in stored)
        {
            if (entity is null || !seenIds.Add(entity.Id))
            {
                continue;
            }

            if (!MapPlacedEntityEdit.TryValidate(entity, map, out _))
            {
                continue;
            }

            if (MapPlacedEntityEdit.FindAt(entities, entity.TileX, entity.TileY) is not null)
            {
                continue;
            }

            entities.Add(entity);
        }

        return true;
    }

    public static void Write(Guid? mapId, Map map, IReadOnlyList<MapPlacedEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);
        var key = MapPlaytestSpawn.BuildWorkstateKey(mapId, map.Name, map.Width, map.Height);
        var kept = new List<MapPlacedEntity>();
        var seenIds = new HashSet<Guid>();
        foreach (var entity in MapPlacedEntityEdit.Clone(entities))
        {
            if (!seenIds.Add(entity.Id) || !MapPlacedEntityEdit.TryValidate(entity, map, out _))
            {
                continue;
            }

            if (MapPlacedEntityEdit.FindAt(kept, entity.TileX, entity.TileY) is not null)
            {
                continue;
            }

            kept.Add(entity);
        }

        EditorLocalWorkstate.WriteMapPlacedEntities(key, kept);
    }
}
