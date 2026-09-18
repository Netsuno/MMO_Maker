namespace Frog.Core.Enums;

/// <summary>Actions opérateur P9-1 (corps de <see cref="PacketId.ModerateRequest"/>).</summary>
public enum ModerationAction : byte
{
    Mute = 1,
    Unmute = 2,
    Kick = 3,
    Ban = 4,
    Unban = 5,
}
