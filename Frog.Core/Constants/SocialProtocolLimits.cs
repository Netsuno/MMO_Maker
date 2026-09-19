namespace Frog.Core.Constants;

public static class SocialProtocolLimits
{
    public const int MaxGuildNameUtf8Bytes = 64;
    public const int MaxGuildNameGraphemes = 32;
    public const int MaxMotdUtf8Bytes = 256;
    public const int MaxDisplayNameUtf8Bytes = 64;
    public const int MaxResultMessageUtf8Bytes = 256;
    public const int PartyMaxMembers = 5;
    public const int GuildMaxMembersDefault = 50;
    public const int MaxFriendsDefault = 100;
    public const int MaxBlocksDefault = 100;
    public const int MaxPendingOutgoing = 3;
    public const int MaxPendingIncoming = 8;
    public const int PartyInviteSecondsDefault = 60;
    public const int GuildInviteSecondsDefault = 120;
    public const int FriendRequestDaysDefault = 7;
    public const int ReinviteCooldownSeconds = 30;
    public const int InviteRatePerMinute = 10;
    public const int TradeMaxStacksPerSide = 8;
    public const int TradeInviteSecondsDefault = 60;
    public const int TradeIdleSecondsDefault = 120;
}
