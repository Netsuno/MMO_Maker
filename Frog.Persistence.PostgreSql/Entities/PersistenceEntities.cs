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
    public int NpcDefinitionId { get; set; }
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
