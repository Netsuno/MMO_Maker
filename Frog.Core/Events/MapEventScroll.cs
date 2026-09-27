using System.Globalization;
using Frog.Core.Constants;

namespace Frog.Core.Events;

/// <summary>
/// Défilement de carte (commande <c>scroll_map</c>), style VX : direction, distance en tuiles, vitesse 1–6.
/// Le joueur ne change pas de tuile. Hello 11, pas de nouvel opcode.
/// Une tuile dure 512 ms à la vitesse 1, puis moitié à chaque cran (16 ms à la vitesse 6).
/// </summary>
public static class MapEventScroll
{
    public const string DirectionUp = "up";

    public const string DirectionDown = "down";

    public const string DirectionLeft = "left";

    public const string DirectionRight = "right";

    public const int MinDistance = 1;

    public const int MaxDistance = 100;

    public const int DefaultDistance = 1;

    public const int MinSpeed = 1;

    public const int MaxSpeed = 6;

    public const int DefaultSpeed = 4;

    public static readonly IReadOnlyList<string> Directions =
    [
        DirectionUp,
        DirectionDown,
        DirectionLeft,
        DirectionRight,
    ];

    public static bool IsDirection(string? value) =>
        value is DirectionUp or DirectionDown or DirectionLeft or DirectionRight;

    public static bool IsDistance(int value) => value is >= MinDistance and <= MaxDistance;

    public static bool IsSpeed(int value) => value is >= MinSpeed and <= MaxSpeed;

    /// <summary>Millisecondes pour traverser une tuile. Vitesse 1 = 512, vitesse 6 = 16.</summary>
    public static int MillisecondsPerTile(int speed) =>
        IsSpeed(speed) ? 512 >> (speed - 1) : 0;

    public static int DurationMs(int distance, int speed) =>
        IsDistance(distance) && IsSpeed(speed) ? distance * MillisecondsPerTile(speed) : 0;

    public static string FormatLine(MapEventScrollOp op)
    {
        if (!IsDirection(op.Direction) || !IsDistance(op.Distance) || !IsSpeed(op.Speed))
        {
            return string.Empty;
        }

        return FormattableString.Invariant($"scroll:{op.Direction}:{op.Distance}:{op.Speed}");
    }

    public static bool TryParseLine(string line, out MapEventScrollOp op)
    {
        op = MapEventScrollOp.Create(DirectionDown, DefaultDistance, DefaultSpeed);
        if (!line.StartsWith("scroll:", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = line.Split(':');
        if (parts.Length != 4
            || parts[0] != "scroll"
            || !IsDirection(parts[1])
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var distance)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var speed)
            || !IsDistance(distance)
            || !IsSpeed(speed))
        {
            return false;
        }

        op = MapEventScrollOp.Create(parts[1], distance, speed);
        return true;
    }
}

/// <summary>Une commande de défilement. La durée se déduit de la distance et de la vitesse.</summary>
public sealed record MapEventScrollOp
{
    public string Direction { get; init; } = MapEventScroll.DirectionDown;

    public int Distance { get; init; }

    public int Speed { get; init; }

    public int DurationMs => MapEventScroll.DurationMs(Distance, Speed);

    public static MapEventScrollOp Create(string direction, int distance, int speed) =>
        new()
        {
            Direction = direction,
            Distance = distance,
            Speed = speed,
        };
}

/// <summary>
/// Décalage caméra en pixels, ajouté au focus. À la fin, le décalage reste.
/// La taille de tuile passée au calcul est celle affichée (48 pour une carte TileAsset).
/// </summary>
public static class MapEventScrollPlayback
{
    public static (int X, int Y) DeltaPixels(MapEventScrollOp op, int tileSize)
    {
        if (tileSize <= 0
            || !MapEventScroll.IsDirection(op.Direction)
            || !MapEventScroll.IsDistance(op.Distance))
        {
            return (0, 0);
        }

        var pixels = op.Distance * tileSize;
        return op.Direction switch
        {
            MapEventScroll.DirectionUp => (0, -pixels),
            MapEventScroll.DirectionDown => (0, pixels),
            MapEventScroll.DirectionLeft => (-pixels, 0),
            MapEventScroll.DirectionRight => (pixels, 0),
            _ => (0, 0),
        };
    }

    public static (int X, int Y) Sample(MapEventScrollOp op, int fromX, int fromY, int elapsedMs, int tileSize)
    {
        var (dx, dy) = DeltaPixels(op, tileSize);
        var endX = fromX + dx;
        var endY = fromY + dy;
        var duration = op.DurationMs;
        if (duration <= 0 || elapsedMs >= duration)
        {
            return (endX, endY);
        }

        if (elapsedMs <= 0)
        {
            return (fromX, fromY);
        }

        var t = elapsedMs / (double)duration;
        return (MapEventScreenPlayback.Lerp(fromX, endX, t), MapEventScreenPlayback.Lerp(fromY, endY, t));
    }
}
