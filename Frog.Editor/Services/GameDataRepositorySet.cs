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
        EditorSystemFlagRepositoryBundle systemFlag,
        EditorSystemSettingsRepositoryBundle systemSettings,
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
        Actor = actor;
        SystemFlag = systemFlag;
        SystemSettings = systemSettings;
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

    public EditorActorRepositoryBundle Actor { get; }

    public EditorSystemFlagRepositoryBundle SystemFlag { get; }

    public EditorSystemSettingsRepositoryBundle SystemSettings { get; }

    public EditorShopRepositoryBundle Shop { get; }

    public EditorResourceRepositoryBundle Resource { get; }

    public EditorResourceSpawnRepositoryBundle ResourceSpawn { get; }

    public EditorPostgreSqlScope? DatabaseScope { get; }

    public void Dispose() => DatabaseScope?.Dispose();
}
