using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Persistence.PostgreSql;

namespace Frog.Editor.Services;

/// <summary>Ensemble de dépôts initialisé une fois pour la fenêtre Données de jeu.</summary>
public sealed class GameDataRepositorySet : IDisposable
{
    public GameDataRepositorySet(
        EditorMapRepositoryBundle map,
        EditorTilesetRepositoryBundle tileset,
        EditorNpcRepositoryBundle npc,
        EditorItemRepositoryBundle item,
        EditorSpellRepositoryBundle spell,
        EditorClassRepositoryBundle classBundle,
        EditorActorRepositoryBundle actor,
        EditorShopRepositoryBundle shop,
        EditorResourceRepositoryBundle resource,
        EditorResourceSpawnRepositoryBundle resourceSpawn,
        IPhase8ContentEditorRepository systemCatalog,
        EditorPostgreSqlScope? databaseScope)
    {
        Map = map;
        Tileset = tileset;
        Npc = npc;
        Item = item;
        Spell = spell;
        Class = classBundle;
        Actor = actor;
        Shop = shop;
        Resource = resource;
        ResourceSpawn = resourceSpawn;
        SystemCatalog = systemCatalog ?? throw new ArgumentNullException(nameof(systemCatalog));
        DatabaseScope = databaseScope;
    }

    public EditorMapRepositoryBundle Map { get; }

    public EditorTilesetRepositoryBundle Tileset { get; }

    public EditorNpcRepositoryBundle Npc { get; }

    public EditorItemRepositoryBundle Item { get; }

    public EditorSpellRepositoryBundle Spell { get; }

    public EditorClassRepositoryBundle Class { get; }

    public EditorActorRepositoryBundle Actor { get; }

    public EditorShopRepositoryBundle Shop { get; }

    public EditorResourceRepositoryBundle Resource { get; }

    public EditorResourceSpawnRepositoryBundle ResourceSpawn { get; }

    /// <summary>Catalogue Système (interrupteurs et variables), dépôt Phase 8.</summary>
    public IPhase8ContentEditorRepository SystemCatalog { get; }

    public EditorPostgreSqlScope? DatabaseScope { get; }

    public void Dispose() => DatabaseScope?.Dispose();
}
