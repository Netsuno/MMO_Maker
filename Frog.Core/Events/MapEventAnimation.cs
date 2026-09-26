using System.Globalization;
using Frog.Core.Constants;

namespace Frog.Core.Events;

/// <summary>
/// Animation de carte (commande <c>show_animation</c>), cible événement ou joueur.
/// Trois animations intégrées, sans planche ni combat. Hello 11, pas de nouvel opcode.
/// </summary>
public static class MapEventAnimation
{
    public const int SparkId = 1;

    public const int HealId = 2;

    public const int HitId = 3;

    public const int MinId = SparkId;

    public const int MaxId = HitId;

    public const int DefaultId = SparkId;

    public const string TargetEvent = "event";

    public const string TargetPlayer = "player";

    public const int MinTile = 0;

    public const int MaxTile = GameLimits.MaxMapWidth - 1;

    public static readonly IReadOnlyList<int> Ids = new[] { SparkId, HealId, HitId };

    public static readonly IReadOnlyList<string> Targets = new[] { TargetEvent, TargetPlayer };

    public static bool IsId(int value) => value is >= MinId and <= MaxId;

    public static bool IsTarget(string? value) => value is TargetEvent or TargetPlayer;

    public static bool IsTile(int value) => value is >= MinTile and <= MaxTile;

    public static int ClampTile(int value) => Math.Clamp(value, MinTile, MaxTile);

    public static (int Red, int Green, int Blue) ColorOf(int animationId) => animationId switch
    {
        HealId => (40, 220, 90),
        HitId => (220, 40, 48),
        _ => (255, 220, 40),
    };

    public static string FormatLine(MapEventAnimationOp op)
    {
        if (!op.IsPlaced
            || !IsId(op.AnimationId)
            || !IsTarget(op.Target)
            || !MapEventScreen.IsDuration(op.DurationMs))
        {
            return string.Empty;
        }

        return FormattableString.Invariant(
            $"anim:{op.AnimationId}:{op.Target}:{op.TileX}:{op.TileY}:{op.DurationMs}");
    }

    public static bool TryParseLine(string line, out MapEventAnimationOp op)
    {
        op = MapEventAnimationOp.Create(DefaultId, TargetEvent, 0);
        if (!line.StartsWith("anim:", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = line.Split(':');
        if (parts.Length != 6
            || parts[0] != "anim"
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var animationId)
            || !IsId(animationId)
            || !IsTarget(parts[2])
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var tileX)
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var tileY)
            || !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var durationMs)
            || !IsTile(tileX)
            || !IsTile(tileY)
            || !MapEventScreen.IsDuration(durationMs))
        {
            return false;
        }

        op = MapEventAnimationOp.Create(animationId, parts[2], durationMs).At(tileX, tileY);
        return true;
    }
}

/// <summary>Commande jouée sur une tuile. <see cref="TileX"/> vaut -1 tant que la cible n'est pas résolue.</summary>
public sealed record MapEventAnimationOp
{
    public int AnimationId { get; init; }

    public string Target { get; init; } = MapEventAnimation.TargetEvent;

    public int DurationMs { get; init; }

    public int TileX { get; init; } = -1;

    public int TileY { get; init; } = -1;

    public bool TargetsPlayer => Target == MapEventAnimation.TargetPlayer;

    public bool IsPlaced => MapEventAnimation.IsTile(TileX) && MapEventAnimation.IsTile(TileY);

    public MapEventAnimationOp At(int tileX, int tileY) =>
        this with
        {
            TileX = MapEventAnimation.ClampTile(tileX),
            TileY = MapEventAnimation.ClampTile(tileY),
        };

    public static MapEventAnimationOp Create(int animationId, string target, int durationMs) =>
        new()
        {
            AnimationId = animationId,
            Target = target,
            DurationMs = durationMs,
        };
}

/// <summary>Pose la tuile de l'événement déclencheur ou du joueur au moment où le résultat part.</summary>
public static class MapEventAnimationAnchor
{
    public static void Resolve(
        IList<MapEventAnimationOp>? ops,
        int playerTileX,
        int playerTileY,
        int eventTileX,
        int eventTileY)
    {
        if (ops is null || ops.Count == 0)
        {
            return;
        }

        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            ops[i] = op.TargetsPlayer
                ? op.At(playerTileX, playerTileY)
                : op.At(eventTileX, eventTileY);
        }
    }
}

/// <summary>Cadre dessiné au centre de la tuile cible. Rien ne reste à la fin.</summary>
public readonly record struct MapEventAnimationFrame(
    bool Visible,
    int Red,
    int Green,
    int Blue,
    int Opacity,
    int RadiusPx)
{
    public static MapEventAnimationFrame Hidden => default;
}

public static class MapEventAnimationPlayback
{
    public static MapEventAnimationFrame Sample(MapEventAnimationOp op, int elapsedMs, int tileSize)
    {
        if (!op.IsPlaced
            || tileSize <= 0
            || op.DurationMs <= 0
            || elapsedMs < 0
            || elapsedMs >= op.DurationMs)
        {
            return MapEventAnimationFrame.Hidden;
        }

        var t = elapsedMs / (double)op.DurationMs;
        var (red, green, blue) = MapEventAnimation.ColorOf(op.AnimationId);
        var radius = MapEventScreenPlayback.Lerp(Math.Max(1, tileSize / 4), Math.Max(1, tileSize / 2), t);
        return new MapEventAnimationFrame(
            true,
            red,
            green,
            blue,
            MapEventScreenPlayback.Lerp(255, 0, t),
            radius);
    }
}
