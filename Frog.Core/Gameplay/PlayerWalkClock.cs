using Frog.Core.Enums;

namespace Frog.Core.Gameplay;

/// <summary>
/// Client-only walk-sheet index (3 columns × 4 rows). No protocol field —
/// facing is inferred from the last move vector. Thin wrapper over <see cref="WalkClock"/>.
/// </summary>
public static class PlayerWalkClock
{
    public const int FrameDurationMs = 140;
    public const int IdleColumn = 1;
    public const int SheetColumns = 3;
    public const int SheetRows = 4;
    public const int NativeCellPixels = 32;

    /// <summary>Walk cycle columns 0 → 1 → 2 → 1 (idle is the planted middle frame).</summary>
    public static int Column(bool walking, int elapsedMs) => WalkClock.Column(walking, elapsedMs);

    public static int Row(Direction facing) => WalkClock.Row(facing);

    public static Direction FacingFromVector(float vx, float vy, Direction fallback)
        => WalkClock.FacingFromVector(vx, vy, fallback);
}

/// <summary>
/// World-sprite pose passed into <c>MapViewRenderer</c>. Default = south idle.
/// <see cref="Action"/> selects the walk, attack, or death sheet; the row stays the facing.
/// </summary>
public readonly record struct PlayerSpritePose(
    Direction Facing,
    bool Walking,
    int ElapsedMs = 0,
    SpriteAction Action = SpriteAction.Walk,
    int ActionElapsedMs = 0)
{
    public static PlayerSpritePose IdleDown { get; } = new(Direction.Down, false);

    public int SheetColumn => Action == SpriteAction.Walk
        ? PlayerWalkClock.Column(Walking, ElapsedMs)
        : ActionClock.Column(Action, ActionElapsedMs);

    public int SheetRow => PlayerWalkClock.Row(Facing);
}
