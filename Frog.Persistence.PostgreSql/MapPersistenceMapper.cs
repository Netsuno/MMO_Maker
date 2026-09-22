using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;

namespace Frog.Persistence.PostgreSql;

internal static class MapPersistenceMapper
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static MapEntity ToEntity(Map map, DateTimeOffset nowUtc)
    {
        var entity = new MapEntity
        {
            Id = Guid.NewGuid(),
            Name = map.Name,
            Width = map.Width,
            Height = map.Height,
            AllowPlayerOverlap = map.AllowPlayerOverlap,
            Status = MapPublishStatus.Draft,
            Revision = 1,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            LayersCatalogJson = "[]",
        };

        PopulateChildren(entity, map);
        return entity;
    }

    public static void ReplaceChildren(MapEntity entity, Map map, DateTimeOffset nowUtc)
    {
        ApplyMapFields(entity, map, nowUtc);
        entity.Cells.Clear();
        entity.Warps.Clear();
        entity.NpcSpawns.Clear();
        PopulateChildren(entity, map);
    }

    public static void ApplyMapFields(MapEntity entity, Map map, DateTimeOffset nowUtc)
    {
        entity.Name = map.Name;
        entity.Width = map.Width;
        entity.Height = map.Height;
        entity.AllowPlayerOverlap = map.AllowPlayerOverlap;
        entity.UpdatedAtUtc = nowUtc;
        entity.LayersCatalogJson = SerializeLayersCatalog(map);
    }

    public static MapChildEntities BuildChildren(Guid mapId, Map map)
    {
        var entity = new MapEntity { Id = mapId, LayersCatalogJson = SerializeLayersCatalog(map) };
        PopulateChildren(entity, map);
        return new MapChildEntities(entity.Cells, entity.Warps, entity.NpcSpawns);
    }

    public sealed record MapChildEntities(
        List<MapCellEntity> Cells,
        List<MapWarpEntity> Warps,
        List<MapNpcSpawnEntity> NpcSpawns);

    public static MapPublishedSnapshotEntity ToPublishedSnapshot(MapEntity draft, Map map, DateTimeOffset nowUtc)
    {
        var snapshotId = Guid.NewGuid();
        var snapshot = new MapPublishedSnapshotEntity
        {
            Id = snapshotId,
            MapId = draft.Id,
            Revision = draft.Revision,
            PublishedAtUtc = nowUtc,
            Name = map.Name,
            Width = map.Width,
            Height = map.Height,
            AllowPlayerOverlap = map.AllowPlayerOverlap,
            LayersCatalogJson = SerializeLayersCatalog(map),
            PrefabsJson = draft.PrefabsJson,
        };

        var children = BuildChildren(draft.Id, map);
        foreach (var cell in children.Cells)
        {
            snapshot.Cells.Add(new MapPublishedCellEntity
            {
                SnapshotId = snapshotId,
                X = cell.X,
                Y = cell.Y,
                LayersJson = cell.LayersJson,
            });
        }

        foreach (var warp in children.Warps)
        {
            snapshot.Warps.Add(new MapPublishedWarpEntity
            {
                Id = Guid.NewGuid(),
                SnapshotId = snapshotId,
                SourceX = warp.SourceX,
                SourceY = warp.SourceY,
                TargetMapId = warp.TargetMapId,
                TargetX = warp.TargetX,
                TargetY = warp.TargetY,
                DestinationUnresolved = warp.DestinationUnresolved,
            });
        }

        return snapshot;
    }

    public static StoredMap ToStoredFromSnapshot(MapPublishedSnapshotEntity snapshot, long? publishedRevision)
    {
        var pseudo = new MapEntity
        {
            Id = snapshot.MapId,
            Name = snapshot.Name,
            Width = snapshot.Width,
            Height = snapshot.Height,
            AllowPlayerOverlap = snapshot.AllowPlayerOverlap,
            LayersCatalogJson = snapshot.LayersCatalogJson,
            Cells = snapshot.Cells.Select(c => new MapCellEntity
            {
                MapId = snapshot.MapId,
                X = c.X,
                Y = c.Y,
                LayersJson = c.LayersJson,
            }).ToList(),
        };

        return new StoredMap
        {
            MapId = snapshot.MapId,
            Map = ToDomain(pseudo),
            Revision = snapshot.Revision,
            Status = MapPublishStatus.Published,
            PublishedRevision = publishedRevision ?? snapshot.Revision,
            Prefabs = DeserializePrefabs(snapshot.PrefabsJson),
        };
    }

    public static string? SerializePrefabs(MapPrefabPersistDocument? document)
        => document is null ? null : MapPrefabPersistJson.SerializeToString(document);

    public static MapPrefabPersistDocument? DeserializePrefabs(string? json)
        => MapPrefabPersistJson.TryDeserializeFromString(json);

    public static string SerializeLayersCatalog(Map map) =>
        JsonSerializer.Serialize(
            map.Layers.Select(l => new LayerCatalogEntry
            {
                LayerType = (byte)l.LayerType,
                DisplayName = l.DisplayName,
                Visible = l.Visible,
                Locked = l.Locked,
            }).ToList(),
            Json);

    public static Map ToDomain(MapEntity entity)
    {
        var map = new Map
        {
            Name = entity.Name,
            Width = entity.Width,
            Height = entity.Height,
            AllowPlayerOverlap = entity.AllowPlayerOverlap,
        };

        var catalog = JsonSerializer.Deserialize<List<LayerCatalogEntry>>(entity.LayersCatalogJson, Json)
                      ?? new List<LayerCatalogEntry>();
        // Ordre du catalogue : deux couches Ground (« Sol » et « Tombe ») restent distinctes.
        var layers = new List<Layer>(catalog.Count);
        foreach (var entry in catalog)
        {
            layers.Add(new Layer
            {
                LayerType = (LayerType)entry.LayerType,
                DisplayName = entry.DisplayName,
                Visible = entry.Visible,
                Locked = entry.Locked,
            });
        }

        foreach (var cell in entity.Cells)
        {
            var payloads = JsonSerializer.Deserialize<List<CellLayerPayload>>(cell.LayersJson, Json)
                           ?? new List<CellLayerPayload>();
            var legacySlot = new Dictionary<LayerType, int>();
            foreach (var p in payloads)
            {
                var layer = ResolveLayer(layers, p, legacySlot);

                var tile = new Tile
                {
                    X = cell.X,
                    Y = cell.Y,
                    Type = (TileType)p.TileType,
                    TilesetId = p.TilesetId,
                    SrcX = p.SrcX,
                    SrcY = p.SrcY,
                    WarpTargetMapId = p.WarpTargetMapId,
                    WarpTargetX = p.WarpTargetX,
                    WarpTargetY = p.WarpTargetY,
                    ScriptId = p.ScriptId,
                };

                if (tile.Type == TileType.Block)
                {
                    tile.Attributes.Add(new BlockAttribute());
                }
                else if (tile.Type == TileType.Warp)
                {
                    tile.Attributes.Add(new WarpAttribute
                    {
                        TargetMapId = tile.WarpTargetMapId,
                        TargetX = tile.WarpTargetX,
                        TargetY = tile.WarpTargetY,
                    });
                }

                layer.Tiles.Add(tile);
            }
        }

        foreach (var layer in layers)
        {
            map.Layers.Add(layer);
        }

        if (map.Layers.Count == 0)
        {
            map.Layers.Add(new Layer { LayerType = LayerType.Ground, DisplayName = "Ground" });
        }

        return map;
    }

    private static void PopulateChildren(MapEntity entity, Map map)
    {
        entity.LayersCatalogJson = SerializeLayersCatalog(map);

        var cells = new Dictionary<(int X, int Y), List<CellLayerPayload>>();
        var warpKeys = new HashSet<(int X, int Y)>();
        for (var layerIndex = 0; layerIndex < map.Layers.Count; layerIndex++)
        {
            var layer = map.Layers[layerIndex];
            foreach (var tile in layer.Tiles)
            {
                var key = (tile.X, tile.Y);
                if (!cells.TryGetValue(key, out var list))
                {
                    list = new List<CellLayerPayload>();
                    cells[key] = list;
                }

                list.Add(new CellLayerPayload
                {
                    LayerIndex = layerIndex,
                    LayerType = (byte)layer.LayerType,
                    TileType = (byte)tile.Type,
                    TilesetId = tile.TilesetId,
                    SrcX = tile.SrcX,
                    SrcY = tile.SrcY,
                    WarpTargetMapId = tile.WarpTargetMapId,
                    WarpTargetX = tile.WarpTargetX,
                    WarpTargetY = tile.WarpTargetY,
                    ScriptId = tile.ScriptId,
                });

                if (tile.Type == TileType.Warp && warpKeys.Add(key))
                {
                    var targetId = tile.WarpTargetMapId == Guid.Empty ? (Guid?)null : tile.WarpTargetMapId;
                    entity.Warps.Add(new MapWarpEntity
                    {
                        Id = Guid.NewGuid(),
                        MapId = entity.Id,
                        SourceX = tile.X,
                        SourceY = tile.Y,
                        TargetMapId = targetId,
                        TargetX = tile.WarpTargetX,
                        TargetY = tile.WarpTargetY,
                        DestinationUnresolved = targetId is null,
                    });
                }
            }
        }

        foreach (var ((x, y), payloads) in cells)
        {
            entity.Cells.Add(new MapCellEntity
            {
                MapId = entity.Id,
                X = x,
                Y = y,
                LayersJson = JsonSerializer.Serialize(payloads, Json),
            });
        }
    }

    private sealed class LayerCatalogEntry
    {
        public byte LayerType { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public bool Locked { get; set; }
    }

    /// <summary>
    /// Couche cible. <see cref="CellLayerPayload.LayerIndex"/> (additif) prime.
    /// Les JSON anciens n’ont que <see cref="CellLayerPayload.LayerType"/> : la n-ième
    /// charge utile de ce type sur la cellule va à la n-ième couche du catalogue de ce type.
    /// </summary>
    private static Layer ResolveLayer(
        List<Layer> layers,
        CellLayerPayload payload,
        Dictionary<LayerType, int> legacySlot)
    {
        if (payload.LayerIndex is int index && (uint)index < (uint)layers.Count)
        {
            return layers[index];
        }

        var layerType = (LayerType)payload.LayerType;
        var slot = legacySlot.GetValueOrDefault(layerType);
        legacySlot[layerType] = slot + 1;
        var seen = 0;
        foreach (var layer in layers)
        {
            if (layer.LayerType != layerType)
            {
                continue;
            }

            if (seen == slot)
            {
                return layer;
            }

            seen++;
        }

        var created = new Layer { LayerType = layerType, DisplayName = layerType.ToString() };
        layers.Add(created);
        return created;
    }

    private sealed class CellLayerPayload
    {
        /// <summary>Index dans <c>layers_catalog_json</c>. Absent des snapshots publiés avant ce correctif.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LayerIndex { get; set; }

        public byte LayerType { get; set; }
        public byte TileType { get; set; }
        public int TilesetId { get; set; }
        public int SrcX { get; set; }
        public int SrcY { get; set; }
        public Guid WarpTargetMapId { get; set; }
        public int WarpTargetX { get; set; }
        public int WarpTargetY { get; set; }
        public string? ScriptId { get; set; }
    }
}
