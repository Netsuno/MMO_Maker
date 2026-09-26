using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Pas d’une trajectoire, dans le modèle de jalons existant.
/// <see cref="Move"/> garde la tuile absolue. Les autres pas ne réécrivent pas l’architecture de déplacement.
/// </summary>
public static class MapEventRouteStepKinds
{
    public const string Move = "move";
    public const string Wait = "wait";
    public const string Down = "down";
    public const string Left = "left";
    public const string Right = "right";
    public const string Up = "up";

    public static readonly IReadOnlyList<string> All =
    [
        Move, Wait, Down, Left, Right, Up,
    ];

    public static bool TryNormalize(string? kind, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            normalized = Move;
            return true;
        }

        var trimmed = kind.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, trimmed, StringComparison.Ordinal))
            {
                normalized = known;
                return true;
            }
        }

        normalized = trimmed;
        return false;
    }

    public static string Canonical(string? kind) =>
        TryNormalize(kind, out var normalized) ? normalized : (kind ?? string.Empty).Trim();

    public static bool UsesAbsoluteTile(string? kind) =>
        TryNormalize(kind, out var normalized) && normalized == Move;

    public static bool TryDelta(string? kind, out int deltaX, out int deltaY)
    {
        deltaX = 0;
        deltaY = 0;
        if (!TryNormalize(kind, out var normalized))
        {
            return false;
        }

        TilePassageDirection? direction = normalized switch
        {
            Down => TilePassageDirection.South,
            Up => TilePassageDirection.North,
            Left => TilePassageDirection.West,
            Right => TilePassageDirection.East,
            _ => null,
        };

        if (direction is not TilePassageDirection cardinal)
        {
            return false;
        }

        (deltaX, deltaY) = TilePassage.Step(cardinal);
        return true;
    }

    public static bool TryValidate(MapEventRouteWaypoint? step, out string? error)
    {
        if (step is null)
        {
            error = "jalon manquant.";
            return false;
        }

        if (!TryNormalize(step.StepKind, out var kind))
        {
            error = $"type inconnu ({step.StepKind}).";
            return false;
        }

        if (step.WaitMs < 0 || step.WaitMs > MapEventRuntimeLimits.MaxWaitMs)
        {
            error = $"attente hors bornes 0–{MapEventRuntimeLimits.MaxWaitMs}.";
            return false;
        }

        if (UsesAbsoluteTile(kind) && (step.TileX < 0 || step.TileY < 0))
        {
            error = "coordonnées invalides.";
            return false;
        }

        error = null;
        return true;
    }
}
