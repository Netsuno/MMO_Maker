using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Persistence.PostgreSql.Entities;

public sealed class MapEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool AllowPlayerOverlap { get; set; }
    public MapPublishStatus Status { get; set; }
    public long Revision { get; set; }
    /// <summary>Révision publiée immuable (dernier snapshot).</summary>
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    /// <summary>Métadonnées de couches (ordre, nom, visible/locked) même si aucune tuile.</summary>
    public string LayersCatalogJson { get; set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<MapCellEntity> Cells { get; set; } = new();
    public List<MapWarpEntity> Warps { get; set; } = new();
    public List<MapNpcSpawnEntity> NpcSpawns { get; set; } = new();
    public List<MapEventPlacementEntity> EventPlacements { get; set; } = new();
}

public sealed class MapCellEntity
{
    public Guid MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string LayersJson { get; set; } = "[]";
    public MapEntity Map { get; set; } = null!;
}

public sealed class MapWarpEntity
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public int SourceX { get; set; }
    public int SourceY { get; set; }
    public Guid? TargetMapId { get; set; }
    public MapEntity? TargetMap { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public bool DestinationUnresolved { get; set; }
    public MapEntity Map { get; set; } = null!;
}

public sealed class MapNpcSpawnEntity
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    /// <summary>Alias entier historique (éditeur). 0 si <see cref="NpcId"/> est renseigné.</summary>
    public int NpcDefinitionId { get; set; }
    /// <summary>Identifiant catalogue Guid (préféré). Empty = résoudre via alias.</summary>
    public Guid NpcId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public byte Direction { get; set; }
    public MapEntity Map { get; set; } = null!;
}

public sealed class TilesetEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LogicalPath { get; set; } = string.Empty;
    public int TileSizePixels { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Sha256Hex { get; set; } = string.Empty;
    public int? EditorPaletteId { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’un tileset publié.</summary>
public sealed class TilesetPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid TilesetId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LogicalPath { get; set; } = string.Empty;
    public int TileSizePixels { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Sha256Hex { get; set; } = string.Empty;
    public int? EditorPaletteId { get; set; }
    public TilesetEntity Tileset { get; set; } = null!;
}

public sealed class TilesetPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid TilesetId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public TilesetEntity Tileset { get; set; } = null!;
}

public sealed class NpcEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NpcKind Kind { get; set; }
    public string SpriteLogicalPath { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? Notes { get; set; }
    public int? EditorAliasId { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’un NPC ou monstre publié.</summary>
public sealed class NpcPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid NpcId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public NpcKind Kind { get; set; }
    public string SpriteLogicalPath { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? Notes { get; set; }
    public int? EditorAliasId { get; set; }
    public NpcEntity Npc { get; set; } = null!;
}

public sealed class NpcPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid NpcId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public NpcEntity Npc { get; set; } = null!;
}

public sealed class ItemEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ItemType Kind { get; set; }
    public string IconLogicalPath { get; set; } = string.Empty;
    public int MaxStack { get; set; }
    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }
    public string? Description { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’un objet publié.</summary>
public sealed class ItemPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public ItemType Kind { get; set; }
    public string IconLogicalPath { get; set; } = string.Empty;
    public int MaxStack { get; set; }
    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }
    public string? Description { get; set; }
    public ItemEntity Item { get; set; } = null!;
}

public sealed class ItemPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public ItemEntity Item { get; set; } = null!;
}

public sealed class SpellEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpellKind Kind { get; set; }
    public int ManaCost { get; set; }
    public int CooldownMs { get; set; }
    public TargetType TargetType { get; set; }
    public string IconLogicalPath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’un sort ou d’une compétence publié.</summary>
public sealed class SpellPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid SpellId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpellKind Kind { get; set; }
    public int ManaCost { get; set; }
    public int CooldownMs { get; set; }
    public TargetType TargetType { get; set; }
    public string IconLogicalPath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SpellEntity Spell { get; set; } = null!;
}

public sealed class SpellPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid SpellId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public SpellEntity Spell { get; set; } = null!;
}

public sealed class ClassEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BaseHp { get; set; }
    public int BaseMp { get; set; }
    public int Str { get; set; }
    public int Agi { get; set; }
    public int Vit { get; set; }
    public int Int { get; set; }
    public int Dex { get; set; }
    public int Luck { get; set; }
    public Guid? StartingSpellId { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’une classe publiée.</summary>
public sealed class ClassPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BaseHp { get; set; }
    public int BaseMp { get; set; }
    public int Str { get; set; }
    public int Agi { get; set; }
    public int Vit { get; set; }
    public int Int { get; set; }
    public int Dex { get; set; }
    public int Luck { get; set; }
    public Guid? StartingSpellId { get; set; }
    public ClassEntity Class { get; set; } = null!;
}

public sealed class ClassPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public ClassEntity Class { get; set; } = null!;
}

public sealed class ShopEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ListingsJson { get; set; } = "[]";
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’une boutique publiée.</summary>
public sealed class ShopPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ListingsJson { get; set; } = "[]";
    public ShopEntity Shop { get; set; } = null!;
}

