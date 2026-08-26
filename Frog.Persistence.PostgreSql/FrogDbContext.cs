using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Frog.Persistence.PostgreSql.Entities.Auth;
using Frog.Persistence.PostgreSql.Entities.Player;
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
    public DbSet<NpcEntity> Npcs => Set<NpcEntity>();
    public DbSet<NpcPublishedSnapshotEntity> NpcPublishedSnapshots => Set<NpcPublishedSnapshotEntity>();
    public DbSet<NpcPublicationHistoryEntity> NpcPublicationHistory => Set<NpcPublicationHistoryEntity>();
    public DbSet<ItemEntity> Items => Set<ItemEntity>();
    public DbSet<ItemPublishedSnapshotEntity> ItemPublishedSnapshots => Set<ItemPublishedSnapshotEntity>();
    public DbSet<ItemPublicationHistoryEntity> ItemPublicationHistory => Set<ItemPublicationHistoryEntity>();
    public DbSet<SpellEntity> Spells => Set<SpellEntity>();
    public DbSet<SpellPublishedSnapshotEntity> SpellPublishedSnapshots => Set<SpellPublishedSnapshotEntity>();
    public DbSet<SpellPublicationHistoryEntity> SpellPublicationHistory => Set<SpellPublicationHistoryEntity>();
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<ClassPublishedSnapshotEntity> ClassPublishedSnapshots => Set<ClassPublishedSnapshotEntity>();
    public DbSet<ClassPublicationHistoryEntity> ClassPublicationHistory => Set<ClassPublicationHistoryEntity>();
    public DbSet<ShopEntity> Shops => Set<ShopEntity>();
    public DbSet<ShopPublishedSnapshotEntity> ShopPublishedSnapshots => Set<ShopPublishedSnapshotEntity>();
    public DbSet<ShopPublicationHistoryEntity> ShopPublicationHistory => Set<ShopPublicationHistoryEntity>();
    public DbSet<ResourceEntity> Resources => Set<ResourceEntity>();
    public DbSet<ResourcePublishedSnapshotEntity> ResourcePublishedSnapshots =>
        Set<ResourcePublishedSnapshotEntity>();
    public DbSet<ResourcePublicationHistoryEntity> ResourcePublicationHistory =>
        Set<ResourcePublicationHistoryEntity>();
    public DbSet<ResourceSpawnEntity> ResourceSpawns => Set<ResourceSpawnEntity>();
    public DbSet<ResourceSpawnPublishedSnapshotEntity> ResourceSpawnPublishedSnapshots =>
        Set<ResourceSpawnPublishedSnapshotEntity>();
    public DbSet<ResourceSpawnPublicationHistoryEntity> ResourceSpawnPublicationHistory =>
        Set<ResourceSpawnPublicationHistoryEntity>();
    public DbSet<LegacyImportEntity> LegacyImports => Set<LegacyImportEntity>();

    public DbSet<AccountEntity> AuthAccounts => Set<AccountEntity>();

    public DbSet<AuthSessionEntity> AuthSessions => Set<AuthSessionEntity>();

    public DbSet<CharacterEntity> PlayerCharacters => Set<CharacterEntity>();

    public DbSet<InventorySlotEntity> PlayerInventorySlots => Set<InventorySlotEntity>();

    public DbSet<BankSlotEntity> PlayerBankSlots => Set<BankSlotEntity>();

    public DbSet<GroundItemEntity> PlayerGroundItems => Set<GroundItemEntity>();

    public DbSet<ShopStockEntity> PlayerShopStock => Set<ShopStockEntity>();

    public DbSet<EconomyRequestIdEntity> PlayerEconomyRequestIds => Set<EconomyRequestIdEntity>();

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

        modelBuilder.Entity<NpcEntity>(e =>
        {
            e.ToTable("npcs", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EditorAliasId).IsUnique().HasFilter("editor_alias_id IS NOT NULL");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.SpriteLogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_npcs_level", "level >= 1 AND level <= 99");
                t.HasCheckConstraint("ck_npcs_non_negative_revision", "revision >= 0");
            });
        });

        modelBuilder.Entity<NpcPublishedSnapshotEntity>(e =>
        {
            e.ToTable("npc_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.NpcId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.SpriteLogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasOne(x => x.Npc).WithMany().HasForeignKey(x => x.NpcId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint(
                "ck_npc_published_snapshots_level",
                "level >= 1 AND level <= 99"));
        });

        modelBuilder.Entity<NpcPublicationHistoryEntity>(e =>
        {
            e.ToTable("npc_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NpcId);
            e.HasOne(x => x.Npc).WithMany().HasForeignKey(x => x.NpcId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemEntity>(e =>
        {
            e.ToTable("items", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.IconLogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_items_kind", "kind > 0");
                t.HasCheckConstraint("ck_items_max_stack", "max_stack >= 1 AND max_stack <= 999");
                t.HasCheckConstraint("ck_items_non_negative_prices", "buy_price >= 0 AND sell_price >= 0");
                t.HasCheckConstraint("ck_items_non_negative_revision", "revision >= 0");
            });
        });

        modelBuilder.Entity<ItemPublishedSnapshotEntity>(e =>
        {
            e.ToTable("item_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ItemId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.IconLogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_item_published_snapshots_kind", "kind > 0");
                t.HasCheckConstraint(
                    "ck_item_published_snapshots_max_stack",
                    "max_stack >= 1 AND max_stack <= 999");
                t.HasCheckConstraint(
                    "ck_item_published_snapshots_non_negative_prices",
                    "buy_price >= 0 AND sell_price >= 0");
            });
        });

        modelBuilder.Entity<ItemPublicationHistoryEntity>(e =>
        {
            e.ToTable("item_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ItemId);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SpellEntity>(e =>
        {
            e.ToTable("spells", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.TargetType).HasConversion<byte>();
            e.Property(x => x.IconLogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_spells_kind", "kind >= 1 AND kind <= 2");
                t.HasCheckConstraint("ck_spells_target_type", "target_type >= 1 AND target_type <= 4");
                t.HasCheckConstraint(
                    "ck_spells_non_negative_cost_cooldown",
                    "mana_cost >= 0 AND cooldown_ms >= 0");
                t.HasCheckConstraint("ck_spells_non_negative_revision", "revision >= 0");
            });
        });

        modelBuilder.Entity<SpellPublishedSnapshotEntity>(e =>
        {
            e.ToTable("spell_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SpellId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.TargetType).HasConversion<byte>();
            e.Property(x => x.IconLogicalPath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.HasOne(x => x.Spell).WithMany().HasForeignKey(x => x.SpellId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_spell_published_snapshots_kind",
                    "kind >= 1 AND kind <= 2");
                t.HasCheckConstraint(
                    "ck_spell_published_snapshots_target_type",
                    "target_type >= 1 AND target_type <= 4");
                t.HasCheckConstraint(
                    "ck_spell_published_snapshots_non_negative_cost_cooldown",
                    "mana_cost >= 0 AND cooldown_ms >= 0");
            });
        });

        modelBuilder.Entity<SpellPublicationHistoryEntity>(e =>
        {
            e.ToTable("spell_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SpellId);
            e.HasOne(x => x.Spell).WithMany().HasForeignKey(x => x.SpellId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClassEntity>(e =>
        {
            e.ToTable("classes", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(ClassDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ClassDefinition.MaxDescriptionLength);
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<SpellEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingSpellId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_classes_positive_resources", "base_hp > 0 AND base_mp > 0");
                t.HasCheckConstraint(
                    "ck_classes_stats",
                    "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 "
                    + "AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 "
                    + "AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                t.HasCheckConstraint("ck_classes_non_negative_revision", "revision >= 0");
            });
        });

        modelBuilder.Entity<ClassPublishedSnapshotEntity>(e =>
        {
            e.ToTable("class_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ClassId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(ClassDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ClassDefinition.MaxDescriptionLength);
            e.HasOne(x => x.Class)
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<SpellEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingSpellId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_class_published_snapshots_positive_resources",
                    "base_hp > 0 AND base_mp > 0");
                t.HasCheckConstraint(
                    "ck_class_published_snapshots_stats",
                    "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 "
                    + "AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 "
                    + "AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
            });
        });

        modelBuilder.Entity<ClassPublicationHistoryEntity>(e =>
        {
            e.ToTable("class_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ClassId);
            e.HasOne(x => x.Class)
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShopEntity>(e =>
        {
            e.ToTable("shops", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(ShopDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ShopDefinition.MaxDescriptionLength);
            e.Property(x => x.ListingsJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.ToTable(t =>
                t.HasCheckConstraint("ck_shops_non_negative_revision", "revision >= 0"));
        });

        modelBuilder.Entity<ShopPublishedSnapshotEntity>(e =>
        {
            e.ToTable("shop_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ShopId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(ShopDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ShopDefinition.MaxDescriptionLength);
            e.Property(x => x.ListingsJson).HasColumnType("jsonb").IsRequired();
            e.HasOne(x => x.Shop)
                .WithMany()
                .HasForeignKey(x => x.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShopPublicationHistoryEntity>(e =>
        {
            e.ToTable("shop_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ShopId);
            e.HasOne(x => x.Shop)
                .WithMany()
                .HasForeignKey(x => x.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResourceEntity>(e =>
        {
            e.ToTable("resources", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(ResourceDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ResourceDefinition.MaxDescriptionLength);
            e.Property(x => x.SpriteLogicalPath)
                .HasMaxLength(ResourceDefinition.MaxLogicalPathLength)
                .IsRequired();
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.ToolItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.YieldItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_resources_non_negative_respawn_revision",
                    "respawn_seconds >= 0 AND revision >= 0");
                t.HasCheckConstraint(
                    "ck_resources_yield_quantity",
                    "yield_quantity >= 1 AND yield_quantity <= 999");
            });
        });

        modelBuilder.Entity<ResourcePublishedSnapshotEntity>(e =>
        {
            e.ToTable("resource_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ResourceId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(ResourceDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ResourceDefinition.MaxDescriptionLength);
            e.Property(x => x.SpriteLogicalPath)
                .HasMaxLength(ResourceDefinition.MaxLogicalPathLength)
                .IsRequired();
            e.HasOne(x => x.Resource)
                .WithMany()
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.ToolItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.YieldItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_resource_published_snapshots_non_negative_respawn",
                    "respawn_seconds >= 0");
                t.HasCheckConstraint(
                    "ck_resource_published_snapshots_yield_quantity",
                    "yield_quantity >= 1 AND yield_quantity <= 999");
            });
        });

        modelBuilder.Entity<ResourcePublicationHistoryEntity>(e =>
        {
            e.ToTable("resource_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ResourceId);
            e.HasOne(x => x.Resource)
                .WithMany()
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResourceSpawnEntity>(e =>
        {
            e.ToTable("resource_spawns", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MapId, x.TileX, x.TileY });
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<MapEntity>()
                .WithMany()
                .HasForeignKey(x => x.MapId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ResourceEntity>()
                .WithMany()
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint(
                "ck_resource_spawns_coordinates_revision",
                "tile_x >= 0 AND tile_y >= 0 AND revision >= 0"));
        });

        modelBuilder.Entity<ResourceSpawnPublishedSnapshotEntity>(e =>
        {
            e.ToTable("resource_spawn_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SpawnId, x.Revision }).IsUnique();
            e.HasOne(x => x.Spawn)
                .WithMany()
                .HasForeignKey(x => x.SpawnId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<MapEntity>()
                .WithMany()
                .HasForeignKey(x => x.MapId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ResourceEntity>()
                .WithMany()
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint(
                "ck_resource_spawn_published_snapshots_coordinates",
                "tile_x >= 0 AND tile_y >= 0"));
        });

        modelBuilder.Entity<ResourceSpawnPublicationHistoryEntity>(e =>
        {
            e.ToTable("resource_spawn_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SpawnId);
            e.HasOne(x => x.Spawn)
                .WithMany()
                .HasForeignKey(x => x.SpawnId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<AccountEntity>(e =>
        {
            e.ToTable("accounts", "auth");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.CreatedAtUtc).IsRequired();
            e.HasMany(x => x.Sessions)
                .WithOne(x => x.Account)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuthSessionEntity>(e =>
        {
            e.ToTable("auth_sessions", "auth");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.ExpiresAtUtc);
            e.Property(x => x.TokenHash).HasMaxLength(32).IsRequired();
            e.Property(x => x.CreatedAtUtc).IsRequired();
            e.Property(x => x.ExpiresAtUtc).IsRequired();
            e.Property(x => x.LastSeenAtUtc).IsRequired();
        });

        modelBuilder.Entity<CharacterEntity>(e =>
        {
            e.ToTable("characters", "player");
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.AccountId);
            e.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.InventorySlots)
                .WithOne(x => x.Character)
                .HasForeignKey(x => x.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.BankSlots)
                .WithOne(x => x.Character)
                .HasForeignKey(x => x.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_characters_level", "level >= 1 AND level <= 99");
                t.HasCheckConstraint("ck_characters_experience", "experience >= 0");
                t.HasCheckConstraint(
                    "ck_characters_resources",
                    "hp >= 0 AND max_hp >= 0 AND mp >= 0 AND max_mp >= 0 AND gold >= 0 AND bank_gold >= 0");
                t.HasCheckConstraint(
                    "ck_characters_stats",
                    "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 "
                    + "AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 "
                    + "AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
            });
        });

        modelBuilder.Entity<InventorySlotEntity>(e =>
        {
            e.ToTable("inventory_slots", "player");
            e.HasKey(x => new { x.CharacterId, x.SlotIndex });
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_inventory_slots_index",
                    "slot_index >= 0 AND slot_index < 30");
                t.HasCheckConstraint(
                    "ck_inventory_slots_quantity",
                    "quantity >= 0 AND quantity <= 999");
                t.HasCheckConstraint(
                    "ck_inventory_slots_item_consistency",
                    "(item_id IS NULL AND quantity = 0) OR (item_id IS NOT NULL AND quantity > 0)");
            });
        });

        modelBuilder.Entity<BankSlotEntity>(e =>
        {
            e.ToTable("bank_slots", "player");
            e.HasKey(x => new { x.CharacterId, x.SlotIndex });
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_bank_slots_index",
                    "slot_index >= 0 AND slot_index < 40");
                t.HasCheckConstraint(
                    "ck_bank_slots_quantity",
                    "quantity >= 0 AND quantity <= 999");
                t.HasCheckConstraint(
                    "ck_bank_slots_item_consistency",
                    "(item_id IS NULL AND quantity = 0) OR (item_id IS NOT NULL AND quantity > 0)");
            });
        });

        modelBuilder.Entity<GroundItemEntity>(e =>
        {
            e.ToTable("ground_items", "player");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MapId, x.TakenAtUtc });
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_ground_items_quantity",
                    "quantity >= 1 AND quantity <= 999");
            });
        });

        modelBuilder.Entity<ShopStockEntity>(e =>
        {
            e.ToTable("shop_stock", "player");
            e.HasKey(x => new { x.ShopId, x.ItemId });
            e.ToTable(t =>
                t.HasCheckConstraint("ck_shop_stock_remaining", "remaining >= 0"));
        });

        modelBuilder.Entity<EconomyRequestIdEntity>(e =>
        {
            e.ToTable("economy_request_ids", "player");
            e.HasKey(x => x.RequestId);
            e.HasIndex(x => x.CharacterId);
            e.Property(x => x.Operation).HasMaxLength(64).IsRequired();
            e.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
        });
    }
}
