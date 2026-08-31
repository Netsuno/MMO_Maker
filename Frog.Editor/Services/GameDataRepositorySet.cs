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
        EditorShopRepositoryBundle shop,
        EditorResourceRepositoryBundle resource,
        EditorResourceSpawnRepositoryBundle resourceSpawn,
        EditorPostgreSqlScope? databaseScope)
    {
        Map = map;
        Tileset = tileset;
        Npc = npc;
        Item = item;
        Spell = spell;
        Class = classBundle;
        Shop = shop;
        Resource = resource;
        ResourceSpawn = resourceSpawn;
        DatabaseScope = databaseScope;
    }

    public EditorMapRepositoryBundle Map { get; }

    public EditorTilesetRepositoryBundle Tileset { get; }

    public EditorNpcRepositoryBundle Npc { get; }

    public EditorItemRepositoryBundle Item { get; }

    public EditorSpellRepositoryBundle Spell { get; }

    public EditorClassRepositoryBundle Class { get; }

    public EditorShopRepositoryBundle Shop { get; }

    public EditorResourceRepositoryBundle Resource { get; }

    public EditorResourceSpawnRepositoryBundle ResourceSpawn { get; }

    public EditorPostgreSqlScope? DatabaseScope { get; }

    public void Dispose() => DatabaseScope?.Dispose();
}
