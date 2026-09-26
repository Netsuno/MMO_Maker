using Frog.Application.Playtest;
using Frog.Core.Models;

namespace Frog.Editor.Config;

/// <summary>Mémo spawn playtest par carte dans <c>editor-workstate.json</c>.</summary>
public static class EditorMapSpawnWorkstate
{
    public static bool TryRead(Guid? mapId, Map map, out int tileX, out int tileY)
    {
        ArgumentNullException.ThrowIfNull(map);
        var key = MapPlaytestSpawn.BuildWorkstateKey(mapId, map.Name, map.Width, map.Height);
        return EditorLocalWorkstate.TryReadMapPlaytestSpawn(key, out tileX, out tileY);
    }

    public static void Write(Guid? mapId, Map map, int tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!MapPlaytestSpawn.TryClamp(map, tileX, tileY, out var x, out var y))
        {
            return;
        }

        var key = MapPlaytestSpawn.BuildWorkstateKey(mapId, map.Name, map.Width, map.Height);
        EditorLocalWorkstate.WriteMapPlaytestSpawn(key, x, y);
    }

    public static void Clear(Guid? mapId, Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var key = MapPlaytestSpawn.BuildWorkstateKey(mapId, map.Name, map.Width, map.Height);
        EditorLocalWorkstate.RemoveMapPlaytestSpawn(key);
    }
}
