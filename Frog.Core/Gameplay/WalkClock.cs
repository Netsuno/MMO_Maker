using Frog.Core.Enums;

namespace Frog.Core.Gameplay;

/// <summary>
/// Shared client-only walk-sheet index (3 columns × 4 rows). No protocol field —
/// facing is inferred from the last move vector. Player, NPC, and monster reuse this.
/// </summary>
public static class WalkClock
{
    public const int FrameDurationMs = 140;
    public const int IdleColumn = 1;
    public const int SheetColumns = 3;
    public const int SheetRows = 4;
    public const int NativeCellPixels = 32;

    /// <summary>Walk cycle columns 0 → 1 → 2 → 1 (idle is the planted middle frame).</summary>
    public static int Column(bool walking, int elapsedMs)
    {
        if (!walking)
        {
            return IdleColumn;
        }

        var safe = elapsedMs < 0 ? 0 : elapsedMs;
        return ((safe / FrameDurationMs) % 4) switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            _ => 1,
        };
    }

    public static int Row(Direction facing) => facing switch
    {
        Direction.Left => 1,
        Direction.Right => 2,
        Direction.Up => 3,
        _ => 0,
    };

    public static Direction FacingFromVector(float vx, float vy, Direction fallback)
    {
        if (MathF.Abs(vx) < 0.0001f && MathF.Abs(vy) < 0.0001f)
        {
            return fallback;
        }

        if (MathF.Abs(vx) >= MathF.Abs(vy))
        {
            return vx < 0 ? Direction.Left : Direction.Right;
        }

        return vy < 0 ? Direction.Up : Direction.Down;
    }
}

/// <summary>World entity drawn by <c>MapViewRenderer</c> (NPC / monster). Default = south idle.</summary>
public enum WorldEntityKind : byte
{
    Player = 0,
    Npc = 1,
    Monster = 2,
}

/// <summary>NPC / monster pose passed into <c>MapViewRenderer</c>. Same sheet math as <see cref="PlayerSpritePose"/>.</summary>
public readonly record struct WorldSpritePose(
    Direction Facing,
    bool Walking,
    int ElapsedMs = 0,
    SpriteAction Action = SpriteAction.Walk,
    int ActionElapsedMs = 0)
{
    public static WorldSpritePose IdleDown { get; } = new(Direction.Down, false);

    public int SheetColumn => Action == SpriteAction.Walk
        ? WalkClock.Column(Walking, ElapsedMs)
        : ActionClock.Column(Action, ActionElapsedMs);

    public int SheetRow => WalkClock.Row(Facing);
}
