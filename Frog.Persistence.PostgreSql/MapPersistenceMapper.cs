using System.Text.Json;
using Frog.Application.Maps;
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

    public static MapEntity ToEntity(SaveMapRequest request, DateTimeOffset nowUtc)
    {
        var id = request.MapId is Guid requested && requested != Guid.Empty ? requested : Guid.NewGuid();
        var entity = new MapEntity
        {
            Id = id,
            Name = request.Map.Name,
            Width = request.Map.Width,
            Height = request.Map.Height,
            AllowPlayerOverlap = request.Map.AllowPlayerOverlap,
            Status = MapPublishStatus.Draft,
            Revision = 1,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            LayersCatalogJson = "[]",
        };

        PopulateChildren(entity, request.Map);
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
        };
    }

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
        var layersByType = new Dictionary<LayerType, Layer>();
        foreach (var entry in catalog)
        {
            var layerType = (LayerType)entry.LayerType;
            layersByType[layerType] = new Layer
            {
                LayerType = layerType,
                DisplayName = entry.DisplayName,
                Visible = entry.Visible,
                Locked = entry.Locked,
            };
        }

        foreach (var cell in entity.Cells)
        {
            var payloads = JsonSerializer.Deserialize<List<CellLayerPayload>>(cell.LayersJson, Json)
                           ?? new List<CellLayerPayload>();
            foreach (var p in payloads)
            {
                var layerType = (LayerType)p.LayerType;
                if (!layersByType.TryGetValue(layerType, out var layer))
                {
                    layer = new Layer { LayerType = layerType, DisplayName = layerType.ToString() };
                    layersByType[layerType] = layer;
                }

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

        foreach (var layer in layersByType.Values.OrderBy(l => (byte)l.LayerType))
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
        foreach (var layer in map.Layers)
        {
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

    private sealed class CellLayerPayload
    {
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
