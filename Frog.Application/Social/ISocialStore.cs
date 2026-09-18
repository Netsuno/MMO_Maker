using Frog.Core.Enums;

namespace Frog.Application.Social;

public sealed record SocialInviteRecord(
    Guid Id,
    SocialKind Kind,
    Guid SubjectId,
    Guid FromCharacterId,
    Guid ToCharacterId,
    DateTimeOffset ExpiresAtUtc,
    string Status);

public sealed record GuildRecord(
    Guid Id,
    string DisplayName,
    string NormalizedName,
    string Motd,
    DateTimeOffset CreatedAtUtc);

public sealed record GuildMemberRecord(
    Guid GuildId,
    Guid CharacterId,
    GuildRole Role,
    DateTimeOffset JoinedAtUtc);

public sealed record FriendshipRecord(
    Guid Id,
    Guid CharacterA,
    Guid CharacterB,
    Guid RequestedBy,
    string Status,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record BlockRecord(
    Guid BlockerCharacterId,
    Guid BlockedCharacterId,
    DateTimeOffset CreatedAtUtc);

public static class SocialInviteStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
}

public static class FriendshipStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
}

/// <summary>Persistance guildes / amis / blocage. Les groupes (party) restent en mémoire processus.</summary>
public interface ISocialStore
{
    Task<GuildRecord?> FindGuildByIdAsync(Guid guildId, CancellationToken cancellationToken = default);

    Task<GuildRecord?> FindGuildByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);

    Task<GuildMemberRecord?> FindGuildMemberAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuildMemberRecord>> ListGuildMembersAsync(Guid guildId, CancellationToken cancellationToken = default);

    Task<SocialInviteRecord?> FindPendingGuildInviteAsync(Guid inviteOrGuildId, Guid toCharacterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialInviteRecord>> ListPendingGuildInvitesForAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<int> CountPendingOutgoingAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<int> CountPendingIncomingAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> CreateGuildAsync(
        Guid leaderCharacterId,
        string displayName,
        string normalizedName,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> InviteToGuildAsync(
        Guid guildId,
        Guid fromCharacterId,
        Guid toCharacterId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> RespondGuildInviteAsync(
        Guid inviteId,
        Guid characterId,
        bool accept,
        int maxMembers,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> LeaveGuildAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> KickGuildMemberAsync(
        Guid actorCharacterId,
        Guid targetCharacterId,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> TransferGuildLeaderAsync(
        Guid actorCharacterId,
        Guid targetCharacterId,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> DisbandGuildAsync(
        Guid actorCharacterId,
        bool confirm,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> SetGuildMotdAsync(
        Guid actorCharacterId,
        string motd,
        CancellationToken cancellationToken = default);

    Task<FriendshipRecord?> FindFriendshipAsync(Guid a, Guid b, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FriendshipRecord>> ListFriendshipsAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> RequestFriendAsync(
        Guid fromCharacterId,
        Guid toCharacterId,
        DateTimeOffset expiresAtUtc,
        int maxFriends,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> RespondFriendAsync(
        Guid actorCharacterId,
        Guid otherCharacterId,
        bool accept,
        int maxFriends,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> RemoveFriendAsync(
        Guid actorCharacterId,
        Guid otherCharacterId,
        CancellationToken cancellationToken = default);

    Task<bool> IsBlockedAsync(Guid blockerCharacterId, Guid blockedCharacterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlockRecord>> ListBlocksAsync(Guid blockerCharacterId, CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> BlockAsync(
        Guid blockerCharacterId,
        Guid blockedCharacterId,
        int maxBlocks,
        CancellationToken cancellationToken = default);

    Task<SocialCommandPersistResult> UnblockAsync(
        Guid blockerCharacterId,
        Guid blockedCharacterId,
        CancellationToken cancellationToken = default);
}

public readonly record struct SocialCommandPersistResult(
    bool Success,
    string Message,
    Guid SubjectId,
    Guid OtherId);
