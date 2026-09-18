using Frog.Application.Social;
using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Server.Social;

/// <summary>Store social en mémoire (tests / AllowInMemoryFallback). Pas de groupes — ceux-ci sont dans <see cref="PartyRoster"/>.</summary>
public sealed class InMemorySocialStore : ISocialStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, GuildRecord> _guilds = new();
    private readonly Dictionary<string, Guid> _guildByName = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, GuildMemberRecord> _memberByCharacter = new();
    private readonly Dictionary<Guid, List<GuildMemberRecord>> _membersByGuild = new();
    private readonly Dictionary<Guid, SocialInviteRecord> _guildInvites = new();
    private readonly Dictionary<Guid, FriendshipRecord> _friendships = new();
    private readonly HashSet<(Guid Blocker, Guid Blocked)> _blocks = new();

    public Task<GuildRecord?> FindGuildByIdAsync(Guid guildId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _guilds.TryGetValue(guildId, out var g);
            return Task.FromResult(g);
        }
    }

    public Task<GuildRecord?> FindGuildByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_guildByName.TryGetValue(normalizedName, out var id) && _guilds.TryGetValue(id, out var g))
            {
                return Task.FromResult<GuildRecord?>(g);
            }

            return Task.FromResult<GuildRecord?>(null);
        }
    }

    public Task<GuildMemberRecord?> FindGuildMemberAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _memberByCharacter.TryGetValue(characterId, out var m);
            return Task.FromResult(m);
        }
    }

    public Task<IReadOnlyList<GuildMemberRecord>> ListGuildMembersAsync(Guid guildId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_membersByGuild.TryGetValue(guildId, out var list))
            {
                return Task.FromResult<IReadOnlyList<GuildMemberRecord>>(Array.Empty<GuildMemberRecord>());
            }

            return Task.FromResult<IReadOnlyList<GuildMemberRecord>>(list.ToArray());
        }
    }

    public Task<SocialInviteRecord?> FindPendingGuildInviteAsync(Guid inviteOrGuildId, Guid toCharacterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            foreach (var inv in _guildInvites.Values)
            {
                if (inv.ToCharacterId == toCharacterId
                    && inv.Status == SocialInviteStatuses.Pending
                    && (inv.Id == inviteOrGuildId || inv.SubjectId == inviteOrGuildId))
                {
                    return Task.FromResult<SocialInviteRecord?>(inv);
                }
            }

            return Task.FromResult<SocialInviteRecord?>(null);
        }
    }

    public Task<IReadOnlyList<SocialInviteRecord>> ListPendingGuildInvitesForAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            var list = _guildInvites.Values
                .Where(i => i.Status == SocialInviteStatuses.Pending
                            && (i.ToCharacterId == characterId || i.FromCharacterId == characterId))
                .ToArray();
            return Task.FromResult<IReadOnlyList<SocialInviteRecord>>(list);
        }
    }

    public Task<int> CountPendingOutgoingAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            var n = _guildInvites.Values.Count(i =>
                i.FromCharacterId == characterId && i.Status == SocialInviteStatuses.Pending);
            n += _friendships.Values.Count(f =>
                f.RequestedBy == characterId && f.Status == FriendshipStatuses.Pending);
            return Task.FromResult(n);
        }
    }

    public Task<int> CountPendingIncomingAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            var n = _guildInvites.Values.Count(i =>
                i.ToCharacterId == characterId && i.Status == SocialInviteStatuses.Pending);
            n += _friendships.Values.Count(f =>
                f.Status == FriendshipStatuses.Pending && Other(f, characterId) != Guid.Empty && f.RequestedBy != characterId
                && (f.CharacterA == characterId || f.CharacterB == characterId));
            return Task.FromResult(n);
        }
    }

    public Task<SocialCommandPersistResult> CreateGuildAsync(
        Guid leaderCharacterId,
        string displayName,
        string normalizedName,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_memberByCharacter.ContainsKey(leaderCharacterId))
            {
                return Task.FromResult(Fail("Deja dans une guilde."));
            }

            if (_guildByName.ContainsKey(normalizedName))
            {
                return Task.FromResult(Fail("Ce nom de guilde est deja pris."));
            }

            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var guild = new GuildRecord(id, displayName, normalizedName, string.Empty, now);
            _guilds[id] = guild;
            _guildByName[normalizedName] = id;
            var member = new GuildMemberRecord(id, leaderCharacterId, GuildRole.Leader, now);
            _memberByCharacter[leaderCharacterId] = member;
            _membersByGuild[id] = [member];
            return Task.FromResult(new SocialCommandPersistResult(true, "Guilde creee.", id, leaderCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> InviteToGuildAsync(
        Guid guildId,
        Guid fromCharacterId,
        Guid toCharacterId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            if (!_memberByCharacter.TryGetValue(fromCharacterId, out var from) || from.GuildId != guildId)
            {
                return Task.FromResult(Fail("Vous n'appartenez pas a cette guilde."));
            }

            if (from.Role is not (GuildRole.Leader or GuildRole.Officer))
            {
                return Task.FromResult(Fail("Permission insuffisante."));
            }

            if (_memberByCharacter.ContainsKey(toCharacterId))
            {
                return Task.FromResult(Fail("Ce personnage est deja dans une guilde."));
            }

            foreach (var existing in _guildInvites.Values)
            {
                if (existing.Status == SocialInviteStatuses.Pending
                    && existing.SubjectId == guildId
                    && existing.ToCharacterId == toCharacterId)
                {
                    return Task.FromResult(Fail("Invitation deja en attente."));
                }
            }

            var invite = new SocialInviteRecord(
                Guid.NewGuid(),
                SocialKind.Guild,
                guildId,
                fromCharacterId,
                toCharacterId,
                expiresAtUtc,
                SocialInviteStatuses.Pending);
            _guildInvites[invite.Id] = invite;
            return Task.FromResult(new SocialCommandPersistResult(true, "Invitation envoyee.", guildId, toCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> RespondGuildInviteAsync(
        Guid inviteId,
        Guid characterId,
        bool accept,
        int maxMembers,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireLocked(DateTimeOffset.UtcNow);
            if (!_guildInvites.TryGetValue(inviteId, out var invite)
                && !TryFindPendingByGuild(inviteId, characterId, out invite))
            {
                return Task.FromResult(Fail("Invitation invalide ou expiree."));
            }

            if (invite.ToCharacterId != characterId || invite.Status != SocialInviteStatuses.Pending)
            {
                return Task.FromResult(Fail("Invitation deja traitee."));
            }

            if (!accept)
            {
                _guildInvites[invite.Id] = invite with { Status = SocialInviteStatuses.Declined };
                return Task.FromResult(new SocialCommandPersistResult(true, "Invitation refusee.", invite.SubjectId, characterId));
            }

            if (_memberByCharacter.ContainsKey(characterId))
            {
                _guildInvites[invite.Id] = invite with { Status = SocialInviteStatuses.Declined };
                return Task.FromResult(Fail("Deja dans une guilde."));
            }

            if (!_membersByGuild.TryGetValue(invite.SubjectId, out var members) || !_guilds.ContainsKey(invite.SubjectId))
            {
                _guildInvites[invite.Id] = invite with { Status = SocialInviteStatuses.Expired };
                return Task.FromResult(Fail("Guilde introuvable."));
            }

            if (members.Count >= maxMembers)
            {
                return Task.FromResult(Fail("Guilde complete."));
            }

            var now = DateTimeOffset.UtcNow;
            var member = new GuildMemberRecord(invite.SubjectId, characterId, GuildRole.Member, now);
            members.Add(member);
            _memberByCharacter[characterId] = member;
            _guildInvites[invite.Id] = invite with { Status = SocialInviteStatuses.Accepted };
            CancelOtherGuildInvitesLocked(characterId, invite.Id);
            return Task.FromResult(new SocialCommandPersistResult(true, "Vous avez rejoint la guilde.", invite.SubjectId, characterId));
        }
    }

    public Task<SocialCommandPersistResult> LeaveGuildAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_memberByCharacter.TryGetValue(characterId, out var member))
            {
                return Task.FromResult(Fail("Vous n'etes pas dans une guilde."));
            }

            var members = _membersByGuild[member.GuildId];
            if (member.Role == GuildRole.Leader && members.Count > 1)
            {
                return Task.FromResult(Fail("Transferez la direction avant de quitter."));
            }

            if (member.Role == GuildRole.Leader && members.Count == 1)
            {
                return Task.FromResult(Fail("Utilisez la dissolution pour fermer la guilde."));
            }

            RemoveMemberLocked(member.GuildId, characterId);
            return Task.FromResult(new SocialCommandPersistResult(true, "Vous avez quitte la guilde.", member.GuildId, characterId));
        }
    }

    public Task<SocialCommandPersistResult> KickGuildMemberAsync(
        Guid actorCharacterId,
        Guid targetCharacterId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_memberByCharacter.TryGetValue(actorCharacterId, out var actor)
                || !_memberByCharacter.TryGetValue(targetCharacterId, out var target)
                || actor.GuildId != target.GuildId)
            {
                return Task.FromResult(Fail("Cible introuvable dans la guilde."));
            }

            if (actorCharacterId == targetCharacterId)
            {
                return Task.FromResult(Fail("Impossible de s'exclure soi-meme."));
            }

            if (target.Role == GuildRole.Leader)
            {
                return Task.FromResult(Fail("Impossible d'exclure le chef."));
            }

            if (actor.Role == GuildRole.Member)
            {
                return Task.FromResult(Fail("Permission insuffisante."));
            }

            if (actor.Role == GuildRole.Officer && target.Role != GuildRole.Member)
            {
                return Task.FromResult(Fail("Permission insuffisante."));
            }

            RemoveMemberLocked(actor.GuildId, targetCharacterId);
            return Task.FromResult(new SocialCommandPersistResult(true, "Membre exclu.", actor.GuildId, targetCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> TransferGuildLeaderAsync(
        Guid actorCharacterId,
        Guid targetCharacterId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_memberByCharacter.TryGetValue(actorCharacterId, out var actor)
                || actor.Role != GuildRole.Leader)
            {
                return Task.FromResult(Fail("Seul le chef peut transferer la direction."));
            }

            if (!_memberByCharacter.TryGetValue(targetCharacterId, out var target)
                || target.GuildId != actor.GuildId)
            {
                return Task.FromResult(Fail("Cible introuvable dans la guilde."));
            }

            if (actorCharacterId == targetCharacterId)
            {
                return Task.FromResult(Fail("Cible invalide."));
            }

            ReplaceMemberLocked(actor with { Role = GuildRole.Officer });
            ReplaceMemberLocked(target with { Role = GuildRole.Leader });
            return Task.FromResult(new SocialCommandPersistResult(true, "Direction transferee.", actor.GuildId, targetCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> DisbandGuildAsync(
        Guid actorCharacterId,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!confirm)
            {
                return Task.FromResult(Fail("Confirmation requise."));
            }

            if (!_memberByCharacter.TryGetValue(actorCharacterId, out var actor))
            {
                return Task.FromResult(Fail("Vous n'etes pas dans une guilde."));
            }

            var members = _membersByGuild[actor.GuildId];
            if (actor.Role != GuildRole.Leader)
            {
                return Task.FromResult(Fail("Seul le chef peut dissoudre la guilde."));
            }

            if (members.Count > 1)
            {
                return Task.FromResult(Fail("Excluez ou faites partir les autres membres d'abord."));
            }

            var guildId = actor.GuildId;
            var guild = _guilds[guildId];
            foreach (var m in members.ToArray())
            {
                _memberByCharacter.Remove(m.CharacterId);
            }

            _membersByGuild.Remove(guildId);
            _guilds.Remove(guildId);
            _guildByName.Remove(guild.NormalizedName);
            foreach (var inv in _guildInvites.Values.Where(i => i.SubjectId == guildId).ToArray())
            {
                _guildInvites[inv.Id] = inv with { Status = SocialInviteStatuses.Cancelled };
            }

            return Task.FromResult(new SocialCommandPersistResult(true, "Guilde dissoute.", guildId, actorCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> SetGuildMotdAsync(
        Guid actorCharacterId,
        string motd,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_memberByCharacter.TryGetValue(actorCharacterId, out var actor)
                || actor.Role is not (GuildRole.Leader or GuildRole.Officer))
            {
                return Task.FromResult(Fail("Permission insuffisante."));
            }

            var g = _guilds[actor.GuildId];
            _guilds[actor.GuildId] = g with { Motd = motd };
            return Task.FromResult(new SocialCommandPersistResult(true, "Message de guilde mis a jour.", actor.GuildId, actorCharacterId));
        }
    }

    public Task<FriendshipRecord?> FindFriendshipAsync(Guid a, Guid b, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireFriendsLocked(DateTimeOffset.UtcNow);
            return Task.FromResult(FindFriendshipLocked(a, b));
        }
    }

    public Task<IReadOnlyList<FriendshipRecord>> ListFriendshipsAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireFriendsLocked(DateTimeOffset.UtcNow);
            var list = _friendships.Values
                .Where(f => f.CharacterA == characterId || f.CharacterB == characterId)
                .ToArray();
            return Task.FromResult<IReadOnlyList<FriendshipRecord>>(list);
        }
    }

    public Task<SocialCommandPersistResult> RequestFriendAsync(
        Guid fromCharacterId,
        Guid toCharacterId,
        DateTimeOffset expiresAtUtc,
        int maxFriends,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireFriendsLocked(DateTimeOffset.UtcNow);
            if (fromCharacterId == toCharacterId)
            {
                return Task.FromResult(Fail("Cible invalide."));
            }

            if (CountAcceptedFriendsLocked(fromCharacterId) >= maxFriends)
            {
                return Task.FromResult(Fail("Liste d'amis complete."));
            }

            var existing = FindFriendshipLocked(fromCharacterId, toCharacterId);
            if (existing is not null)
            {
                if (existing.Status == FriendshipStatuses.Accepted)
                {
                    return Task.FromResult(Fail("Deja amis."));
                }

                if (existing.Status == FriendshipStatuses.Pending)
                {
                    return Task.FromResult(Fail("Demande deja en attente."));
                }

                _friendships.Remove(existing.Id);
            }

            var id = Guid.NewGuid();
            var rec = new FriendshipRecord(
                id,
                Low(fromCharacterId, toCharacterId),
                High(fromCharacterId, toCharacterId),
                fromCharacterId,
                FriendshipStatuses.Pending,
                expiresAtUtc,
                DateTimeOffset.UtcNow);
            _friendships[id] = rec;
            return Task.FromResult(new SocialCommandPersistResult(true, "Demande d'ami envoyee.", id, toCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> RespondFriendAsync(
        Guid actorCharacterId,
        Guid otherCharacterId,
        bool accept,
        int maxFriends,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExpireFriendsLocked(DateTimeOffset.UtcNow);
            var existing = FindFriendshipLocked(actorCharacterId, otherCharacterId);
            if (existing is null || existing.Status != FriendshipStatuses.Pending)
            {
                return Task.FromResult(Fail("Demande invalide ou expiree."));
            }

            if (existing.RequestedBy == actorCharacterId)
            {
                return Task.FromResult(Fail("Vous ne pouvez pas accepter votre propre demande."));
            }

            if (!accept)
            {
                _friendships[existing.Id] = existing with { Status = FriendshipStatuses.Declined, ExpiresAtUtc = null };
                return Task.FromResult(new SocialCommandPersistResult(true, "Demande refusee.", existing.Id, otherCharacterId));
            }

            if (CountAcceptedFriendsLocked(actorCharacterId) >= maxFriends
                || CountAcceptedFriendsLocked(otherCharacterId) >= maxFriends)
            {
                return Task.FromResult(Fail("Liste d'amis complete."));
            }

            _friendships[existing.Id] = existing with { Status = FriendshipStatuses.Accepted, ExpiresAtUtc = null };
            return Task.FromResult(new SocialCommandPersistResult(true, "Ami ajoute.", existing.Id, otherCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> RemoveFriendAsync(
        Guid actorCharacterId,
        Guid otherCharacterId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var existing = FindFriendshipLocked(actorCharacterId, otherCharacterId);
            if (existing is null)
            {
                return Task.FromResult(Fail("Relation introuvable."));
            }

            _friendships.Remove(existing.Id);
            return Task.FromResult(new SocialCommandPersistResult(true, "Relation supprimee.", existing.Id, otherCharacterId));
        }
    }

    public Task<bool> IsBlockedAsync(Guid blockerCharacterId, Guid blockedCharacterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_blocks.Contains((blockerCharacterId, blockedCharacterId)));
        }
    }

    public Task<IReadOnlyList<BlockRecord>> ListBlocksAsync(Guid blockerCharacterId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var list = _blocks
                .Where(b => b.Blocker == blockerCharacterId)
                .Select(b => new BlockRecord(b.Blocker, b.Blocked, DateTimeOffset.UtcNow))
                .ToArray();
            return Task.FromResult<IReadOnlyList<BlockRecord>>(list);
        }
    }

    public Task<SocialCommandPersistResult> BlockAsync(
        Guid blockerCharacterId,
        Guid blockedCharacterId,
        int maxBlocks,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (blockerCharacterId == blockedCharacterId)
            {
                return Task.FromResult(Fail("Cible invalide."));
            }

            if (_blocks.Count(b => b.Blocker == blockerCharacterId) >= maxBlocks)
            {
                return Task.FromResult(Fail("Liste de blocage complete."));
            }

            _blocks.Add((blockerCharacterId, blockedCharacterId));
            var friendship = FindFriendshipLocked(blockerCharacterId, blockedCharacterId);
            if (friendship is not null)
            {
                _friendships.Remove(friendship.Id);
            }

            return Task.FromResult(new SocialCommandPersistResult(true, "Personnage bloque.", Guid.Empty, blockedCharacterId));
        }
    }

    public Task<SocialCommandPersistResult> UnblockAsync(
        Guid blockerCharacterId,
        Guid blockedCharacterId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _blocks.Remove((blockerCharacterId, blockedCharacterId));
            return Task.FromResult(new SocialCommandPersistResult(true, "Blocage leve.", Guid.Empty, blockedCharacterId));
        }
    }

    private static SocialCommandPersistResult Fail(string message)
        => new(false, message, Guid.Empty, Guid.Empty);

    private bool TryFindPendingByGuild(Guid guildId, Guid toCharacterId, out SocialInviteRecord invite)
    {
        foreach (var inv in _guildInvites.Values)
        {
            if (inv.SubjectId == guildId
                && inv.ToCharacterId == toCharacterId
                && inv.Status == SocialInviteStatuses.Pending)
            {
                invite = inv;
                return true;
            }
        }

        invite = null!;
        return false;
    }

    private void CancelOtherGuildInvitesLocked(Guid characterId, Guid keepId)
    {
        foreach (var inv in _guildInvites.Values.ToArray())
        {
            if (inv.Id != keepId
                && inv.ToCharacterId == characterId
                && inv.Status == SocialInviteStatuses.Pending)
            {
                _guildInvites[inv.Id] = inv with { Status = SocialInviteStatuses.Cancelled };
            }
        }
    }

    private void RemoveMemberLocked(Guid guildId, Guid characterId)
    {
        _memberByCharacter.Remove(characterId);
        if (_membersByGuild.TryGetValue(guildId, out var list))
        {
            list.RemoveAll(m => m.CharacterId == characterId);
        }
    }

    private void ReplaceMemberLocked(GuildMemberRecord member)
    {
        _memberByCharacter[member.CharacterId] = member;
        var list = _membersByGuild[member.GuildId];
        var idx = list.FindIndex(m => m.CharacterId == member.CharacterId);
        if (idx >= 0)
        {
            list[idx] = member;
        }
    }

    private void ExpireLocked(DateTimeOffset now)
    {
        foreach (var inv in _guildInvites.Values.ToArray())
        {
            if (inv.Status == SocialInviteStatuses.Pending && inv.ExpiresAtUtc <= now)
            {
                _guildInvites[inv.Id] = inv with { Status = SocialInviteStatuses.Expired };
            }
        }
    }

    private void ExpireFriendsLocked(DateTimeOffset now)
    {
        foreach (var f in _friendships.Values.ToArray())
        {
            if (f.Status == FriendshipStatuses.Pending && f.ExpiresAtUtc is { } exp && exp <= now)
            {
                _friendships.Remove(f.Id);
            }
        }
    }

    private FriendshipRecord? FindFriendshipLocked(Guid a, Guid b)
    {
        var low = Low(a, b);
        var high = High(a, b);
        return _friendships.Values.FirstOrDefault(f => f.CharacterA == low && f.CharacterB == high);
    }

    private int CountAcceptedFriendsLocked(Guid characterId)
        => _friendships.Values.Count(f =>
            f.Status == FriendshipStatuses.Accepted
            && (f.CharacterA == characterId || f.CharacterB == characterId));

    private static Guid Other(FriendshipRecord f, Guid me)
        => f.CharacterA == me ? f.CharacterB : f.CharacterA;

    private static Guid Low(Guid a, Guid b) => a.CompareTo(b) <= 0 ? a : b;

    private static Guid High(Guid a, Guid b) => a.CompareTo(b) <= 0 ? b : a;
}
