namespace Frog.Core.Gameplay;

/// <summary>
/// Client-only attack / death sheet index. Same 3×4 grid as <see cref="WalkClock"/>
/// (columns = frames, rows = facing). No protocol field.
/// </summary>
public enum SpriteAction : byte
{
    Walk = 0,
    Attack = 1,
    Death = 2,
}

/// <summary>
/// One-shot attack (returns to walk) and a death that holds the last frame.
/// Durations are client clocks only — Hello stays 11.
/// </summary>
public static class ActionClock
{
    public const int AttackFrameMs = 90;

    public const int AttackFrames = 3;

    public const int AttackDurationMs = AttackFrameMs * AttackFrames;

    public const int DeathFrameMs = 140;

    public const int DeathFrames = 3;

    public static int Column(SpriteAction action, int elapsedMs)
    {
        var safe = elapsedMs < 0 ? 0 : elapsedMs;
        if (action == SpriteAction.Death)
        {
            var frame = safe / DeathFrameMs;
            return frame >= DeathFrames ? DeathFrames - 1 : frame;
        }

        var attack = safe / AttackFrameMs;
        return attack >= AttackFrames ? AttackFrames - 1 : attack;
    }

    public static bool AttackFinished(int elapsedMs) => elapsedMs >= AttackDurationMs;

    /// <summary>The corpse frame has been reached; further ticks do not change the cell.</summary>
    public static bool DeathSettled(int elapsedMs) => elapsedMs >= DeathFrameMs * DeathFrames;

    /// <summary>
    /// Résultat mêlée reçu par la cible : le champ nom est l'attaquant
    /// (« Subi une attaque melee. » / « Subi une attaque à distance. »).
    /// </summary>
    public static bool IsIncomingAttack(string? message)
        => !string.IsNullOrEmpty(message)
           && message.Contains("Subi une attaque", StringComparison.OrdinalIgnoreCase);
}
