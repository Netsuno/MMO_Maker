using Frog.Application.Maps;

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
    public string LogicalPath { get; set; } = string.Empty;
    public int TileSizePixels { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Sha256Hex { get; set; } = string.Empty;
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
