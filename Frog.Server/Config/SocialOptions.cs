using Frog.Core.Constants;

namespace Frog.Server.Config;

public sealed class SocialOptions
{
    public int PartyMaxMembers { get; init; } = SocialProtocolLimits.PartyMaxMembers;

    public int GuildMaxMembers { get; init; } = SocialProtocolLimits.GuildMaxMembersDefault;

    public int PartyInviteSeconds { get; init; } = SocialProtocolLimits.PartyInviteSecondsDefault;

    public int GuildInviteSeconds { get; init; } = SocialProtocolLimits.GuildInviteSecondsDefault;

    public int FriendRequestDays { get; init; } = SocialProtocolLimits.FriendRequestDaysDefault;

    public int MaxFriends { get; init; } = SocialProtocolLimits.MaxFriendsDefault;

    public int MaxBlocks { get; init; } = SocialProtocolLimits.MaxBlocksDefault;

    public int MaxPendingOutgoing { get; init; } = SocialProtocolLimits.MaxPendingOutgoing;

    public int MaxPendingIncoming { get; init; } = SocialProtocolLimits.MaxPendingIncoming;

    public int ReinviteCooldownSeconds { get; init; } = SocialProtocolLimits.ReinviteCooldownSeconds;

    public int InviteRatePerMinute { get; init; } = SocialProtocolLimits.InviteRatePerMinute;
}