public sealed class ShopPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public ShopEntity Shop { get; set; } = null!;
}

public sealed class ResourceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SpriteLogicalPath { get; set; } = string.Empty;
    public int RespawnSeconds { get; set; }
    public Guid? ToolItemId { get; set; }
    public Guid YieldItemId { get; set; }
    public int YieldQuantity { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’une ressource publiée.</summary>
public sealed class ResourcePublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SpriteLogicalPath { get; set; } = string.Empty;
    public int RespawnSeconds { get; set; }
    public Guid? ToolItemId { get; set; }
    public Guid YieldItemId { get; set; }
    public int YieldQuantity { get; set; }
    public ResourceEntity Resource { get; set; } = null!;
}

public sealed class ResourcePublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public ResourceEntity Resource { get; set; } = null!;
}

public sealed class ResourceSpawnEntity
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public Guid ResourceId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’un placement de ressource publié.</summary>
public sealed class ResourceSpawnPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid SpawnId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public Guid MapId { get; set; }
    public Guid ResourceId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public ResourceSpawnEntity Spawn { get; set; } = null!;
}

public sealed class ResourceSpawnPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid SpawnId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public ResourceSpawnEntity Spawn { get; set; } = null!;
}

public sealed class LegacyImportEntity
{
    public Guid Id { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string Sha256Hex { get; set; } = string.Empty;
    public string FormatType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ReportJson { get; set; } = "{}";
    public DateTimeOffset ImportedAtUtc { get; set; }
}

/// <summary>Snapshot immuable d’une révision publiée (brouillon courant reste dans maps/map_cells).</summary>
public sealed class MapPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool AllowPlayerOverlap { get; set; }
    public string LayersCatalogJson { get; set; } = "[]";
    public MapEntity Map { get; set; } = null!;
    public List<MapPublishedCellEntity> Cells { get; set; } = new();
    public List<MapPublishedWarpEntity> Warps { get; set; } = new();
    public List<MapPublishedNpcSpawnEntity> NpcSpawns { get; set; } = new();
    public List<MapPublishedEventPlacementEntity> EventPlacements { get; set; } = new();
}

/// <summary>Spawn NPC/monstre immuable d’un snapshot publié (NpcId = Guid catalogue).</summary>
public sealed class MapPublishedNpcSpawnEntity
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid NpcId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public byte Direction { get; set; }
    public MapPublishedSnapshotEntity Snapshot { get; set; } = null!;
}

/// <summary>Liaison durable Guid carte ↔ identifiant runtime int (sessions / protocol).</summary>
public sealed class RuntimeMapBindingEntity
{
    public Guid MapId { get; set; }
    public int RuntimeMapId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>Configuration singleton entrée joueur / respawn (cartes publiées uniquement).</summary>
public sealed class WorldSpawnSettingsEntity
{
    public int Id { get; set; } = 1;
    public Guid StartMapId { get; set; }
    public int StartTileX { get; set; }
    public int StartTileY { get; set; }
    public Guid RespawnMapId { get; set; }
    public int RespawnTileX { get; set; }
    public int RespawnTileY { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class MapPublishedCellEntity
{
    public Guid SnapshotId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string LayersJson { get; set; } = "[]";
    public MapPublishedSnapshotEntity Snapshot { get; set; } = null!;
}

public sealed class MapPublishedWarpEntity
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public int SourceX { get; set; }
    public int SourceY { get; set; }
    public Guid? TargetMapId { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public bool DestinationUnresolved { get; set; }
    public MapPublishedSnapshotEntity Snapshot { get; set; } = null!;
}

public sealed class MapPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public MapEntity Map { get; set; } = null!;
}

public sealed class MapEventDefinitionEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CatalogSlug { get; set; }
    public int? EditorAliasId { get; set; }
    public string PagesJson { get; set; } = "[]";
    public ContentPublishStatus Status { get; set; }
    public long Revision { get; set; }
    public long? PublishedRevision { get; set; }
    public Guid? PublishedSnapshotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class MapEventPublishedSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid EventDefinitionId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CatalogSlug { get; set; }
    public int? EditorAliasId { get; set; }
    public string PagesJson { get; set; } = "[]";
    public MapEventDefinitionEntity EventDefinition { get; set; } = null!;
}

public sealed class MapEventPublicationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid EventDefinitionId { get; set; }
    public Guid SnapshotId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public MapEventDefinitionEntity EventDefinition { get; set; } = null!;
}

public sealed class MapEventPlacementEntity
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public Guid EventDefinitionId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string TriggerKind { get; set; } = "action";
    public string MovementKind { get; set; } = "fixed";
    public string RouteWaypointsJson { get; set; } = "[]";
    public MapEntity Map { get; set; } = null!;
}

public sealed class MapPublishedEventPlacementEntity
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid EventDefinitionId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string TriggerKind { get; set; } = "action";
    public string MovementKind { get; set; } = "fixed";
    public string RouteWaypointsJson { get; set; } = "[]";
    public MapPublishedSnapshotEntity Snapshot { get; set; } = null!;
}
