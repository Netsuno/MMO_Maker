using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Application.Social;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Config;
using Frog.Server.Models;
using Frog.Server.Network;
using Frog.Server.Services;
using Microsoft.Extensions.Options;

namespace Frog.Server.Social;

public sealed class SocialService : ISocialPresenceSink
{
    private readonly ISocialStore _store;
    private readonly ICharacterRepository _characters;
    private readonly ConnectionManager _connections;
    private readonly ClientRegistry _clients;
    private readonly SocialOptions _options;
    private readonly TimeProvider _clock;
    private readonly PartyRoster _parties = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, SocialResultWire>> _replays = new();
    private readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> _inviteRate = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _reinviteCooldown = new();
    private readonly object _partyGate = new();

    public SocialService(
        ISocialStore store,
        ICharacterRepository characters,
        ConnectionManager connections,
        ClientRegistry clients,
        IOptions<SocialOptions> options,
        TimeProvider? clock = null)
    {
        _store = store;
        _characters = characters;
        _connections = connections;
        _clients = clients;
        _options = options.Value;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<SocialResultWire> ExecuteAsync(
        Session session,
        SocialKind kind,
        byte action,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid actorId || actorId == Guid.Empty)
        {
            return Fail(kind, action, requestId, "Personnage actif requis.");
        }

        if (TryGetReplay(actorId, requestId, out var replay))
        {
            return replay;
        }

        if (!SocialWire.IsKnownAction(kind, action))
        {
            return Remember(actorId, Fail(kind, action, requestId, "Action inconnue."));
        }

        SocialResultWire result = kind switch
        {
            SocialKind.Party => await ExecutePartyAsync(session, actorId, action, requestId, extra, cancellationToken)
                .ConfigureAwait(false),
            SocialKind.Guild => await ExecuteGuildAsync(session, actorId, action, requestId, extra, cancellationToken)
                .ConfigureAwait(false),
            SocialKind.Friend => await ExecuteFriendAsync(session, actorId, action, requestId, extra, cancellationToken)
                .ConfigureAwait(false),
            SocialKind.Block => await ExecuteBlockAsync(session, actorId, action, requestId, extra, cancellationToken)
                .ConfigureAwait(false),
            _ => Fail(kind, action, requestId, "Action inconnue.")
        };

        return Remember(actorId, result);
    }

    public IReadOnlyList<Guid> GetPartyMemberIds(Guid characterId)
        => _parties.FindByCharacter(characterId) is { } party
            ? party.Members.Select(m => m.CharacterId).ToArray()
            : Array.Empty<Guid>();

    public async Task<IReadOnlyList<Guid>> GetGuildMemberIdsAsync(Guid characterId, CancellationToken cancellationToken)
    {
        var member = await _store.FindGuildMemberAsync(characterId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Array.Empty<Guid>();
        }

        var list = await _store.ListGuildMembersAsync(member.GuildId, cancellationToken).ConfigureAwait(false);
        return list.Select(m => m.CharacterId).ToArray();
    }

    /// <summary>Vrai si <paramref name="target"/> a bloqué <paramref name="actor"/> (l'acteur ne peut plus le contacter).</summary>
    public Task<bool> IsBlockedFromContactingAsync(Guid actor, Guid target, CancellationToken cancellationToken)
        => _store.IsBlockedAsync(target, actor, cancellationToken);

