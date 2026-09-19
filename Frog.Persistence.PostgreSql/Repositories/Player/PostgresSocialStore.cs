using Frog.Application.Social;
using Frog.Core.Enums;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresSocialStore : ISocialStore
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresSocialStore(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<GuildRecord?> FindGuildByIdAsync(Guid guildId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var e = await db.PlayerGuilds.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == guildId, ct)
                .ConfigureAwait(false);
            return e is null ? null : MapGuild(e);
        }, cancellationToken);

    public Task<GuildRecord?> FindGuildByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var e = await db.PlayerGuilds.AsNoTracking()
                .FirstOrDefaultAsync(g => g.NormalizedName == normalizedName, ct)
                .ConfigureAwait(false);
            return e is null ? null : MapGuild(e);
        }, cancellationToken);

    public Task<GuildMemberRecord?> FindGuildMemberAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var e = await db.PlayerGuildMembers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.CharacterId == characterId, ct)
                .ConfigureAwait(false);
            return e is null ? null : MapMember(e);
        }, cancellationToken);

    public Task<IReadOnlyList<GuildMemberRecord>> ListGuildMembersAsync(
        Guid guildId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var list = await db.PlayerGuildMembers.AsNoTracking()
                .Where(m => m.GuildId == guildId)
                .ToArrayAsync(ct)
                .ConfigureAwait(false);
            return (IReadOnlyList<GuildMemberRecord>)list.Select(MapMember).ToArray();
        }, cancellationToken);

    public Task<SocialInviteRecord?> FindPendingGuildInviteAsync(
        Guid inviteOrGuildId,
        Guid toCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            await ExpireInvitesAsync(db, now, ct).ConfigureAwait(false);
            var e = await db.PlayerGuildInvites
                .FirstOrDefaultAsync(
                    i => i.ToCharacterId == toCharacterId
                         && i.Status == SocialInviteStatuses.Pending
                         && (i.Id == inviteOrGuildId || i.GuildId == inviteOrGuildId),
                    ct)
                .ConfigureAwait(false);
            return e is null ? null : MapInvite(e);
        }, cancellationToken);

    public Task<IReadOnlyList<SocialInviteRecord>> ListPendingGuildInvitesForAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            await ExpireInvitesAsync(db, now, ct).ConfigureAwait(false);
            var list = await db.PlayerGuildInvites.AsNoTracking()
                .Where(i => i.Status == SocialInviteStatuses.Pending
                            && (i.ToCharacterId == characterId || i.FromCharacterId == characterId))
                .ToArrayAsync(ct)
                .ConfigureAwait(false);
            return (IReadOnlyList<SocialInviteRecord>)list.Select(MapInvite).ToArray();
        }, cancellationToken);

    public Task<int> CountPendingOutgoingAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            await ExpireInvitesAsync(db, now, ct).ConfigureAwait(false);
            await ExpireFriendsAsync(db, now, ct).ConfigureAwait(false);
            var guild = await db.PlayerGuildInvites.CountAsync(
                    i => i.FromCharacterId == characterId && i.Status == SocialInviteStatuses.Pending,
                    ct)
                .ConfigureAwait(false);
            var friends = await db.PlayerFriendships.CountAsync(
                    f => f.RequestedBy == characterId && f.Status == FriendshipStatuses.Pending,
                    ct)
                .ConfigureAwait(false);
            return guild + friends;
        }, cancellationToken);

    public Task<int> CountPendingIncomingAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            await ExpireInvitesAsync(db, now, ct).ConfigureAwait(false);
            await ExpireFriendsAsync(db, now, ct).ConfigureAwait(false);
            var guild = await db.PlayerGuildInvites.CountAsync(
                    i => i.ToCharacterId == characterId && i.Status == SocialInviteStatuses.Pending,
                    ct)
                .ConfigureAwait(false);
            var friends = await db.PlayerFriendships.CountAsync(
                    f => f.Status == FriendshipStatuses.Pending
                         && f.RequestedBy != characterId
                         && (f.CharacterA == characterId || f.CharacterB == characterId),
                    ct)
                .ConfigureAwait(false);
            return guild + friends;
        }, cancellationToken);

    public Task<SocialCommandPersistResult> CreateGuildAsync(
        Guid leaderCharacterId,
        string displayName,
        string normalizedName,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (await db.PlayerGuildMembers.AnyAsync(m => m.CharacterId == leaderCharacterId, ct)
                    .ConfigureAwait(false))
            {
                return Fail("Deja dans une guilde.");
            }

            if (await db.PlayerGuilds.AnyAsync(g => g.NormalizedName == normalizedName, ct)
                    .ConfigureAwait(false))
            {
                return Fail("Ce nom de guilde est deja pris.");
            }

            var now = _clock.GetUtcNow();
            var id = Guid.NewGuid();
            db.PlayerGuilds.Add(new GuildEntity
            {
                Id = id,
                DisplayName = displayName,
                NormalizedName = normalizedName,
                Motd = string.Empty,
                CreatedAtUtc = now
            });
            db.PlayerGuildMembers.Add(new GuildMemberEntity
            {
                GuildId = id,
                CharacterId = leaderCharacterId,
                Role = (byte)GuildRole.Leader,
                JoinedAtUtc = now
            });
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                return Fail("Ce nom de guilde est deja pris.");
            }

            return new SocialCommandPersistResult(true, "Guilde creee.", id, leaderCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> InviteToGuildAsync(
        Guid guildId,
        Guid fromCharacterId,
        Guid toCharacterId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            await ExpireInvitesAsync(db, now, ct).ConfigureAwait(false);
            var from = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == fromCharacterId && m.GuildId == guildId, ct)
                .ConfigureAwait(false);
            if (from is null)
            {
                return Fail("Vous n'appartenez pas a cette guilde.");
            }

            if (from.Role is not ((byte)GuildRole.Leader or (byte)GuildRole.Officer))
            {
                return Fail("Permission insuffisante.");
            }

            if (await db.PlayerGuildMembers.AnyAsync(m => m.CharacterId == toCharacterId, ct)
                    .ConfigureAwait(false))
            {
                return Fail("Ce personnage est deja dans une guilde.");
            }

            if (await db.PlayerGuildInvites.AnyAsync(
                    i => i.GuildId == guildId
                         && i.ToCharacterId == toCharacterId
                         && i.Status == SocialInviteStatuses.Pending,
                    ct).ConfigureAwait(false))
            {
                return Fail("Invitation deja en attente.");
            }

            db.PlayerGuildInvites.Add(new GuildInviteEntity
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                FromCharacterId = fromCharacterId,
                ToCharacterId = toCharacterId,
                ExpiresAtUtc = expiresAtUtc,
                Status = SocialInviteStatuses.Pending
            });
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                return Fail("Invitation deja en attente.");
            }

            return new SocialCommandPersistResult(true, "Invitation envoyee.", guildId, toCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> RespondGuildInviteAsync(
        Guid inviteId,
        Guid characterId,
        bool accept,
        int maxMembers,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var now = _clock.GetUtcNow();
            await ExpireInvitesAsync(db, now, ct).ConfigureAwait(false);
            var invite = await db.PlayerGuildInvites
                .FirstOrDefaultAsync(
                    i => i.ToCharacterId == characterId
                         && i.Status == SocialInviteStatuses.Pending
                         && (i.Id == inviteId || i.GuildId == inviteId),
                    ct)
                .ConfigureAwait(false);
            if (invite is null)
            {
                return Fail("Invitation invalide ou expiree.");
            }

            if (!accept)
            {
                invite.Status = SocialInviteStatuses.Declined;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return new SocialCommandPersistResult(true, "Invitation refusee.", invite.GuildId, characterId);
            }

            if (await db.PlayerGuildMembers.AnyAsync(m => m.CharacterId == characterId, ct)
                    .ConfigureAwait(false))
            {
                invite.Status = SocialInviteStatuses.Declined;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return Fail("Deja dans une guilde.");
            }

            if (!await db.PlayerGuilds.AnyAsync(g => g.Id == invite.GuildId, ct).ConfigureAwait(false))
            {
                invite.Status = SocialInviteStatuses.Expired;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return Fail("Guilde introuvable.");
            }

            var count = await db.PlayerGuildMembers.CountAsync(m => m.GuildId == invite.GuildId, ct)
                .ConfigureAwait(false);
            if (count >= maxMembers)
            {
                return Fail("Guilde complete.");
            }

            invite.Status = SocialInviteStatuses.Accepted;
            db.PlayerGuildMembers.Add(new GuildMemberEntity
            {
                GuildId = invite.GuildId,
                CharacterId = characterId,
                Role = (byte)GuildRole.Member,
                JoinedAtUtc = now
            });
            var others = await db.PlayerGuildInvites
                .Where(i => i.Id != invite.Id
                            && i.ToCharacterId == characterId
                            && i.Status == SocialInviteStatuses.Pending)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var o in others)
            {
                o.Status = SocialInviteStatuses.Cancelled;
            }

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                return Fail("Deja dans une guilde.");
            }

            return new SocialCommandPersistResult(true, "Vous avez rejoint la guilde.", invite.GuildId, characterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> LeaveGuildAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var member = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == characterId, ct)
                .ConfigureAwait(false);
            if (member is null)
            {
                return Fail("Vous n'etes pas dans une guilde.");
            }

            var count = await db.PlayerGuildMembers.CountAsync(m => m.GuildId == member.GuildId, ct)
                .ConfigureAwait(false);
            if (member.Role == (byte)GuildRole.Leader && count > 1)
            {
                return Fail("Transferez la direction avant de quitter.");
            }

            if (member.Role == (byte)GuildRole.Leader && count == 1)
            {
                return Fail("Utilisez la dissolution pour fermer la guilde.");
            }

            var guildId = member.GuildId;
            db.PlayerGuildMembers.Remove(member);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Vous avez quitte la guilde.", guildId, characterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> KickGuildMemberAsync(
        Guid actorCharacterId,
        Guid targetCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var actor = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == actorCharacterId, ct)
                .ConfigureAwait(false);
            var target = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == targetCharacterId, ct)
                .ConfigureAwait(false);
            if (actor is null || target is null || actor.GuildId != target.GuildId)
            {
                return Fail("Cible introuvable dans la guilde.");
            }

            if (actorCharacterId == targetCharacterId)
            {
                return Fail("Impossible de s'exclure soi-meme.");
            }

            if (target.Role == (byte)GuildRole.Leader)
            {
                return Fail("Impossible d'exclure le chef.");
            }

            if (actor.Role == (byte)GuildRole.Member)
            {
                return Fail("Permission insuffisante.");
            }

            if (actor.Role == (byte)GuildRole.Officer && target.Role != (byte)GuildRole.Member)
            {
                return Fail("Permission insuffisante.");
            }

            db.PlayerGuildMembers.Remove(target);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Membre exclu.", actor.GuildId, targetCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> TransferGuildLeaderAsync(
        Guid actorCharacterId,
        Guid targetCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var actor = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == actorCharacterId, ct)
                .ConfigureAwait(false);
            if (actor is null || actor.Role != (byte)GuildRole.Leader)
            {
                return Fail("Seul le chef peut transferer la direction.");
            }

            var target = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == targetCharacterId, ct)
                .ConfigureAwait(false);
            if (target is null || target.GuildId != actor.GuildId)
            {
                return Fail("Cible introuvable dans la guilde.");
            }

            if (actorCharacterId == targetCharacterId)
            {
                return Fail("Cible invalide.");
            }

            actor.Role = (byte)GuildRole.Officer;
            target.Role = (byte)GuildRole.Leader;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Direction transferee.", actor.GuildId, targetCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> DisbandGuildAsync(
        Guid actorCharacterId,
        bool confirm,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (!confirm)
            {
                return Fail("Confirmation requise.");
            }

            var actor = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == actorCharacterId, ct)
                .ConfigureAwait(false);
            if (actor is null)
            {
                return Fail("Vous n'etes pas dans une guilde.");
            }

            if (actor.Role != (byte)GuildRole.Leader)
            {
                return Fail("Seul le chef peut dissoudre la guilde.");
            }

            var count = await db.PlayerGuildMembers.CountAsync(m => m.GuildId == actor.GuildId, ct)
                .ConfigureAwait(false);
            if (count > 1)
            {
                return Fail("Excluez ou faites partir les autres membres d'abord.");
            }

            var guildId = actor.GuildId;
            var guild = await db.PlayerGuilds.FirstOrDefaultAsync(g => g.Id == guildId, ct).ConfigureAwait(false);
            if (guild is not null)
            {
                db.PlayerGuilds.Remove(guild);
            }

            var invites = await db.PlayerGuildInvites.Where(i => i.GuildId == guildId).ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var inv in invites)
            {
                inv.Status = SocialInviteStatuses.Cancelled;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Guilde dissoute.", guildId, actorCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> SetGuildMotdAsync(
        Guid actorCharacterId,
        string motd,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var actor = await db.PlayerGuildMembers
                .FirstOrDefaultAsync(m => m.CharacterId == actorCharacterId, ct)
                .ConfigureAwait(false);
            if (actor is null || actor.Role is not ((byte)GuildRole.Leader or (byte)GuildRole.Officer))
            {
                return Fail("Permission insuffisante.");
            }

            var guild = await db.PlayerGuilds.FirstOrDefaultAsync(g => g.Id == actor.GuildId, ct)
                .ConfigureAwait(false);
            if (guild is null)
            {
                return Fail("Guilde introuvable.");
            }

            guild.Motd = motd;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Message de guilde mis a jour.", actor.GuildId, actorCharacterId);
        }, cancellationToken);

    public Task<FriendshipRecord?> FindFriendshipAsync(Guid a, Guid b, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await ExpireFriendsAsync(db, _clock.GetUtcNow(), ct).ConfigureAwait(false);
            var low = Low(a, b);
            var high = High(a, b);
            var e = await db.PlayerFriendships
                .FirstOrDefaultAsync(f => f.CharacterA == low && f.CharacterB == high, ct)
                .ConfigureAwait(false);
            return e is null ? null : MapFriend(e);
        }, cancellationToken);

    public Task<IReadOnlyList<FriendshipRecord>> ListFriendshipsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await ExpireFriendsAsync(db, _clock.GetUtcNow(), ct).ConfigureAwait(false);
            var list = await db.PlayerFriendships.AsNoTracking()
                .Where(f => f.CharacterA == characterId || f.CharacterB == characterId)
                .ToArrayAsync(ct)
                .ConfigureAwait(false);
            return (IReadOnlyList<FriendshipRecord>)list.Select(MapFriend).ToArray();
        }, cancellationToken);

    public Task<SocialCommandPersistResult> RequestFriendAsync(
        Guid fromCharacterId,
        Guid toCharacterId,
        DateTimeOffset expiresAtUtc,
        int maxFriends,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await ExpireFriendsAsync(db, _clock.GetUtcNow(), ct).ConfigureAwait(false);
            if (fromCharacterId == toCharacterId)
            {
                return Fail("Cible invalide.");
            }

            if (await CountAcceptedFriendsAsync(db, fromCharacterId, ct).ConfigureAwait(false) >= maxFriends)
            {
                return Fail("Liste d'amis complete.");
            }

            var low = Low(fromCharacterId, toCharacterId);
            var high = High(fromCharacterId, toCharacterId);
            var existing = await db.PlayerFriendships
                .FirstOrDefaultAsync(f => f.CharacterA == low && f.CharacterB == high, ct)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.Status == FriendshipStatuses.Accepted)
                {
                    return Fail("Deja amis.");
                }

                if (existing.Status == FriendshipStatuses.Pending)
                {
                    return Fail("Demande deja en attente.");
                }

                db.PlayerFriendships.Remove(existing);
            }

            var id = Guid.NewGuid();
            db.PlayerFriendships.Add(new FriendshipEntity
            {
                Id = id,
                CharacterA = low,
                CharacterB = high,
                RequestedBy = fromCharacterId,
                Status = FriendshipStatuses.Pending,
                ExpiresAtUtc = expiresAtUtc,
                CreatedAtUtc = _clock.GetUtcNow()
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Demande d'ami envoyee.", id, toCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> RespondFriendAsync(
        Guid actorCharacterId,
        Guid otherCharacterId,
        bool accept,
        int maxFriends,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await ExpireFriendsAsync(db, _clock.GetUtcNow(), ct).ConfigureAwait(false);
            var low = Low(actorCharacterId, otherCharacterId);
            var high = High(actorCharacterId, otherCharacterId);
            var existing = await db.PlayerFriendships
                .FirstOrDefaultAsync(f => f.CharacterA == low && f.CharacterB == high, ct)
                .ConfigureAwait(false);
            if (existing is null || existing.Status != FriendshipStatuses.Pending)
            {
                return Fail("Demande invalide ou expiree.");
            }

            if (existing.RequestedBy == actorCharacterId)
            {
                return Fail("Vous ne pouvez pas accepter votre propre demande.");
            }

            if (!accept)
            {
                existing.Status = FriendshipStatuses.Declined;
                existing.ExpiresAtUtc = null;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return new SocialCommandPersistResult(true, "Demande refusee.", existing.Id, otherCharacterId);
            }

            if (await CountAcceptedFriendsAsync(db, actorCharacterId, ct).ConfigureAwait(false) >= maxFriends
                || await CountAcceptedFriendsAsync(db, otherCharacterId, ct).ConfigureAwait(false) >= maxFriends)
            {
                return Fail("Liste d'amis complete.");
            }

            existing.Status = FriendshipStatuses.Accepted;
            existing.ExpiresAtUtc = null;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Ami ajoute.", existing.Id, otherCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> RemoveFriendAsync(
        Guid actorCharacterId,
        Guid otherCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var low = Low(actorCharacterId, otherCharacterId);
            var high = High(actorCharacterId, otherCharacterId);
            var existing = await db.PlayerFriendships
                .FirstOrDefaultAsync(f => f.CharacterA == low && f.CharacterB == high, ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return Fail("Relation introuvable.");
            }

            db.PlayerFriendships.Remove(existing);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Relation supprimee.", existing.Id, otherCharacterId);
        }, cancellationToken);

    public Task<bool> IsBlockedAsync(
        Guid blockerCharacterId,
        Guid blockedCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(
            (db, ct) => db.PlayerCharacterBlocks.AsNoTracking()
                .AnyAsync(
                    b => b.BlockerCharacterId == blockerCharacterId && b.BlockedCharacterId == blockedCharacterId,
                    ct),
            cancellationToken);

    public Task<IReadOnlyList<BlockRecord>> ListBlocksAsync(
        Guid blockerCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var list = await db.PlayerCharacterBlocks.AsNoTracking()
                .Where(b => b.BlockerCharacterId == blockerCharacterId)
                .ToArrayAsync(ct)
                .ConfigureAwait(false);
            return (IReadOnlyList<BlockRecord>)list
                .Select(b => new BlockRecord(b.BlockerCharacterId, b.BlockedCharacterId, b.CreatedAtUtc))
                .ToArray();
        }, cancellationToken);

    public Task<SocialCommandPersistResult> BlockAsync(
        Guid blockerCharacterId,
        Guid blockedCharacterId,
        int maxBlocks,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (blockerCharacterId == blockedCharacterId)
            {
                return Fail("Cible invalide.");
            }

            var count = await db.PlayerCharacterBlocks.CountAsync(b => b.BlockerCharacterId == blockerCharacterId, ct)
                .ConfigureAwait(false);
            if (count >= maxBlocks)
            {
                return Fail("Liste de blocage complete.");
            }

            if (!await db.PlayerCharacterBlocks.AnyAsync(
                    b => b.BlockerCharacterId == blockerCharacterId && b.BlockedCharacterId == blockedCharacterId,
                    ct).ConfigureAwait(false))
            {
                db.PlayerCharacterBlocks.Add(new CharacterBlockEntity
                {
                    BlockerCharacterId = blockerCharacterId,
                    BlockedCharacterId = blockedCharacterId,
                    CreatedAtUtc = _clock.GetUtcNow()
                });
            }

            var low = Low(blockerCharacterId, blockedCharacterId);
            var high = High(blockerCharacterId, blockedCharacterId);
            var friendship = await db.PlayerFriendships
                .FirstOrDefaultAsync(f => f.CharacterA == low && f.CharacterB == high, ct)
                .ConfigureAwait(false);
            if (friendship is not null)
            {
                db.PlayerFriendships.Remove(friendship);
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new SocialCommandPersistResult(true, "Personnage bloque.", Guid.Empty, blockedCharacterId);
        }, cancellationToken);

    public Task<SocialCommandPersistResult> UnblockAsync(
        Guid blockerCharacterId,
        Guid blockedCharacterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.PlayerCharacterBlocks.FirstOrDefaultAsync(
                    b => b.BlockerCharacterId == blockerCharacterId && b.BlockedCharacterId == blockedCharacterId,
                    ct)
                .ConfigureAwait(false);
            if (row is not null)
            {
                db.PlayerCharacterBlocks.Remove(row);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return new SocialCommandPersistResult(true, "Blocage leve.", Guid.Empty, blockedCharacterId);
        }, cancellationToken);

    private static async Task ExpireInvitesAsync(FrogDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.PlayerGuildInvites
            .Where(i => i.Status == SocialInviteStatuses.Pending && i.ExpiresAtUtc <= now)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var inv in expired)
        {
            inv.Status = SocialInviteStatuses.Expired;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task ExpireFriendsAsync(FrogDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.PlayerFriendships
            .Where(f => f.Status == FriendshipStatuses.Pending && f.ExpiresAtUtc != null && f.ExpiresAtUtc <= now)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (expired.Count == 0)
        {
            return;
        }

        db.PlayerFriendships.RemoveRange(expired);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static Task<int> CountAcceptedFriendsAsync(FrogDbContext db, Guid characterId, CancellationToken ct)
        => db.PlayerFriendships.CountAsync(
            f => f.Status == FriendshipStatuses.Accepted
                 && (f.CharacterA == characterId || f.CharacterB == characterId),
            ct);

    private static SocialCommandPersistResult Fail(string message)
        => new(false, message, Guid.Empty, Guid.Empty);

    private static GuildRecord MapGuild(GuildEntity e)
        => new(e.Id, e.DisplayName, e.NormalizedName, e.Motd, e.CreatedAtUtc);

    private static GuildMemberRecord MapMember(GuildMemberEntity e)
        => new(e.GuildId, e.CharacterId, (GuildRole)e.Role, e.JoinedAtUtc);

    private static SocialInviteRecord MapInvite(GuildInviteEntity e)
        => new(e.Id, SocialKind.Guild, e.GuildId, e.FromCharacterId, e.ToCharacterId, e.ExpiresAtUtc, e.Status);

    private static FriendshipRecord MapFriend(FriendshipEntity e)
        => new(e.Id, e.CharacterA, e.CharacterB, e.RequestedBy, e.Status, e.ExpiresAtUtc, e.CreatedAtUtc);

    private static Guid Low(Guid a, Guid b) => a.CompareTo(b) <= 0 ? a : b;

    private static Guid High(Guid a, Guid b) => a.CompareTo(b) <= 0 ? b : a;
}
