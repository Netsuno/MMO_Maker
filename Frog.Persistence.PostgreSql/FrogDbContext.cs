using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Frog.Persistence.PostgreSql.Entities.Auth;
using Frog.Persistence.PostgreSql.Entities.Ops;
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
    public DbSet<MapEventPlacementEntity> MapEventPlacements => Set<MapEventPlacementEntity>();
    public DbSet<MapPublishedEventPlacementEntity> MapPublishedEventPlacements => Set<MapPublishedEventPlacementEntity>();
    public DbSet<MapEventDefinitionEntity> MapEventDefinitions => Set<MapEventDefinitionEntity>();
    public DbSet<MapEventPublishedSnapshotEntity> MapEventPublishedSnapshots => Set<MapEventPublishedSnapshotEntity>();
    public DbSet<MapEventPublicationHistoryEntity> MapEventPublicationHistory => Set<MapEventPublicationHistoryEntity>();
    public DbSet<MapPublishedSnapshotEntity> MapPublishedSnapshots => Set<MapPublishedSnapshotEntity>();
    public DbSet<MapPublishedCellEntity> MapPublishedCells => Set<MapPublishedCellEntity>();
    public DbSet<MapPublishedWarpEntity> MapPublishedWarps => Set<MapPublishedWarpEntity>();
    public DbSet<MapPublishedNpcSpawnEntity> MapPublishedNpcSpawns => Set<MapPublishedNpcSpawnEntity>();
    public DbSet<MapPublicationHistoryEntity> MapPublicationHistory => Set<MapPublicationHistoryEntity>();
    public DbSet<RuntimeMapBindingEntity> RuntimeMapBindings => Set<RuntimeMapBindingEntity>();
    public DbSet<WorldSpawnSettingsEntity> WorldSpawnSettings => Set<WorldSpawnSettingsEntity>();
    public DbSet<TilesetEntity> Tilesets => Set<TilesetEntity>();

    /// <summary>Catalogue TileAsset. Les déblocages <c>player.player_tile_unlocks</c> ne sont pas mappés.</summary>
    public DbSet<ContentTileEntity> ContentTiles => Set<ContentTileEntity>();

    public DbSet<ContentTilePackEntity> ContentTilePacks => Set<ContentTilePackEntity>();

    public DbSet<ContentTilePackEntryEntity> ContentTilePackEntries => Set<ContentTilePackEntryEntity>();
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
    public DbSet<ActorEntity> Actors => Set<ActorEntity>();
    public DbSet<ActorPublishedSnapshotEntity> ActorPublishedSnapshots => Set<ActorPublishedSnapshotEntity>();
    public DbSet<ActorPublicationHistoryEntity> ActorPublicationHistory => Set<ActorPublicationHistoryEntity>();
    public DbSet<SystemFlagEntity> SystemFlags => Set<SystemFlagEntity>();
    public DbSet<SystemFlagPublishedSnapshotEntity> SystemFlagPublishedSnapshots =>
        Set<SystemFlagPublishedSnapshotEntity>();
    public DbSet<SystemFlagPublicationHistoryEntity> SystemFlagPublicationHistory =>
        Set<SystemFlagPublicationHistoryEntity>();
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

    public DbSet<OperatorEntity> AuthOperators => Set<OperatorEntity>();

    public DbSet<AccountSanctionEntity> OpsAccountSanctions => Set<AccountSanctionEntity>();

    public DbSet<ModerationEventEntity> OpsModerationEvents => Set<ModerationEventEntity>();

    public DbSet<CharacterEntity> PlayerCharacters => Set<CharacterEntity>();

    public DbSet<InventorySlotEntity> PlayerInventorySlots => Set<InventorySlotEntity>();

    public DbSet<BankSlotEntity> PlayerBankSlots => Set<BankSlotEntity>();

    public DbSet<GroundItemEntity> PlayerGroundItems => Set<GroundItemEntity>();

    public DbSet<ShopStockEntity> PlayerShopStock => Set<ShopStockEntity>();

    public DbSet<EconomyRequestIdEntity> PlayerEconomyRequestIds => Set<EconomyRequestIdEntity>();

    public DbSet<MonsterKillRewardEntity> PlayerMonsterKillRewards => Set<MonsterKillRewardEntity>();

    public DbSet<CharacterWorldSwitchEntity> PlayerCharacterWorldSwitches => Set<CharacterWorldSwitchEntity>();

    public DbSet<CharacterWorldVariableEntity> PlayerCharacterWorldVariables => Set<CharacterWorldVariableEntity>();

    public DbSet<CharacterQuestProgressEntity> PlayerCharacterQuestProgress => Set<CharacterQuestProgressEntity>();

    public DbSet<CharacterProfessionProgressEntity> PlayerCharacterProfessionProgress =>
        Set<CharacterProfessionProgressEntity>();

    public DbSet<EventCraftRequestEntity> PlayerEventCraftRequests => Set<EventCraftRequestEntity>();

    public DbSet<MapEventExecutionRequestEntity> PlayerMapEventExecutionRequests =>
        Set<MapEventExecutionRequestEntity>();

    public DbSet<QuestTurnInRequestEntity> PlayerQuestTurnInRequests => Set<QuestTurnInRequestEntity>();

    public DbSet<GuildEntity> PlayerGuilds => Set<GuildEntity>();

    public DbSet<GuildMemberEntity> PlayerGuildMembers => Set<GuildMemberEntity>();

    public DbSet<GuildInviteEntity> PlayerGuildInvites => Set<GuildInviteEntity>();

    public DbSet<FriendshipEntity> PlayerFriendships => Set<FriendshipEntity>();

    public DbSet<CharacterBlockEntity> PlayerCharacterBlocks => Set<CharacterBlockEntity>();

    public DbSet<TradeExecutionEntity> PlayerTradeExecutions => Set<TradeExecutionEntity>();

    public DbSet<Phase8ContentDefinitionEntity> Phase8ContentDefinitions => Set<Phase8ContentDefinitionEntity>();

    public DbSet<Phase8ContentPublishedSnapshotEntity> Phase8ContentPublishedSnapshots =>
        Set<Phase8ContentPublishedSnapshotEntity>();

    public DbSet<Phase8ContentPublicationHistoryEntity> Phase8ContentPublicationHistory =>
        Set<Phase8ContentPublicationHistoryEntity>();

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
            e.Property(x => x.PrefabsJson).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<byte>();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_maps_positive_size", "width > 0 AND height > 0");
                t.HasCheckConstraint("ck_maps_non_negative_revision", "revision >= 0");
            });
            e.HasMany(x => x.Cells).WithOne(x => x.Map).HasForeignKey(x => x.MapId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Warps).WithOne(x => x.Map).HasForeignKey(x => x.MapId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.NpcSpawns).WithOne(x => x.Map).HasForeignKey(x => x.MapId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.EventPlacements).WithOne(x => x.Map).HasForeignKey(x => x.MapId).OnDelete(DeleteBehavior.Cascade);
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
            e.Property(x => x.NpcId).IsRequired();
        });

        modelBuilder.Entity<MapEventPlacementEntity>(e =>
        {
            e.ToTable("map_event_placements", "world");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MapId, x.TileX, x.TileY, x.EventDefinitionId }).IsUnique();
            e.Property(x => x.TriggerKind).HasMaxLength(32).IsRequired();
            e.Property(x => x.MovementKind).HasMaxLength(16).IsRequired();
            e.Property(x => x.RouteWaypointsJson).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<MapPublishedSnapshotEntity>(e =>
        {
            e.ToTable("map_published_snapshots");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MapId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.LayersCatalogJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.PrefabsJson).HasColumnType("jsonb");
            e.HasMany(x => x.Cells).WithOne(x => x.Snapshot).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Warps).WithOne(x => x.Snapshot).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.NpcSpawns).WithOne(x => x.Snapshot).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.EventPlacements).WithOne(x => x.Snapshot).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MapPublishedNpcSpawnEntity>(e =>
        {
            e.ToTable("map_published_npc_spawns");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SnapshotId, x.X, x.Y, x.NpcId });
            e.ToTable(t => t.HasCheckConstraint("ck_map_published_npc_spawns_tiles", "x >= 0 AND y >= 0"));
        });

        modelBuilder.Entity<MapPublishedEventPlacementEntity>(e =>
        {
            e.ToTable("map_published_event_placements", "world");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SnapshotId, x.TileX, x.TileY, x.EventDefinitionId });
            e.Property(x => x.TriggerKind).HasMaxLength(32).IsRequired();
            e.Property(x => x.MovementKind).HasMaxLength(16).IsRequired();
            e.Property(x => x.RouteWaypointsJson).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<RuntimeMapBindingEntity>(e =>
        {
            e.ToTable("runtime_map_bindings");
            e.HasKey(x => x.MapId);
            e.HasIndex(x => x.RuntimeMapId).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_runtime_map_bindings_positive", "runtime_map_id > 0"));
        });

        modelBuilder.Entity<WorldSpawnSettingsEntity>(e =>
        {
            e.ToTable("world_spawn_settings");
            e.HasKey(x => x.Id);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_world_spawn_settings_singleton", "id = 1");
                t.HasCheckConstraint("ck_world_spawn_settings_tiles", "start_tile_x >= 0 AND start_tile_y >= 0 AND respawn_tile_x >= 0 AND respawn_tile_y >= 0");
            });
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
            e.Property(x => x.PngBytes).HasColumnType("bytea");
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

        modelBuilder.Entity<MapEventDefinitionEntity>(e =>
        {
            e.ToTable("map_event_definitions", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CatalogSlug).IsUnique().HasFilter("catalog_slug IS NOT NULL");
            e.HasIndex(x => x.EditorAliasId).IsUnique().HasFilter("editor_alias_id IS NOT NULL");
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.CatalogSlug).HasMaxLength(64);
            e.Property(x => x.PagesJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
        });

        modelBuilder.Entity<MapEventPublishedSnapshotEntity>(e =>
        {
            e.ToTable("map_event_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventDefinitionId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.CatalogSlug).HasMaxLength(64);
            e.Property(x => x.PagesJson).HasColumnType("jsonb").IsRequired();
            e.HasOne(x => x.EventDefinition).WithMany().HasForeignKey(x => x.EventDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MapEventPublicationHistoryEntity>(e =>
        {
            e.ToTable("map_event_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EventDefinitionId);
            e.HasOne(x => x.EventDefinition).WithMany().HasForeignKey(x => x.EventDefinitionId).OnDelete(DeleteBehavior.Cascade);
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
            e.HasIndex(x => x.DefaultWeaponItemId);
            e.HasIndex(x => x.DefaultArmorItemId);
            e.HasOne<SpellEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingSpellId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.DefaultWeaponItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.DefaultArmorItemId)
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
                t.HasCheckConstraint(
                    "ck_classes_default_equipment_distinct",
                    "default_weapon_item_id IS NULL OR default_armor_item_id IS NULL "
                    + "OR default_weapon_item_id <> default_armor_item_id");
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
            e.HasIndex(x => x.DefaultWeaponItemId);
            e.HasIndex(x => x.DefaultArmorItemId);
            e.HasOne<SpellEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingSpellId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.DefaultWeaponItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.DefaultArmorItemId)
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
                t.HasCheckConstraint(
                    "ck_class_published_snapshots_default_equipment_distinct",
                    "default_weapon_item_id IS NULL OR default_armor_item_id IS NULL "
                    + "OR default_weapon_item_id <> default_armor_item_id");
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

        modelBuilder.Entity<ActorEntity>(e =>
        {
            e.ToTable("actors", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(ActorDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ActorDefinition.MaxDescriptionLength);
            e.Property(x => x.FaceLogicalPath).HasMaxLength(ActorDefinition.MaxLogicalPathLength);
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasIndex(x => x.ClassId);
            e.HasIndex(x => x.StartingWeaponItemId);
            e.HasIndex(x => x.StartingArmorItemId);
            e.HasOne<ClassEntity>()
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingWeaponItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingArmorItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_actors_positive_resources", "base_hp > 0 AND base_mp > 0");
                t.HasCheckConstraint(
                    "ck_actors_stats",
                    "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 "
                    + "AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 "
                    + "AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                t.HasCheckConstraint(
                    "ck_actors_look",
                    "body >= 0 AND body < 4 AND hair >= 0 AND hair < 5 AND tunic >= 0 AND tunic < 4");
                t.HasCheckConstraint("ck_actors_non_negative_revision", "revision >= 0");
            });
        });

        modelBuilder.Entity<ActorPublishedSnapshotEntity>(e =>
        {
            e.ToTable("actor_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ActorId, x.Revision }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(ActorDefinition.MaxNameLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(ActorDefinition.MaxDescriptionLength);
            e.Property(x => x.FaceLogicalPath).HasMaxLength(ActorDefinition.MaxLogicalPathLength);
            e.HasIndex(x => x.ClassId);
            e.HasIndex(x => x.StartingWeaponItemId);
            e.HasIndex(x => x.StartingArmorItemId);
            e.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ClassEntity>()
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingWeaponItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ItemEntity>()
                .WithMany()
                .HasForeignKey(x => x.StartingArmorItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_actor_published_snapshots_positive_resources",
                    "base_hp > 0 AND base_mp > 0");
                t.HasCheckConstraint(
                    "ck_actor_published_snapshots_stats",
                    "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 "
                    + "AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 "
                    + "AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                t.HasCheckConstraint(
                    "ck_actor_published_snapshots_look",
                    "body >= 0 AND body < 4 AND hair >= 0 AND hair < 5 AND tunic >= 0 AND tunic < 4");
            });
        });

        modelBuilder.Entity<ActorPublicationHistoryEntity>(e =>
        {
            e.ToTable("actor_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ActorId);
            e.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemFlagEntity>(e =>
        {
            e.ToTable("system_flags", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.FlagKey).HasMaxLength(SystemFlagDefinition.MaxKeyLength).IsRequired();
            e.Property(x => x.Label).HasMaxLength(SystemFlagDefinition.MaxLabelLength).IsRequired();
            e.Property(x => x.Note).HasMaxLength(SystemFlagDefinition.MaxNoteLength);
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasIndex(x => new { x.Kind, x.FlagKey }).IsUnique();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_system_flags_kind", "kind >= 0 AND kind <= 1");
                t.HasCheckConstraint("ck_system_flags_non_negative_revision", "revision >= 0");
                t.HasCheckConstraint(
                    "ck_system_flags_key_label",
                    "char_length(flag_key) >= 1 AND char_length(label) >= 1");
            });
        });

        modelBuilder.Entity<SystemFlagPublishedSnapshotEntity>(e =>
        {
            e.ToTable("system_flag_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FlagId, x.Revision }).IsUnique();
            e.Property(x => x.Kind).HasConversion<byte>();
            e.Property(x => x.FlagKey).HasMaxLength(SystemFlagDefinition.MaxKeyLength).IsRequired();
            e.Property(x => x.Label).HasMaxLength(SystemFlagDefinition.MaxLabelLength).IsRequired();
            e.Property(x => x.Note).HasMaxLength(SystemFlagDefinition.MaxNoteLength);
            e.HasOne(x => x.Flag)
                .WithMany()
                .HasForeignKey(x => x.FlagId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_system_flag_published_snapshots_kind", "kind >= 0 AND kind <= 1");
                t.HasCheckConstraint(
                    "ck_system_flag_published_snapshots_key_label",
                    "char_length(flag_key) >= 1 AND char_length(label) >= 1");
            });
        });

        modelBuilder.Entity<SystemFlagPublicationHistoryEntity>(e =>
        {
            e.ToTable("system_flag_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FlagId);
            e.HasOne(x => x.Flag)
                .WithMany()
                .HasForeignKey(x => x.FlagId)
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

        modelBuilder.Entity<OperatorEntity>(e =>
        {
            e.ToTable("operators", "auth");
            e.HasKey(x => x.AccountId);
            e.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.GrantedBy).HasMaxLength(64).IsRequired();
            e.Property(x => x.Note).HasMaxLength(256);
            e.Property(x => x.GrantedAtUtc).IsRequired();
            e.HasIndex(x => x.RevokedAtUtc);
        });

        modelBuilder.Entity<AccountSanctionEntity>(e =>
        {
            e.ToTable("account_sanctions", "ops", t =>
            {
                t.HasCheckConstraint("ck_account_sanctions_kind", "kind IN ('mute', 'ban')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(8).IsRequired();
            e.Property(x => x.Reason).IsRequired();
            e.Property(x => x.CreatedAtUtc).IsRequired();
            e.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ActorAccount)
                .WithMany()
                .HasForeignKey(x => x.ActorAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AccountId, x.Kind })
                .IsUnique()
                .HasFilter("revoked_at_utc IS NULL")
                .HasDatabaseName("ux_account_sanctions_active_kind");
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.ActorAccountId);
        });

        modelBuilder.Entity<ModerationEventEntity>(e =>
        {
            e.ToTable("moderation_events", "ops", t =>
            {
                t.HasCheckConstraint(
                    "ck_moderation_events_action",
                    "action IN ('mute', 'unmute', 'kick', 'ban', 'unban')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.AtUtc).IsRequired();
            e.Property(x => x.Action).HasMaxLength(8).IsRequired();
            e.Property(x => x.Reason).IsRequired();
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            e.HasOne(x => x.ActorAccount)
                .WithMany()
                .HasForeignKey(x => x.ActorAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TargetAccount)
                .WithMany()
                .HasForeignKey(x => x.TargetAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TargetAccountId, x.AtUtc });
            e.HasIndex(x => x.ActorAccountId);
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
            // Cle scopee au (personnage, requestId) uniquement : un requestId ne peut
            // jamais etre rejoue avec une operation ou un payload differents (cf.
            // TryReplayAsync/StoreRequestAsync qui appliquent la verification stricte).
            e.HasKey(x => new { x.CharacterId, x.RequestId });
            e.Property(x => x.Operation).HasMaxLength(64).IsRequired();
            e.Property(x => x.RequestFingerprint).HasColumnType("bytea").IsRequired();
            e.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<MonsterKillRewardEntity>(e =>
        {
            e.ToTable("monster_kill_rewards", "player");
            e.HasKey(x => new { x.CharacterId, x.MonsterInstanceId });
            e.Property(x => x.ExperienceAmount).IsRequired();
        });

        modelBuilder.Entity<CharacterWorldSwitchEntity>(e =>
        {
            e.ToTable("character_world_switches", "player");
            e.HasKey(x => new { x.CharacterId, x.SwitchKey });
            e.Property(x => x.SwitchKey).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Character)
                .WithMany(x => x.WorldSwitches)
                .HasForeignKey(x => x.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterWorldVariableEntity>(e =>
        {
            e.ToTable("character_world_variables", "player");
            e.HasKey(x => new { x.CharacterId, x.VariableKey });
            e.Property(x => x.VariableKey).HasMaxLength(64).IsRequired();
            e.HasOne<CharacterEntity>()
                .WithMany(x => x.WorldVariables)
                .HasForeignKey(x => x.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterQuestProgressEntity>(e =>
        {
            e.ToTable("character_quest_progress", "player");
            e.HasKey(x => new { x.CharacterId, x.QuestId });
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.ObjectiveCountersJson).HasColumnType("jsonb").HasDefaultValue("{}");
        });

        modelBuilder.Entity<CharacterProfessionProgressEntity>(e =>
        {
            e.ToTable("character_profession_progress", "player");
            e.HasKey(x => new { x.CharacterId, x.ProfessionId });
        });

        modelBuilder.Entity<EventCraftRequestEntity>(e =>
        {
            e.ToTable("event_craft_requests", "player");
            e.HasKey(x => new { x.CharacterId, x.RequestId });
            e.Property(x => x.RecipeId).IsRequired();
        });

        modelBuilder.Entity<MapEventExecutionRequestEntity>(e =>
        {
            e.ToTable("map_event_execution_requests", "player");
            e.HasKey(x => new { x.CharacterId, x.RequestId });
            e.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.ActivationId).IsRequired();
            e.Property(x => x.WaitOrdinal).IsRequired();
            e.HasIndex(x => new { x.CharacterId, x.PlacementId, x.CatalogAliasId });
            e.HasIndex(x => new { x.CharacterId, x.ActivationId, x.WaitOrdinal })
                .IsUnique()
                .HasDatabaseName("ix_map_event_execution_requests_activation_ordinal");
            e.HasIndex(x => x.RequestId)
                .IsUnique()
                .HasDatabaseName("ix_map_event_execution_requests_request_id");
        });

        modelBuilder.Entity<QuestTurnInRequestEntity>(e =>
        {
            e.ToTable("quest_turn_in_requests", "player");
            e.HasKey(x => new { x.CharacterId, x.RequestId });
            e.Property(x => x.QuestId).IsRequired();
            e.HasIndex(x => new { x.CharacterId, x.QuestId });
        });

        modelBuilder.Entity<Phase8ContentDefinitionEntity>(e =>
        {
            e.ToTable("phase8_content_definitions", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Revision).IsRequired().IsConcurrencyToken();
            e.HasIndex(x => new { x.Kind, x.Name });
            e.HasIndex(x => new { x.Kind, x.EditorAliasId }).IsUnique().HasFilter("editor_alias_id IS NOT NULL");
        });

        modelBuilder.Entity<Phase8ContentPublishedSnapshotEntity>(e =>
        {
            e.ToTable("phase8_content_published_snapshots", "content");
            e.HasKey(x => x.Id);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(x => new { x.ContentDefinitionId, x.Revision }).IsUnique();
            e.HasOne(x => x.ContentDefinition)
                .WithMany()
                .HasForeignKey(x => x.ContentDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Phase8ContentPublicationHistoryEntity>(e =>
        {
            e.ToTable("phase8_content_publication_history", "content");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ContentDefinitionId, x.Revision }).IsUnique();
            e.HasOne(x => x.ContentDefinition)
                .WithMany()
                .HasForeignKey(x => x.ContentDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuildEntity>(e =>
        {
            e.ToTable("guilds", "player");
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(32).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.NormalizedName).IsUnique();
            e.Property(x => x.Motd).HasMaxLength(256).IsRequired();
            e.HasMany(x => x.Members).WithOne(x => x.Guild).HasForeignKey(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Invites).WithOne(x => x.Guild).HasForeignKey(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuildMemberEntity>(e =>
        {
            e.ToTable("guild_members", "player", t =>
            {
                t.HasCheckConstraint("ck_guild_members_role", "role IN (0, 1, 2)");
            });
            e.HasKey(x => new { x.GuildId, x.CharacterId });
            e.HasIndex(x => x.CharacterId).IsUnique();
            e.HasOne(x => x.Character)
                .WithMany()
                .HasForeignKey(x => x.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.RoleEnum);
        });

        modelBuilder.Entity<GuildInviteEntity>(e =>
        {
            e.ToTable("guild_invites", "player");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(16).IsRequired();
            e.HasIndex(x => new { x.GuildId, x.ToCharacterId })
                .IsUnique()
                .HasFilter("status = 'pending'");
            e.HasIndex(x => x.ToCharacterId);
            e.HasIndex(x => x.FromCharacterId);
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.FromCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.ToCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FriendshipEntity>(e =>
        {
            e.ToTable("friendships", "player", t =>
            {
                t.HasCheckConstraint("ck_friendships_pair", "character_a < character_b");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(16).IsRequired();
            e.HasIndex(x => new { x.CharacterA, x.CharacterB }).IsUnique();
            e.HasIndex(x => x.RequestedBy);
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.CharacterA)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.CharacterB)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CharacterBlockEntity>(e =>
        {
            e.ToTable("character_blocks", "player");
            e.HasKey(x => new { x.BlockerCharacterId, x.BlockedCharacterId });
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.BlockerCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.BlockedCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TradeExecutionEntity>(e =>
        {
            e.ToTable("trade_executions", "player");
            e.HasKey(x => x.TradeId);
            e.Property(x => x.ContentsJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(x => x.CommittedAtUtc);
            e.HasIndex(x => x.CommitRequestId);
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.InitiatorCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterEntity>()
                .WithMany()
                .HasForeignKey(x => x.PartnerCharacterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Tables créées par 20260924214100_TileAssetCatalog (migration écrite à la main, hors snapshot).
        // ExcludeFromMigrations évite un second CREATE TABLE au prochain `dotnet ef migrations add`.
        modelBuilder.Entity<ContentTileEntity>(e =>
        {
            e.ToTable("tiles", "content", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.TileAssetId);
            e.HasAlternateKey(x => x.Id);
            e.Property(x => x.TileAssetId).HasColumnType("char(64)").HasMaxLength(64).IsFixedLength().IsRequired();
            e.Property(x => x.PngBytes).HasColumnType("bytea").IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(120);
            e.Property(x => x.Tags).HasColumnType("text[]").IsRequired();
            e.Property(x => x.MetaJson).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<ContentTilePackEntity>(e =>
        {
            e.ToTable("tile_packs", "content", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Slug, x.Version });
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Version).HasMaxLength(64).IsRequired();
            e.Property(x => x.FrogpackSha256).HasColumnType("char(64)").HasMaxLength(64).IsFixedLength();
            e.Property(x => x.FrogpackBytes).HasColumnType("bytea");
            e.Property(x => x.Ed25519Signature).HasColumnName("ed25519_signature").HasColumnType("bytea");
            e.Property(x => x.Ed25519PublicKeyId).HasColumnName("ed25519_public_key_id").HasMaxLength(64);
            e.Property(x => x.ManifestJson).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<ContentTilePackEntryEntity>(e =>
        {
            e.ToTable("tile_pack_entries", "content", t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.PackId, x.TileAssetId });
            e.Property(x => x.TileAssetId).HasColumnType("char(64)").HasMaxLength(64).IsFixedLength().IsRequired();
            e.Property(x => x.EntryMetaJson).HasColumnType("jsonb").IsRequired();
            e.HasOne<ContentTilePackEntity>()
                .WithMany()
                .HasForeignKey(x => x.PackId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ContentTileEntity>()
                .WithMany()
                .HasForeignKey(x => x.TileAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
