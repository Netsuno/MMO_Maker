using Frog.Application.Playtest;
using Frog.Application.Prefabs;
using Frog.Core.Models;

namespace Frog.Editor.Config;

/// <summary>Mémo placements prefab par carte dans <c>editor-workstate.json</c>.</summary>
public static class EditorMapPrefabWorkstate
{
    public static bool TryRead(Guid? mapId, Map map, out List<PrefabPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(map);
        var key = MapPlaytestSpawn.BuildWorkstateKey(mapId, map.Name, map.Width, map.Height);
        return EditorLocalWorkstate.TryReadMapPrefabPlacements(key, out placements);
    }

    public static void Write(Guid? mapId, Map map, IReadOnlyList<PrefabPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(placements);
        var key = MapPlaytestSpawn.BuildWorkstateKey(mapId, map.Name, map.Width, map.Height);
        EditorLocalWorkstate.WriteMapPrefabPlacements(key, PrefabPlacementService.ClonePlacements(placements));
    }
}
