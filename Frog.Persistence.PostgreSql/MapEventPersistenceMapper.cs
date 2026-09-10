using System.Text.Json;
using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Persistence.PostgreSql.Entities;

namespace Frog.Persistence.PostgreSql;

internal static class MapEventPersistenceMapper
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static MapEventDefinitionEntity ToEntity(MapEventDefinition definition, Guid id, DateTimeOffset nowUtc)
    {
        if (!MapEventPagesCodec.TryDeserializePages(
                MapEventPagesCodec.SerializePages(definition.Pages),
                out _,
                out _))
        {
            throw new InvalidOperationException("Pages invalides.");
        }

        return new MapEventDefinitionEntity
        {
            Id = id,
            Name = definition.Name.Trim(),
            CatalogSlug = string.IsNullOrWhiteSpace(definition.CatalogSlug) ? null : definition.CatalogSlug.Trim(),
            EditorAliasId = definition.EditorAliasId,
            PagesJson = MapEventPagesCodec.SerializePages(definition.Pages),
            Status = ContentPublishStatus.Draft,
            Revision = 1,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public static void ApplyDefinition(MapEventDefinitionEntity entity, MapEventDefinition definition, DateTimeOffset nowUtc)
    {
        entity.Name = definition.Name.Trim();
        entity.CatalogSlug = string.IsNullOrWhiteSpace(definition.CatalogSlug) ? null : definition.CatalogSlug.Trim();
        entity.EditorAliasId = definition.EditorAliasId;
        entity.PagesJson = MapEventPagesCodec.SerializePages(definition.Pages);
        entity.UpdatedAtUtc = nowUtc;
    }

    public static MapEventDefinition ToDomain(MapEventDefinitionEntity entity)
    {
        if (!MapEventPagesCodec.TryDeserializePages(entity.PagesJson, out var pages, out var error))
        {
            throw new InvalidOperationException(error ?? "Pages invalides.");
        }

        return new MapEventDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            CatalogSlug = entity.CatalogSlug,
            EditorAliasId = entity.EditorAliasId,
            Pages = pages,
        };
    }

    public static StoredMapEvent ToStored(MapEventDefinitionEntity entity)
    {
        return new StoredMapEvent
        {
            EventId = entity.Id,
            Definition = ToDomain(entity),
            Revision = entity.Revision,
            Status = entity.Status,
            PublishedRevision = entity.PublishedRevision,
        };
    }

    public static MapEventWireEntry ToWireEntry(
        MapPublishedEventPlacementEntity placement,
        MapEventPublishedSnapshotEntity catalogSnapshot,
        long placementWireId)
    {
        var blocksCollision = true;
        if (MapEventPagesCodec.TryDeserializePages(catalogSnapshot.PagesJson, out var pages, out _))
        {
            var page = pages.OrderByDescending(p => p.Priority).FirstOrDefault();
            if (page is not null)
            {
                blocksCollision = page.BlocksCollision;
            }
        }

        return new MapEventWireEntry
        {
            PlacementId = placementWireId,
            CatalogId = PostgresMapEventRepository.StableCatalogWireId(
                catalogSnapshot.EventDefinitionId,
                catalogSnapshot.EditorAliasId),
            Slug = catalogSnapshot.CatalogSlug ?? catalogSnapshot.EventDefinitionId.ToString("N")[..16],
            DisplayName = catalogSnapshot.Name,
            TileX = placement.TileX,
            TileY = placement.TileY,
            TriggerKind = Phase8MapEventTriggerKinds.ToWireTriggerKind(placement.TriggerKind),
            MovementKind = placement.MovementKind,
            RouteWaypoints = DeserializeRouteWaypoints(placement.RouteWaypointsJson),
            BlocksCollision = blocksCollision,
        };
    }

    public static string SerializeRouteWaypoints(IReadOnlyList<MapEventRouteWaypoint> waypoints) =>
        JsonSerializer.Serialize(waypoints, Json);

    public static IReadOnlyList<MapEventRouteWaypoint> DeserializeRouteWaypoints(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return Array.Empty<MapEventRouteWaypoint>();
        }

        return JsonSerializer.Deserialize<List<MapEventRouteWaypoint>>(json, Json) ?? new List<MapEventRouteWaypoint>();
    }
}
