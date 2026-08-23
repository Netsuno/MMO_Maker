using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class FrogDbContext : DbContext
{
    public FrogDbContext(DbContextOptions<FrogDbContext> options)
        : base(options)
    {
    }

    public DbSet<MapEntity> Maps => Set<MapEntity>();
    public DbSet<MapCellEntity> MapCells => Set<MapCellEntity>();
    public DbSet<MapWarpEntity> MapWarps => Set<MapWarpEntity>();
    public DbSet<MapNpcSpawnEntity> MapNpcSpawns => Set<MapNpcSpawnEntity>();
    public DbSet<MapPublishedSnapshotEntity> MapPublishedSnapshots => Set<MapPublishedSnapshotEntity>();
    public DbSet<MapPublishedCellEntity> MapPublishedCells => Set<MapPublishedCellEntity>();
    public DbSet<MapPublishedWarpEntity> MapPublishedWarps => Set<MapPublishedWarpEntity>();
    public DbSet<MapPublicationHistoryEntity> MapPublicationHistory => Set<MapPublicationHistoryEntity>();
    public DbSet<TilesetEntity> Tilesets => Set<TilesetEntity>();
    public DbSet<TilesetPublishedSnapshotEntity> TilesetPublishedSnapshots => Set<TilesetPublishedSnapshotEntity>();
    public DbSet<TilesetPublicationHistoryEntity> TilesetPublicationHistory => Set<TilesetPublicationHistoryEntity>();
    public DbSet<LegacyImportEntity> LegacyImports => Set<LegacyImportEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("world");

        modelBuilder.Entity<MapEntity>(e =>
        {
            e.ToTable("maps");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Width).IsRequired();
            e.Property(x => x.Height).IsRequired();
            e.Property(x => x.Revision).IsRequired().IsConcurrencyToken();
            e.Property(x => x.PublishedRevision);
            e.Property(x => x.PublishedSnapshotId);
            e.Property(x => x.LayersCatalogJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status).HasConversion<byte>();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_maps_positive_size", "width > 0 AND height > 0");
                t.HasCheckConstraint("ck_maps_non_negative_revision", "revision >= 0");
            });
            e.HasMany(x => x.Cells).WithOne(x => x.Map).HasForeignKey(x => x.MapId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Warps).WithOne(x => x.Map).HasForeignKey(x => x.MapId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.NpcSpawns).WithOne(x => x.Map).HasForeignKey(x => x.MapId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MapCellEntity>(e =>
        {
            e.ToTable("map_cells");
            e.HasKey(x => new { x.MapId, x.X, x.Y });
            e.Property(x => x.LayersJson).HasColumnType("jsonb").IsRequired();
            e.ToTable(t => t.HasCheckConstraint("ck_map_cells_in_bounds", "x >= 0 AND y >= 0"));
        });

        modelBuilder.Entity<MapWarpEntity>(e =>
        {
            e.ToTable("map_warps");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MapId, x.SourceX, x.SourceY }).IsUnique();
            e.HasOne(x => x.TargetMap)
                .WithMany()
                .HasForeignKey(x => x.TargetMapId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MapNpcSpawnEntity>(e =>
        {
            e.ToTable("map_npc_spawns");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<MapPublishedSnapshotEntity>(e =>
        {
            e.ToTable("map_published_snapshots");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MapId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.LayersCatalogJson).HasColumnType("jsonb").IsRequired();
            e.HasMany(x => x.Cells).WithOne(x => x.Snapshot).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Warps).WithOne(x => x.Snapshot).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MapPublishedCellEntity>(e =>
        {
            e.ToTable("map_published_cells");
            e.HasKey(x => new { x.SnapshotId, x.X, x.Y });
            e.Property(x => x.LayersJson).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<MapPublishedWarpEntity>(e =>
        {
            e.ToTable("map_published_warps");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Snapshot).WithMany(x => x.Warps).HasForeignKey(x => x.SnapshotId);
        });

        modelBuilder.Entity<MapPublicationHistoryEntity>(e =>
        {
            e.ToTable("map_publication_history");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.MapId);
        });

        modelBuilder.Entity<TilesetEntity>(e =>
        {
            e.ToTable("tilesets", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LogicalPath).IsUnique();
            e.HasIndex(x => x.EditorPaletteId).IsUnique().HasFilter("editor_palette_id IS NOT NULL");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.LogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Sha256Hex).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_tilesets_positive_size", "width > 0 AND height > 0 AND tile_size_pixels > 0");
                t.HasCheckConstraint("ck_tilesets_non_negative_revision", "revision >= 0");
            });
        });

        modelBuilder.Entity<TilesetPublishedSnapshotEntity>(e =>
        {
            e.ToTable("tileset_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TilesetId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.LogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Sha256Hex).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Tileset).WithMany().HasForeignKey(x => x.TilesetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TilesetPublicationHistoryEntity>(e =>
        {
            e.ToTable("tileset_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TilesetId);
            e.HasOne(x => x.Tileset).WithMany().HasForeignKey(x => x.TilesetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LegacyImportEntity>(e =>
        {
            e.ToTable("legacy_imports", "ops");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Sha256Hex, x.FormatType }).IsUnique();
            e.Property(x => x.Sha256Hex).HasMaxLength(64).IsRequired();
            e.Property(x => x.FormatType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Result).HasMaxLength(32).IsRequired();
            e.Property(x => x.ReportJson).HasColumnType("jsonb").IsRequired();
        });
    }
}
