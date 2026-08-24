using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

/// <summary>Initialisation asynchrone unique des dépôts Données de jeu (une migration, portée DB explicite).</summary>
public static class GameDataInitializationService
{
    public static Task<GameDataRepositorySet> InitializeAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
        => InitializeCoreAsync(progress, cancellationToken);

    public static GameDataRepositorySet CreateInjectedSet() => CreateFromInjected();

    private static async Task<GameDataRepositorySet> InitializeCoreAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Préparation des dépôts…");

        if (HasInjectedRepositories())
        {
            return CreateFromInjected();
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            return CreateInMemorySet(ContentRepositoryCapabilities.InMemoryTest);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return CreateInMemorySet(ContentRepositoryCapabilities.InMemoryDemo);
        }

        progress?.Report("Migration PostgreSQL…");
        var scope = new EditorPostgreSqlScope(connectionString);
        try
        {
            await scope.MigrateAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report("Chargement des catalogues…");
            return CreatePostgreSqlSet(scope);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    internal static bool HasInjectedRepositories()
        => EditorTestHooks.OverrideMapRepository is not null
           || EditorTestHooks.OverrideTilesetRepository is not null
           || EditorTestHooks.OverrideNpcRepository is not null
           || EditorTestHooks.OverrideItemRepository is not null
           || EditorTestHooks.OverrideSpellRepository is not null
           || EditorTestHooks.OverrideClassRepository is not null
           || EditorTestHooks.OverrideShopRepository is not null
           || EditorTestHooks.OverrideResourceRepository is not null
           || EditorTestHooks.OverrideResourceSpawnRepository is not null;

    private static GameDataRepositorySet CreateFromInjected()
    {
        var map = EditorMapRepositoryFactory.CreateBundle();
        var tileset = EditorTilesetRepositoryFactory.CreateBundle();
        var npc = EditorNpcRepositoryFactory.CreateBundle();
        var item = EditorItemRepositoryFactory.CreateBundle();
        var spell = EditorSpellRepositoryFactory.CreateBundle();
        var classBundle = EditorClassRepositoryFactory.CreateBundle(spell.Repository);
        var shop = EditorShopRepositoryFactory.CreateBundle(item.PublishedCatalog);
        var resource = EditorResourceRepositoryFactory.CreateBundle(item.PublishedCatalog);
        var spawn = EditorResourceSpawnRepositoryFactory.CreateBundle(
            map.Repository,
            resource.PublishedCatalog,
            resource.Capabilities);
        return new GameDataRepositorySet(
            map,
            tileset,
            npc,
            item,
            spell,
            classBundle,
            shop,
            resource,
            spawn,
            databaseScope: null);
    }

    private static GameDataRepositorySet CreateInMemorySet(ContentRepositoryCapabilities capabilities)
    {
        var mapRepo = new InMemoryMapRepository(
            capabilities == ContentRepositoryCapabilities.InMemoryTest
                ? MapRepositoryCapabilities.InMemoryTest
                : MapRepositoryCapabilities.InMemoryDemo);
        var map = new EditorMapRepositoryBundle(mapRepo, mapRepo.Capabilities);

        var tilesetMem = new InMemoryTilesetRepository(capabilities);
        var tileset = new EditorTilesetRepositoryBundle(tilesetMem, tilesetMem, tilesetMem.Capabilities);

        var npcMem = new InMemoryNpcRepository(capabilities);
        var npc = new EditorNpcRepositoryBundle(npcMem, npcMem, npcMem.Capabilities);

        var itemMem = new InMemoryItemRepository(capabilities);
        var item = new EditorItemRepositoryBundle(itemMem, itemMem, itemMem.Capabilities);

        var spellMem = new InMemorySpellRepository(capabilities);
        var spell = new EditorSpellRepositoryBundle(spellMem, spellMem, spellMem.Capabilities);

        var classMem = new InMemoryClassRepository(spellMem, capabilities);
        var classBundle = new EditorClassRepositoryBundle(classMem, classMem, classMem.Capabilities);

        var shopMem = new InMemoryShopRepository(itemMem, capabilities);
        var shop = new EditorShopRepositoryBundle(shopMem, shopMem, shopMem.Capabilities);

        var resourceMem = new InMemoryResourceRepository(itemMem, capabilities);
        var resource = new EditorResourceRepositoryBundle(resourceMem, resourceMem, resourceMem.Capabilities);

        var spawnMem = new InMemoryResourceSpawnRepository(mapRepo, resourceMem, capabilities);
        var spawn = new EditorResourceSpawnRepositoryBundle(spawnMem, spawnMem, spawnMem.Capabilities);

        return new GameDataRepositorySet(
            map,
            tileset,
            npc,
            item,
            spell,
            classBundle,
            shop,
            resource,
            spawn,
            databaseScope: null);
    }

    private static GameDataRepositorySet CreatePostgreSqlSet(EditorPostgreSqlScope scope)
    {
        var db = scope.Db;
        var mapRepo = new PostgresMapRepository(db);
        var map = new EditorMapRepositoryBundle(mapRepo, mapRepo.Capabilities);

        var tilesetRepo = new PostgresTilesetRepository(db);
        var tileset = new EditorTilesetRepositoryBundle(tilesetRepo, tilesetRepo, tilesetRepo.Capabilities);

        var npcRepo = new PostgresNpcRepository(db);
        var npc = new EditorNpcRepositoryBundle(npcRepo, npcRepo, npcRepo.Capabilities);

        var itemRepo = new PostgresItemRepository(db);
        var item = new EditorItemRepositoryBundle(itemRepo, itemRepo, itemRepo.Capabilities);

        var spellRepo = new PostgresSpellRepository(db);
        var spell = new EditorSpellRepositoryBundle(spellRepo, spellRepo, spellRepo.Capabilities);

        var classRepo = new PostgresClassRepository(db);
        var classBundle = new EditorClassRepositoryBundle(classRepo, classRepo, classRepo.Capabilities);

        var shopRepo = new PostgresShopRepository(db, itemRepo);
        var shop = new EditorShopRepositoryBundle(shopRepo, shopRepo, shopRepo.Capabilities);

        var resourceRepo = new PostgresResourceRepository(db, itemRepo);
        var resource = new EditorResourceRepositoryBundle(resourceRepo, resourceRepo, resourceRepo.Capabilities);

        var spawnRepo = new PostgresResourceSpawnRepository(db, mapRepo, resourceRepo);
        var spawn = new EditorResourceSpawnRepositoryBundle(spawnRepo, spawnRepo, spawnRepo.Capabilities);

        return new GameDataRepositorySet(
            map,
            tileset,
            npc,
            item,
            spell,
            classBundle,
            shop,
            resource,
            spawn,
            scope);
    }
}
