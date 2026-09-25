using Frog.Core.Enums;

namespace Frog.Core.Maps;

/// <summary>
/// Profondeur monde (nord → sud), façon Graal / Eclipse.
/// Pour chaque rangée : sol et masques, puis acteurs (butin, monstres, PNJ, joueurs) du nord au sud,
/// puis la frange de cette rangée. La frange couvre les acteurs de sa rangée et passe derrière ceux du sud.
/// </summary>
public static class WorldDepth
{
    /// <summary>Ordre de dessin quand l'ancre Y est la même : le plus grand passe devant.</summary>
    public enum ActorSlot
    {
        Loot = 0,
        Monster = 1,
        Npc = 2,
        RemotePlayer = 3,
        LocalPlayer = 4,
    }

    public enum RowStepKind
    {
        ActorsNorthOfMap = 0,
        BelowActors = 1,
        ActorsOnRow = 2,
        Fringe = 3,
        ActorsSouthOfMap = 4,
    }

    /// <summary>Une étape du balayage vertical. <see cref="Row"/> est la rangée de tuiles, ou hors carte.</summary>
    public readonly record struct RowStep(RowStepKind Kind, int Row);

    /// <summary>Clé stable : rangée, puis Y d'ancre, puis slot, puis ordre d'insertion.</summary>
    public readonly record struct ActorKey(int Row, int BaselineY, ActorSlot Slot, int Sequence);

    /// <summary>Frange et frange 2 : devant les acteurs de la même rangée.</summary>
    public static bool IsFringeLayer(LayerType type) =>
        type is LayerType.Fringe or LayerType.Fringe2;

    /// <summary>Attributs (blocage, warp) : teinte, pas un sprite de profondeur.</summary>
    public static bool IsAttributeLayer(LayerType type) =>
        type == LayerType.Attributes;

    /// <summary>Sol, masque, masque 2 : sous les acteurs de la rangée.</summary>
    public static bool IsBelowActorLayer(LayerType type) =>
        !IsFringeLayer(type) && !IsAttributeLayer(type);

    /// <summary>Rangée de tuile de l'ancre (division entière vers −∞, y compris au nord de 0).</summary>
    public static int FloorTileIndex(int pixel, int tileSizePixels)
    {
        if (tileSizePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSizePixels));
        }

        var quotient = pixel / tileSizePixels;
        var remainder = pixel % tileSizePixels;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    /// <summary>Rangée de tuile d'une ancre flottante (pieds / centre).</summary>
    public static int FloorTileIndex(float pixel, int tileSizePixels)
    {
        if (tileSizePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSizePixels));
        }

        return (int)MathF.Floor(pixel / tileSizePixels);
    }

    /// <summary>Rangée sud d'une empreinte prefab : les acteurs de cette rangée passent devant.</summary>
    public static int FootprintBaselineRow(int originTileY, int heightTiles)
    {
        var height = Math.Max(1, heightTiles);
        return originTileY + height - 1;
    }

    public static int CompareActors(ActorKey a, ActorKey b)
    {
        var row = a.Row.CompareTo(b.Row);
        if (row != 0)
        {
            return row;
        }

        var baseline = a.BaselineY.CompareTo(b.BaselineY);
        if (baseline != 0)
        {
            return baseline;
        }

        var slot = ((int)a.Slot).CompareTo((int)b.Slot);
        if (slot != 0)
        {
            return slot;
        }

        return a.Sequence.CompareTo(b.Sequence);
    }

    /// <summary>Balayage haut → bas. Les acteurs au nord de la carte passent avant la rangée 0 ; ceux au sud, après la dernière frange.</summary>
    public static RowStep[] RowSteps(int mapHeight)
    {
        if (mapHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapHeight));
        }

        var steps = new RowStep[2 + (mapHeight * 3)];
        var index = 0;
        steps[index++] = new RowStep(RowStepKind.ActorsNorthOfMap, -1);
        for (var row = 0; row < mapHeight; row++)
        {
            steps[index++] = new RowStep(RowStepKind.BelowActors, row);
            steps[index++] = new RowStep(RowStepKind.ActorsOnRow, row);
            steps[index++] = new RowStep(RowStepKind.Fringe, row);
        }

        steps[index] = new RowStep(RowStepKind.ActorsSouthOfMap, mapHeight);
        return steps;
    }
}
