using Frog.Core.Constants;

namespace Frog.Core.Instances;

public sealed record ProceduralDungeonRoom(int Index, int X, int Y, int Width, int Height);

/// <summary>Layout placeholder seedé — pas un moteur procgen. Même seed = mêmes salles.</summary>
public sealed record ProceduralDungeonLayout(
    int Seed,
    int RoomCount,
    IReadOnlyList<ProceduralDungeonRoom> Rooms);

/// <summary>Stub RNG : 2–4 salles alignées sur un template fixe.</summary>
public static class ProceduralDungeonGenerator
{
    public static ProceduralDungeonLayout Generate(int seed)
    {
        var rng = new Random(seed);
        var rooms = rng.Next(InstanceHubLimits.MinProcgenRooms, InstanceHubLimits.MaxProcgenRooms);
        if (rooms > 4)
        {
            rooms = 4;
        }

        var list = new ProceduralDungeonRoom[rooms];
        for (var i = 0; i < rooms; i++)
        {
            list[i] = new ProceduralDungeonRoom(i, i * 8, 0, 6, 6);
        }

        return new ProceduralDungeonLayout(seed, rooms, list);
    }
}
