using Frog.Core.Enums;

namespace Frog.Core.Instances;

/// <summary>Catalogue MVP fixe (2 templates). Persistence PG = TODO STATUS.</summary>
public static class DungeonCatalog
{
    public static readonly DungeonDefinition MarshRuins = new(
        Guid.Parse("a1111111-1111-4111-8111-111111111111"),
        "Ruines du Marais",
        InstanceHubKind.Dungeon,
        TemplateMapId: 1,
        SpawnTileX: 1,
        SpawnTileY: 1,
        MinPartySize: 1);

    public static readonly DungeonDefinition KingCrypt = new(
        Guid.Parse("a2222222-2222-4222-8222-222222222222"),
        "Crypte du Roi",
        InstanceHubKind.Raid,
        TemplateMapId: 1,
        SpawnTileX: 2,
        SpawnTileY: 2,
        MinPartySize: 2);

    public static IReadOnlyList<DungeonDefinition> All { get; } = [MarshRuins, KingCrypt];

    public static DungeonDefinition? Find(Guid id)
    {
        foreach (var def in All)
        {
            if (def.Id == id)
            {
                return def;
            }
        }

        return null;
    }

    public static IReadOnlyList<DungeonDefinition> OfKind(InstanceHubKind kind)
    {
        var list = new List<DungeonDefinition>(All.Count);
        foreach (var def in All)
        {
            if (def.Kind == kind)
            {
                list.Add(def);
            }
        }

        return list;
    }
}