    public async Task NotifyCharacterOnlineAsync(Guid characterId, string displayName, CancellationToken cancellationToken)
    {
        await FanoutPresenceAsync(characterId, SocialEventType.PresenceOnline, displayName, cancellationToken)
            .ConfigureAwait(false);
        if (TryGetClient(characterId, out var client) && client is not null)
        {
            await PushAllSnapshotsAsync(client, characterId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task NotifyCharacterOfflineAsync(Guid characterId, CancellationToken cancellationToken)
    {
        await FanoutPresenceAsync(characterId, SocialEventType.PresenceOffline, string.Empty, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PushAllSnapshotsAsync(ClientSession client, Guid characterId, CancellationToken cancellationToken)
    {
        await PushPartySnapshotAsync(client, characterId, cancellationToken).ConfigureAwait(false);
        await PushGuildSnapshotAsync(client, characterId, cancellationToken).ConfigureAwait(false);
        await PushFriendSnapshotAsync(client, characterId, cancellationToken).ConfigureAwait(false);
        await PushBlockSnapshotAsync(client, characterId, cancellationToken).ConfigureAwait(false);
    }

    public bool TryGetClientSession(Guid characterId, out ClientSession? client)
        => TryGetClient(characterId, out client);

    private async Task<SocialResultWire> ExecutePartyAsync(
        Session session,
        Guid actorId,
        byte action,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        PartyInviteResult partyResult;
        IReadOnlyList<Guid>? dissolvedMembers = null;
        if (action == (byte)PartyAction.Invite)
        {
            partyResult = await InvitePartyAsync(actorId, extra, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            lock (_partyGate)
            {
                var now = _clock.GetUtcNow();
                if (action == (byte)PartyAction.Disband)
                {
                    var existing = _parties.FindByCharacter(actorId);
                    partyResult = DisbandPartyLocked(actorId, extra.Span);
                    if (partyResult.Success && existing is not null)
                    {
                        dissolvedMembers = existing.Members.Select(m => m.CharacterId).ToArray();
                    }
                }
                else
                {
                    partyResult = (PartyAction)action switch
                    {
                        PartyAction.Accept => AcceptPartyLocked(actorId, extra.Span, now),
                        PartyAction.Decline => DeclinePartyLocked(actorId, extra.Span, now),
                        PartyAction.Cancel => CancelPartyLocked(actorId, extra.Span, now),
                        PartyAction.Leave => _parties.Leave(actorId),
                        PartyAction.Kick => KickPartyLocked(actorId, extra.Span),
                        PartyAction.TransferLeader => TransferPartyLocked(actorId, extra.Span),
                        _ => PartyInviteResult.Fail("Action inconnue.")
                    };
                }
            }
        }

        if (!partyResult.Success)
        {
            return Fail(SocialKind.Party, action, requestId, partyResult.Message);
        }

        await AfterPartyMutationAsync(
                session,
                actorId,
                (PartyAction)action,
                partyResult,
                dissolvedMembers,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(SocialKind.Party, action, requestId, partyResult.Message, partyResult.PartyId, partyResult.OtherId);
    }

    private async Task<PartyInviteResult> InvitePartyAsync(
        Guid actorId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (!SocialWire.TryReadGuid(extra.Span, out var targetId))
        {
            return PartyInviteResult.Fail("Cible invalide.");
        }

        if (!TryGetOnlineCharacter(targetId, out _))
        {
            return PartyInviteResult.Fail("Joueur hors ligne.");
        }

        if (await _store.IsBlockedAsync(targetId, actorId, cancellationToken).ConfigureAwait(false))
        {
            return PartyInviteResult.Fail("Vous etes bloque.");
        }

        var pendingOutStore = await _store.CountPendingOutgoingAsync(actorId, cancellationToken).ConfigureAwait(false);
        var pendingInStore = await _store.CountPendingIncomingAsync(targetId, cancellationToken).ConfigureAwait(false);

        lock (_partyGate)
        {
            var now = _clock.GetUtcNow();
            if (!TryAllowInviteLocked(actorId, targetId, SocialKind.Party, now, out var err))
            {
                return PartyInviteResult.Fail(err);
            }

            var pendingOut = _parties.CountPendingOutgoing(actorId) + pendingOutStore;
            var pendingIn = _parties.CountPendingIncoming(targetId) + pendingInStore;
            if (pendingOut >= _options.MaxPendingOutgoing)
            {
                return PartyInviteResult.Fail("Trop d'invitations sortantes.");
            }

            if (pendingIn >= _options.MaxPendingIncoming)
            {
                return PartyInviteResult.Fail("Trop d'invitations entrantes pour la cible.");
            }

            return _parties.Invite(
                actorId,
                targetId,
                _options.PartyMaxMembers,
                TimeSpan.FromSeconds(_options.PartyInviteSeconds),
                now);
        }
    }

    private PartyInviteResult AcceptPartyLocked(Guid actorId, ReadOnlySpan<byte> extra, DateTimeOffset now)
    {
        if (!SocialWire.TryReadGuid(extra, out var id))
        {
            return PartyInviteResult.Fail("Invitation invalide ou expiree.");
        }

        return _parties.Accept(actorId, id, _options.PartyMaxMembers, now);
    }

    private PartyInviteResult DeclinePartyLocked(Guid actorId, ReadOnlySpan<byte> extra, DateTimeOffset now)
    {
        if (!SocialWire.TryReadGuid(extra, out var id))
        {
            return PartyInviteResult.Fail("Invitation invalide ou expiree.");
        }

        var result = _parties.Decline(actorId, id, now);
        if (result.Success)
        {
            MarkCooldown(result.OtherId == actorId ? result.PartyId : result.OtherId, actorId, SocialKind.Party, now);
        }

        return result;
    }

    private PartyInviteResult CancelPartyLocked(Guid actorId, ReadOnlySpan<byte> extra, DateTimeOffset now)
    {
        if (!SocialWire.TryReadGuid(extra, out var id))
        {
            return PartyInviteResult.Fail("Invitation invalide ou expiree.");
        }

        return _parties.Cancel(actorId, id, now);
    }

    private PartyInviteResult KickPartyLocked(Guid actorId, ReadOnlySpan<byte> extra)
        => SocialWire.TryReadGuid(extra, out var target)
            ? _parties.Kick(actorId, target)
            : PartyInviteResult.Fail("Cible invalide.");

    private PartyInviteResult TransferPartyLocked(Guid actorId, ReadOnlySpan<byte> extra)
        => SocialWire.TryReadGuid(extra, out var target)
            ? _parties.TransferLeader(actorId, target)
            : PartyInviteResult.Fail("Cible invalide.");

    private PartyInviteResult DisbandPartyLocked(Guid actorId, ReadOnlySpan<byte> extra)
        => SocialWire.TryReadConfirm(extra, out var confirm)
            ? _parties.Disband(actorId, confirm)
            : PartyInviteResult.Fail("Confirmation requise.");

    private async Task AfterPartyMutationAsync(
        Session session,
        Guid actorId,
        PartyAction action,
        PartyInviteResult result,
        IReadOnlyList<Guid>? dissolvedMembers,
        CancellationToken cancellationToken)
    {
        if (action == PartyAction.Invite && result.Success)
        {
            await SendEventAsync(
                    result.OtherId,
                    new SocialEventWire(
                        SocialEventType.InviteReceived,
                        SocialKind.Party,
                        result.PartyId,
                        actorId,
                        result.OtherId,
                        session.Username + " vous invite dans un groupe."),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (action is PartyAction.Decline or PartyAction.Cancel)
        {
            var notify = action == PartyAction.Decline ? result.PartyId : result.OtherId;
            var leader = _parties.FindById(result.PartyId)?.LeaderId ?? Guid.Empty;
            await SendEventAsync(
                    action == PartyAction.Decline ? leader : result.OtherId,
                    new SocialEventWire(
                        action == PartyAction.Decline ? SocialEventType.InviteDeclined : SocialEventType.InviteCancelled,
                        SocialKind.Party,
                        result.PartyId,
                        actorId,
                        notify,
                        result.Message),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (action == PartyAction.Disband)
        {
            var notifyIds = dissolvedMembers is { Count: > 0 }
                ? dissolvedMembers
                : (IReadOnlyList<Guid>)[actorId, result.OtherId];
            foreach (var memberId in notifyIds.Distinct())
            {
                await SendEventAsync(
                        memberId,
                        new SocialEventWire(
                            SocialEventType.Disbanded,
                            SocialKind.Party,
                            result.PartyId,
                            actorId,
                            Guid.Empty,
                            "Le groupe a ete dissous. Les groupes ne survivent pas a un redemarrage serveur."),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        await PushPartyToMembersAsync(result.PartyId, actorId, cancellationToken).ConfigureAwait(false);
        if (action is PartyAction.Kick or PartyAction.Leave)
        {
            await SendEventAsync(
                    result.OtherId,
                    new SocialEventWire(
                        action == PartyAction.Kick ? SocialEventType.MemberKicked : SocialEventType.MemberLeft,
                        SocialKind.Party,
                        result.PartyId,
                        actorId,
                        result.OtherId,
                        result.Message),
                    cancellationToken)
                .ConfigureAwait(false);
            if (TryGetClient(result.OtherId, out var left) && left is not null)
            {
                await SendSnapshotAsync(
                        left,
                        new SocialSnapshotWire(SocialKind.Party, Guid.Empty, Guid.Empty, string.Empty, Array.Empty<SocialMemberWire>()),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<SocialResultWire> ExecuteGuildAsync(
        Session session,
        Guid actorId,
        byte action,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        SocialCommandPersistResult persisted;
        switch ((GuildAction)action)
        {
            case GuildAction.Create:
                if (!SocialWire.TryReadUtf8(extra.Span, SocialProtocolLimits.MaxGuildNameUtf8Bytes, out var rawName))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Nom de guilde invalide.");
                }

                var display = SocialWire.NormalizeGuildName(rawName);
                if (display.Length == 0 || display.EnumerateRunes().Count() > SocialProtocolLimits.MaxGuildNameGraphemes)
                {
                    return Fail(SocialKind.Guild, action, requestId, "Nom de guilde invalide.");
                }

                persisted = await _store.CreateGuildAsync(actorId, display, SocialWire.GuildNameKey(display), cancellationToken)
                    .ConfigureAwait(false);
                break;
            case GuildAction.Invite:
                if (!SocialWire.TryReadGuid(extra.Span, out var inviteTarget))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Cible invalide.");
                }

                if (await _store.IsBlockedAsync(inviteTarget, actorId, cancellationToken).ConfigureAwait(false))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Vous etes bloque.");
                }

                if (!TryAllowInvite(actorId, inviteTarget, SocialKind.Guild, now, out var inviteErr))
                {
                    return Fail(SocialKind.Guild, action, requestId, inviteErr);
                }

                if (!await CheckPendingCapsAsync(actorId, inviteTarget, cancellationToken).ConfigureAwait(false))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Trop d'invitations.");
                }

                var membership = await _store.FindGuildMemberAsync(actorId, cancellationToken).ConfigureAwait(false);
                if (membership is null)
                {
                    return Fail(SocialKind.Guild, action, requestId, "Vous n'etes pas dans une guilde.");
                }

                persisted = await _store.InviteToGuildAsync(
                        membership.GuildId,
                        actorId,
                        inviteTarget,
                        now.AddSeconds(_options.GuildInviteSeconds),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (persisted.Success)
                {
                    await SendEventAsync(
                            inviteTarget,
                            new SocialEventWire(
                                SocialEventType.InviteReceived,
                                SocialKind.Guild,
                                membership.GuildId,
                                actorId,
                                inviteTarget,
                                session.Username + " vous invite dans une guilde."),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            case GuildAction.Accept:
            case GuildAction.Decline:
                if (!SocialWire.TryReadGuid(extra.Span, out var inviteId))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Invitation invalide ou expiree.");
                }

                persisted = await _store.RespondGuildInviteAsync(
                        inviteId,
                        actorId,
                        accept: action == (byte)GuildAction.Accept,
                        _options.GuildMaxMembers,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case GuildAction.Leave:
                persisted = await _store.LeaveGuildAsync(actorId, cancellationToken).ConfigureAwait(false);
                break;
            case GuildAction.Kick:
                if (!SocialWire.TryReadGuid(extra.Span, out var kickTarget))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Cible invalide.");
                }

                persisted = await _store.KickGuildMemberAsync(actorId, kickTarget, cancellationToken).ConfigureAwait(false);
                break;
            case GuildAction.TransferLeader:
                if (!SocialWire.TryReadGuid(extra.Span, out var newLeader))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Cible invalide.");
                }

                persisted = await _store.TransferGuildLeaderAsync(actorId, newLeader, cancellationToken).ConfigureAwait(false);
                break;
            case GuildAction.Disband:
                if (!SocialWire.TryReadConfirm(extra.Span, out var confirm))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Confirmation requise.");
                }

                persisted = await _store.DisbandGuildAsync(actorId, confirm, cancellationToken).ConfigureAwait(false);
                break;
            case GuildAction.SetMotd:
                if (!SocialWire.TryReadUtf8(extra.Span, SocialProtocolLimits.MaxMotdUtf8Bytes, out var motd))
                {
                    return Fail(SocialKind.Guild, action, requestId, "Message invalide.");
                }

                persisted = await _store.SetGuildMotdAsync(actorId, motd, cancellationToken).ConfigureAwait(false);
                break;
            default:
                return Fail(SocialKind.Guild, action, requestId, "Action inconnue.");
        }

        if (!persisted.Success)
        {
            return Fail(SocialKind.Guild, action, requestId, persisted.Message);
        }

        await AfterGuildMutationAsync(actorId, (GuildAction)action, persisted, cancellationToken).ConfigureAwait(false);
        return Ok(SocialKind.Guild, action, requestId, persisted.Message, persisted.SubjectId, persisted.OtherId);
    }

    private async Task AfterGuildMutationAsync(
        Guid actorId,
        GuildAction action,
        SocialCommandPersistResult persisted,
        CancellationToken cancellationToken)
    {
        if (action == GuildAction.Disband)
        {
            await SendEventAsync(
                    actorId,
                    new SocialEventWire(
                        SocialEventType.Disbanded,
                        SocialKind.Guild,
                        persisted.SubjectId,
                        actorId,
                        Guid.Empty,
                        persisted.Message),
                    cancellationToken)
                .ConfigureAwait(false);
            if (TryGetClient(actorId, out var self) && self is not null)
            {
                await SendSnapshotAsync(
                        self,
                        new SocialSnapshotWire(SocialKind.Guild, Guid.Empty, Guid.Empty, string.Empty, Array.Empty<SocialMemberWire>()),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        await PushGuildToMembersAsync(persisted.SubjectId, cancellationToken).ConfigureAwait(false);
        if (action is GuildAction.Kick or GuildAction.Leave)
        {
            var target = action == GuildAction.Kick ? persisted.OtherId : actorId;
            if (TryGetClient(target, out var left) && left is not null)
            {
                await SendSnapshotAsync(
                        left,
                        new SocialSnapshotWire(SocialKind.Guild, Guid.Empty, Guid.Empty, string.Empty, Array.Empty<SocialMemberWire>()),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<SocialResultWire> ExecuteFriendAsync(
        Session session,
        Guid actorId,
        byte action,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (!SocialWire.TryReadGuid(extra.Span, out var otherId))
        {
            return Fail(SocialKind.Friend, action, requestId, "Cible invalide.");
        }

        var now = _clock.GetUtcNow();
        SocialCommandPersistResult persisted;
        switch ((FriendAction)action)
        {
            case FriendAction.Request:
                if (await _store.IsBlockedAsync(otherId, actorId, cancellationToken).ConfigureAwait(false))
                {
                    return Fail(SocialKind.Friend, action, requestId, "Vous etes bloque.");
                }

                if (!TryAllowInvite(actorId, otherId, SocialKind.Friend, now, out var err))
                {
                    return Fail(SocialKind.Friend, action, requestId, err);
                }

                if (!await CheckPendingCapsAsync(actorId, otherId, cancellationToken).ConfigureAwait(false))
                {
                    return Fail(SocialKind.Friend, action, requestId, "Trop d'invitations.");
                }

                persisted = await _store.RequestFriendAsync(
                        actorId,
                        otherId,
                        now.AddDays(_options.FriendRequestDays),
                        _options.MaxFriends,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (persisted.Success)
                {
                    await SendEventAsync(
                            otherId,
                            new SocialEventWire(
                                SocialEventType.InviteReceived,
                                SocialKind.Friend,
                                persisted.SubjectId,
                                actorId,
                                otherId,
                                session.Username + " souhaite etre votre ami."),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            case FriendAction.Accept:
            case FriendAction.Decline:
                persisted = await _store.RespondFriendAsync(
                        actorId,
                        otherId,
                        accept: action == (byte)FriendAction.Accept,
                        _options.MaxFriends,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case FriendAction.Remove:
                persisted = await _store.RemoveFriendAsync(actorId, otherId, cancellationToken).ConfigureAwait(false);
                break;
            default:
                return Fail(SocialKind.Friend, action, requestId, "Action inconnue.");
        }

        if (!persisted.Success)
        {
            return Fail(SocialKind.Friend, action, requestId, persisted.Message);
        }

        if (TryGetClient(actorId, out var self) && self is not null)
        {
            await PushFriendSnapshotAsync(self, actorId, cancellationToken).ConfigureAwait(false);
        }

        if (TryGetClient(otherId, out var other) && other is not null)
        {
            await PushFriendSnapshotAsync(other, otherId, cancellationToken).ConfigureAwait(false);
        }

        return Ok(SocialKind.Friend, action, requestId, persisted.Message, persisted.SubjectId, otherId);
    }

    private async Task<SocialResultWire> ExecuteBlockAsync(
        Session session,
        Guid actorId,
        byte action,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (!SocialWire.TryReadGuid(extra.Span, out var otherId))
        {
            return Fail(SocialKind.Block, action, requestId, "Cible invalide.");
        }

        SocialCommandPersistResult persisted = (BlockAction)action switch
        {
            BlockAction.Block => await _store.BlockAsync(actorId, otherId, _options.MaxBlocks, cancellationToken)
                .ConfigureAwait(false),
            BlockAction.Unblock => await _store.UnblockAsync(actorId, otherId, cancellationToken).ConfigureAwait(false),
            _ => new SocialCommandPersistResult(false, "Action inconnue.", Guid.Empty, Guid.Empty)
        };

        if (!persisted.Success)
        {
            return Fail(SocialKind.Block, action, requestId, persisted.Message);
        }

        if (TryGetClient(actorId, out var self) && self is not null)
        {
            await PushBlockSnapshotAsync(self, actorId, cancellationToken).ConfigureAwait(false);
            await PushFriendSnapshotAsync(self, actorId, cancellationToken).ConfigureAwait(false);
        }

        return Ok(SocialKind.Block, action, requestId, persisted.Message, Guid.Empty, otherId);
    }

    private async Task PushPartyToMembersAsync(Guid partyId, Guid fallback, CancellationToken cancellationToken)
    {
        var ids = _parties.MemberIds(partyId);
        if (ids.Count == 0)
        {
            ids = [fallback];
        }

        foreach (var id in ids)
        {
            if (TryGetClient(id, out var client) && client is not null)
            {
                await PushPartySnapshotAsync(client, id, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task PushGuildToMembersAsync(Guid guildId, CancellationToken cancellationToken)
    {
        if (guildId == Guid.Empty)
        {
            return;
        }

        var members = await _store.ListGuildMembersAsync(guildId, cancellationToken).ConfigureAwait(false);
        foreach (var m in members)
        {
            if (TryGetClient(m.CharacterId, out var client) && client is not null)
            {
                await PushGuildSnapshotAsync(client, m.CharacterId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task PushPartySnapshotAsync(ClientSession client, Guid characterId, CancellationToken cancellationToken)
    {
        var party = _parties.FindByCharacter(characterId);
        if (party is null)
        {
            await SendSnapshotAsync(
                    client,
                    new SocialSnapshotWire(SocialKind.Party, Guid.Empty, Guid.Empty, string.Empty, Array.Empty<SocialMemberWire>()),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var members = new List<SocialMemberWire>(party.Members.Count);
        foreach (var m in party.Members)
        {
            members.Add(await ToMemberWireAsync(m.CharacterId, m.IsLeader ? (byte)1 : (byte)0, cancellationToken)
                .ConfigureAwait(false));
        }

        await SendSnapshotAsync(
                client,
                new SocialSnapshotWire(SocialKind.Party, party.Id, party.LeaderId, string.Empty, members),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PushGuildSnapshotAsync(ClientSession client, Guid characterId, CancellationToken cancellationToken)
    {
        var member = await _store.FindGuildMemberAsync(characterId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            await SendSnapshotAsync(
                    client,
                    new SocialSnapshotWire(SocialKind.Guild, Guid.Empty, Guid.Empty, string.Empty, Array.Empty<SocialMemberWire>()),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var guild = await _store.FindGuildByIdAsync(member.GuildId, cancellationToken).ConfigureAwait(false);
        var list = await _store.ListGuildMembersAsync(member.GuildId, cancellationToken).ConfigureAwait(false);
        var members = new List<SocialMemberWire>(list.Count);
        Guid leader = Guid.Empty;
        foreach (var m in list)
        {
            if (m.Role == GuildRole.Leader)
            {
                leader = m.CharacterId;
            }

            members.Add(await ToMemberWireAsync(m.CharacterId, (byte)m.Role, cancellationToken).ConfigureAwait(false));
        }

        await SendSnapshotAsync(
                client,
                new SocialSnapshotWire(SocialKind.Guild, member.GuildId, leader, guild?.Motd ?? string.Empty, members),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PushFriendSnapshotAsync(ClientSession client, Guid characterId, CancellationToken cancellationToken)
    {
        var list = await _store.ListFriendshipsAsync(characterId, cancellationToken).ConfigureAwait(false);
        var members = new List<SocialMemberWire>();
        foreach (var f in list)
        {
            var other = f.CharacterA == characterId ? f.CharacterB : f.CharacterA;
            var role = f.Status == FriendshipStatuses.Accepted
                ? (byte)1
                : f.RequestedBy == characterId ? (byte)2 : (byte)3;
            members.Add(await ToMemberWireAsync(other, role, cancellationToken).ConfigureAwait(false));
        }

        await SendSnapshotAsync(
                client,
                new SocialSnapshotWire(SocialKind.Friend, Guid.Empty, Guid.Empty, string.Empty, members),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PushBlockSnapshotAsync(ClientSession client, Guid characterId, CancellationToken cancellationToken)
    {
        var list = await _store.ListBlocksAsync(characterId, cancellationToken).ConfigureAwait(false);
        var members = new List<SocialMemberWire>(list.Count);
        foreach (var b in list)
        {
            members.Add(await ToMemberWireAsync(b.BlockedCharacterId, 0, cancellationToken).ConfigureAwait(false));
        }

        await SendSnapshotAsync(
                client,
                new SocialSnapshotWire(SocialKind.Block, Guid.Empty, Guid.Empty, string.Empty, members),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SocialMemberWire> ToMemberWireAsync(Guid characterId, byte role, CancellationToken cancellationToken)
    {
        var record = await _characters.FindByIdAsync(characterId, cancellationToken).ConfigureAwait(false);
        var online = TryGetOnlineCharacter(characterId, out _);
        return new SocialMemberWire(characterId, role, online, record?.DisplayName ?? characterId.ToString("N")[..8]);
    }

    private async Task FanoutPresenceAsync(
        Guid characterId,
        SocialEventType type,
        string name,
        CancellationToken cancellationToken)
    {
        var targets = new HashSet<Guid>();
        var party = _parties.FindByCharacter(characterId);
        if (party is not null)
        {
            foreach (var m in party.Members)
            {
                targets.Add(m.CharacterId);
            }
        }

        foreach (var id in await GetGuildMemberIdsAsync(characterId, cancellationToken).ConfigureAwait(false))
        {
            targets.Add(id);
        }

        foreach (var f in await _store.ListFriendshipsAsync(characterId, cancellationToken).ConfigureAwait(false))
        {
            if (f.Status == FriendshipStatuses.Accepted)
            {
                targets.Add(f.CharacterA == characterId ? f.CharacterB : f.CharacterA);
            }
        }

        targets.Remove(characterId);
        var ev = new SocialEventWire(type, SocialKind.Friend, Guid.Empty, characterId, Guid.Empty, name);
        foreach (var id in targets)
        {
            await SendEventAsync(id, ev, cancellationToken).ConfigureAwait(false);
        }

        if (party is not null && TryGetClient(characterId, out var self) && self is not null)
        {
            await PushPartySnapshotAsync(self, characterId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendEventAsync(Guid characterId, SocialEventWire ev, CancellationToken cancellationToken)
    {
        if (!TryGetClient(characterId, out var client) || client is null)
        {
            return;
        }

        var payload = SocialWire.BuildEvent(ev);
        var frame = new byte[1 + payload.Length];
        frame[0] = (byte)PacketId.SocialEvent;
        payload.CopyTo(frame.AsSpan(1));
        await client.SendFrameAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    private Task SendSnapshotAsync(ClientSession client, SocialSnapshotWire snapshot, CancellationToken cancellationToken)
    {
        var payload = SocialWire.BuildSnapshot(snapshot);
        var frame = new byte[1 + payload.Length];
        frame[0] = (byte)PacketId.SocialSnapshot;
        payload.CopyTo(frame.AsSpan(1));
        return client.SendFrameAsync(frame, cancellationToken);
    }

    private bool TryGetOnlineCharacter(Guid characterId, out Session? session)
    {
        foreach (var s in _connections.GetActiveSessions())
        {
            if (s.CharacterGuid == characterId)
            {
                session = s;
                return true;
            }
        }

        session = null;
        return false;
    }

    private bool TryGetClient(Guid characterId, out ClientSession? client)
    {
        client = null;
        if (!TryGetOnlineCharacter(characterId, out var session) || session is null)
        {
            return false;
        }

        return _clients.TryGet(session.Id, out client);
    }

    private bool TryAllowInvite(Guid from, Guid to, SocialKind kind, DateTimeOffset now, out string error)
        => TryAllowInviteLocked(from, to, kind, now, out error);

    private bool TryAllowInviteLocked(Guid from, Guid to, SocialKind kind, DateTimeOffset now, out string error)
    {
        error = string.Empty;
        var key = from.ToString("N") + ":" + to.ToString("N") + ":" + (byte)kind;
        if (_reinviteCooldown.TryGetValue(key, out var until) && until > now)
        {
            error = "Patientez avant de renvoyer une invitation.";
            return false;
        }

        var q = _inviteRate.GetOrAdd(from, _ => new Queue<DateTimeOffset>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() > TimeSpan.FromMinutes(1))
            {
                q.Dequeue();
            }

            if (q.Count >= _options.InviteRatePerMinute)
            {
                error = "Trop d'invitations.";
                return false;
            }

            q.Enqueue(now);
        }

        return true;
    }

    private void MarkCooldown(Guid from, Guid to, SocialKind kind, DateTimeOffset now)
    {
        var key = from.ToString("N") + ":" + to.ToString("N") + ":" + (byte)kind;
        _reinviteCooldown[key] = now.AddSeconds(_options.ReinviteCooldownSeconds);
    }

    private async Task<bool> CheckPendingCapsAsync(Guid from, Guid to, CancellationToken cancellationToken)
    {
        var outgoing = _parties.CountPendingOutgoing(from)
                       + await _store.CountPendingOutgoingAsync(from, cancellationToken).ConfigureAwait(false);
        var incoming = _parties.CountPendingIncoming(to)
                       + await _store.CountPendingIncomingAsync(to, cancellationToken).ConfigureAwait(false);
        return outgoing < _options.MaxPendingOutgoing && incoming < _options.MaxPendingIncoming;
    }

    private bool TryGetReplay(Guid characterId, Guid requestId, out SocialResultWire result)
    {
        result = default;
        if (requestId == Guid.Empty)
        {
            return false;
        }

        if (_replays.TryGetValue(characterId, out var map) && map.TryGetValue(requestId, out result))
        {
            return true;
        }

        return false;
    }

    private SocialResultWire Remember(Guid characterId, SocialResultWire result)
    {
        if (result.RequestId == Guid.Empty)
        {
            return result;
        }

        var map = _replays.GetOrAdd(characterId, _ => new ConcurrentDictionary<Guid, SocialResultWire>());
        map[result.RequestId] = result;
        if (map.Count > 64)
        {
            foreach (var key in map.Keys.Take(16).ToArray())
            {
                map.TryRemove(key, out _);
            }
        }

        return result;
    }

    private static SocialResultWire Fail(SocialKind kind, byte action, Guid requestId, string message)
        => new(kind, action, requestId, false, message, Guid.Empty, Guid.Empty);

    private static SocialResultWire Ok(
        SocialKind kind,
        byte action,
        Guid requestId,
        string message,
        Guid subject,
        Guid other)
        => new(kind, action, requestId, true, message, subject, other);
}
