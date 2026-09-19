namespace Frog.Core.Enums;

public enum SocialEventType : byte
{
    InviteReceived = 1,
    InviteExpired = 2,
    MemberJoined = 3,
    MemberLeft = 4,
    MemberKicked = 5,
    LeaderChanged = 6,
    Disbanded = 7,
    PresenceOnline = 8,
    PresenceOffline = 9,
    MotdChanged = 10,
    InviteDeclined = 11,
    InviteCancelled = 12
}
