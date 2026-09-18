using System.Collections.Concurrent;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Social;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Config;
using Frog.Server.Models;
using Frog.Server.Network;
using Frog.Server.Services;
using Frog.Server.Social;
using Microsoft.Extensions.Options;

namespace Frog.Server.Trade;

public sealed class TradeService : ITradePresenceSink
{
    private readonly ISocialStore _social;
    private readonly ICharacterRepository _characters;
    private readonly IInventoryRepository _inventory;
    private readonly IPublishedItemCatalog _items;
    private readonly ITradeCommitRepository _commits;
    private readonly TradeHoldRegistry _holds;
    private readonly ConnectionManager _connections;
    private readonly ClientRegistry _clients;
    private readonly PacketSender _packets;
    private readonly CrossInviteCounters _invites;
    private readonly SocialOptions _options;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, TradeSessionState> _trades = new();
    private readonly ConcurrentDictionary<Guid, Guid> _byCharacter = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, TradeResultWire>> _replays = new();
    private readonly object _gate = new();

    public TradeService(
        ISocialStore social,
        ICharacterRepository characters,
        IInventoryRepository inventory,
        IPublishedItemCatalog items,
        ITradeCommitRepository commits,
        TradeHoldRegistry holds,
        ConnectionManager connections,
        ClientRegistry clients,
        PacketSender packets,
        CrossInviteCounters invites,
        IOptions<SocialOptions> options,
        TimeProvider? clock = null)
    {
        _social = social;
        _characters = characters;
        _inventory = inventory;
        _items = items;
        _commits = commits;
        _holds = holds;
        _connections = connections;
        _clients = clients;
        _packets = packets;
        _invites = invites;
        _options = options.Value;
        _clock = clock ?? TimeProvider.System;
        _invites.Register(
            "trade",
            id =>
            {
                lock (_gate)
                {
                    return CountPendingOutgoingLocked(id);
                }
            },
            id =>
            {
                lock (_gate)
                {
                    return CountPendingIncomingLocked(id);
                }
            });
    }

    public async Task<TradeResultWire> ExecuteAsync(
        Session session,
        byte action,
        Guid tradeId,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (session.CharacterGuid is not Guid actorId || actorId == Guid.Empty)
        {
            return Fail(action, tradeId, requestId, "Personnage actif requis.");
        }

        if (TryGetReplay(actorId, requestId, out var cached))
        {
            return cached;
        }

        var persisted = await _commits.TryReplayAsync(actorId, requestId, cancellationToken).ConfigureAwait(false);
        if (persisted is { Success: true })
        {
            return Remember(actorId, Ok(action, persisted.Initiator is null ? tradeId : tradeId, requestId, persisted.Message));
        }

        if (persisted is { Success: false })
        {
            return Remember(actorId, Fail(action, tradeId, requestId, persisted.Message));
        }

        if (!TradeWire.IsKnownAction(action))
        {
            return Remember(actorId, Fail(action, tradeId, requestId, "Action inconnue."));
        }

        SweepExpired();
        TradeResultWire result = (TradeAction)action switch
        {
            TradeAction.Invite => await InviteAsync(session, actorId, requestId, extra, cancellationToken)
                .ConfigureAwait(false),
            TradeAction.Accept => await AcceptAsync(session, actorId, tradeId, requestId, cancellationToken)
                .ConfigureAwait(false),
            TradeAction.Decline => await DeclineAsync(actorId, tradeId, requestId, cancellationToken)
                .ConfigureAwait(false),
            TradeAction.Cancel => await CancelAsync(actorId, tradeId, requestId, "Echange annule.", cancellationToken)
                .ConfigureAwait(false),
            TradeAction.SetOffer => await SetOfferAsync(session, actorId, tradeId, requestId, extra, cancellationToken)
                .ConfigureAwait(false),
            TradeAction.Confirm => await ConfirmAsync(session, actorId, tradeId, requestId, extra, cancellationToken)
                .ConfigureAwait(false),
            TradeAction.Unconfirm => await UnconfirmAsync(actorId, tradeId, requestId, cancellationToken)
                .ConfigureAwait(false),
            _ => Fail(action, tradeId, requestId, "Action inconnue.")
        };

        return Remember(actorId, result);
    }

    public Task NotifyCharacterOfflineAsync(Guid characterId, CancellationToken cancellationToken)
        => CancelForCharacterAsync(characterId, "Joueur deconnecte.", cancellationToken);

    public Task NotifyCharacterUnfitAsync(Guid characterId, string reason, CancellationToken cancellationToken)
        => CancelForCharacterAsync(characterId, reason, cancellationToken);

    public async Task NotifyMovementAsync(Guid characterId, CancellationToken cancellationToken)
    {
        Guid tradeId;
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out tradeId)
                || !_trades.TryGetValue(tradeId, out var trade)
                || trade.Status is TradeStatus.Committed or TradeStatus.Cancelled)
            {
                return;
            }
        }

        if (!await ParticipantsFitAsync(tradeId, cancellationToken).ConfigureAwait(false))
        {
            await CancelTradeAsync(tradeId, "Participants hors portee.", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<TradeResultWire> InviteAsync(
        Session session,
        Guid actorId,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (!TradeWire.TryReadGuid(extra.Span, out var targetId) || targetId == actorId)
        {
            return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Cible invalide.");
        }

        if (!TryGetOnline(targetId, out var targetSession) || targetSession is null || targetSession.IsDead)
        {
            return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Joueur hors ligne.");
        }

        if (session.IsDead)
        {
            return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Personnage mort.");
        }

        if (session.CurrentMapId != targetSession.CurrentMapId
            || !InRange(session, targetSession))
        {
            return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Trop loin.");
        }

        if (await _social.IsBlockedAsync(targetId, actorId, cancellationToken).ConfigureAwait(false)
            || await _social.IsBlockedAsync(actorId, targetId, cancellationToken).ConfigureAwait(false))
        {
            return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Vous etes bloque.");
        }

        var now = _clock.GetUtcNow();
        if (!_invites.TryAllowInvite(
                actorId,
                targetId,
                "trade",
                now,
                _options.InviteRatePerMinute,
                _options.ReinviteCooldownSeconds,
                out var rateErr))
        {
            return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, rateErr);
        }

        var pendingOutStore = await _social.CountPendingOutgoingAsync(actorId, cancellationToken).ConfigureAwait(false);
        var pendingInStore = await _social.CountPendingIncomingAsync(targetId, cancellationToken).ConfigureAwait(false);
        TradeSessionState created;
        lock (_gate)
        {
            if (_byCharacter.ContainsKey(actorId) || _byCharacter.ContainsKey(targetId))
            {
                return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Echange deja en cours.");
            }

            var outgoing = _invites.MemoryOutgoing(actorId) + pendingOutStore;
            var incoming = _invites.MemoryIncoming(targetId) + pendingInStore;
            if (outgoing >= _options.MaxPendingOutgoing)
            {
                return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Trop d'invitations sortantes.");
            }

            if (incoming >= _options.MaxPendingIncoming)
            {
                return Fail((byte)TradeAction.Invite, Guid.Empty, requestId, "Trop d'invitations entrantes pour la cible.");
            }

            created = new TradeSessionState
            {
                Id = Guid.NewGuid(),
                InitiatorId = actorId,
                PartnerId = targetId,
                InitiatorName = session.Username,
                PartnerName = targetSession.Username,
                Status = TradeStatus.Inviting,
                InviteExpiresAt = now.AddSeconds(SocialProtocolLimits.TradeInviteSecondsDefault),
                LastOfferMutationAt = now,
            };
            _trades[created.Id] = created;
            _byCharacter[actorId] = created.Id;
            _byCharacter[targetId] = created.Id;
        }

        await PushSnapshotAsync(created, cancellationToken).ConfigureAwait(false);
        return Ok((byte)TradeAction.Invite, created.Id, requestId, "Invitation envoyee.");
    }

    private async Task<TradeResultWire> AcceptAsync(
        Session session,
        Guid actorId,
        Guid tradeId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        TradeSessionState? trade;
        lock (_gate)
        {
            if (!TryGetTradeLocked(tradeId, out trade) || trade is null)
            {
                return Fail((byte)TradeAction.Accept, tradeId, requestId, "Echange introuvable.");
            }

            if (trade.Status != TradeStatus.Inviting || trade.PartnerId != actorId)
            {
                return Fail((byte)TradeAction.Accept, tradeId, requestId, "Invitation invalide ou expiree.");
            }

            if (session.IsDead)
            {
                return Fail((byte)TradeAction.Accept, tradeId, requestId, "Personnage mort.");
            }

            trade.Status = TradeStatus.Open;
            trade.LastOfferMutationAt = _clock.GetUtcNow();
        }

        if (!await ParticipantsFitAsync(tradeId, cancellationToken).ConfigureAwait(false))
        {
            await CancelTradeAsync(tradeId, "Participants hors portee.", cancellationToken).ConfigureAwait(false);
            return Fail((byte)TradeAction.Accept, tradeId, requestId, "Trop loin.");
        }

        await PushSnapshotAsync(trade, cancellationToken).ConfigureAwait(false);
        return Ok((byte)TradeAction.Accept, tradeId, requestId, "Echange accepte.");
    }

    private async Task<TradeResultWire> DeclineAsync(
        Guid actorId,
        Guid tradeId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!TryGetTradeLocked(tradeId, out var trade) || trade is null)
            {
                return Fail((byte)TradeAction.Decline, tradeId, requestId, "Echange introuvable.");
            }

            if (trade.Status != TradeStatus.Inviting || (trade.PartnerId != actorId && trade.InitiatorId != actorId))
            {
                return Fail((byte)TradeAction.Decline, tradeId, requestId, "Invitation invalide ou expiree.");
            }

            _invites.MarkCooldown(trade.InitiatorId, trade.PartnerId, "trade", _clock.GetUtcNow(), _options.ReinviteCooldownSeconds);
        }

        await CancelTradeAsync(tradeId, "Invitation refusee.", cancellationToken).ConfigureAwait(false);
        return Ok((byte)TradeAction.Decline, tradeId, requestId, "Invitation refusee.");
    }

    private async Task<TradeResultWire> CancelAsync(
        Guid actorId,
        Guid tradeId,
        Guid requestId,
        string message,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!TryGetTradeLocked(tradeId, out var trade) || trade is null)
            {
                return Fail((byte)TradeAction.Cancel, tradeId, requestId, "Echange introuvable.");
            }

            if (trade.InitiatorId != actorId && trade.PartnerId != actorId)
            {
                return Fail((byte)TradeAction.Cancel, tradeId, requestId, "Echange introuvable.");
            }

            if (trade.Status is TradeStatus.Committed)
            {
                return Fail((byte)TradeAction.Cancel, tradeId, requestId, "Echange deja valide.");
            }
        }

        await CancelTradeAsync(tradeId, message, cancellationToken).ConfigureAwait(false);
        return Ok((byte)TradeAction.Cancel, tradeId, requestId, message);
    }

    private async Task<TradeResultWire> SetOfferAsync(
        Session session,
        Guid actorId,
        Guid tradeId,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (!TradeWire.TryReadSetOffer(extra.Span, out var revisionBase, out var gold, out var stacks))
        {
            return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Offre invalide.");
        }

        if (session.IsDead)
        {
            return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Personnage mort.");
        }

        if (!await ParticipantsFitAsync(tradeId, cancellationToken).ConfigureAwait(false))
        {
            await CancelTradeAsync(tradeId, "Participants hors portee.", cancellationToken).ConfigureAwait(false);
            return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Trop loin.");
        }

        var record = await _characters.FindByIdAsync(actorId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Personnage introuvable.");
        }

        if (gold > record.Gold)
        {
            return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Or insuffisant.");
        }

        var inv = await _inventory.GetAsync(actorId, cancellationToken).ConfigureAwait(false);
        var offers = stacks.Select(s => new TradeStackOffer(s.ItemId, s.Quantity)).ToArray();
        if (!TradeInventoryMath.TryAllocateSlots(inv, offers, out var slotHolds))
        {
            return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Objets insuffisants.");
        }

        TradeSessionState? trade;
        lock (_gate)
        {
            if (!TryGetTradeLocked(tradeId, out trade) || trade is null || trade.Status != TradeStatus.Open)
            {
                return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Echange introuvable.");
            }

            if (trade.InitiatorId != actorId && trade.PartnerId != actorId)
            {
                return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Echange introuvable.");
            }

            if (revisionBase != trade.Revision)
            {
                return Fail((byte)TradeAction.SetOffer, tradeId, requestId, "Revision incorrecte.");
            }

            if (actorId == trade.InitiatorId)
            {
                trade.InitiatorGold = gold;
                trade.InitiatorItems = offers;
            }
            else
            {
                trade.PartnerGold = gold;
                trade.PartnerItems = offers;
            }

            trade.Revision++;
            trade.InitiatorConfirmed = false;
            trade.PartnerConfirmed = false;
            trade.LastOfferMutationAt = _clock.GetUtcNow();
        }

        _holds.Replace(actorId, tradeId, gold, slotHolds);
        await PushSnapshotAsync(trade, cancellationToken).ConfigureAwait(false);
        return Ok((byte)TradeAction.SetOffer, tradeId, requestId, "Offre mise a jour.");
    }

    private async Task<TradeResultWire> ConfirmAsync(
        Session session,
        Guid actorId,
        Guid tradeId,
        Guid requestId,
        ReadOnlyMemory<byte> extra,
        CancellationToken cancellationToken)
    {
        if (!TradeWire.TryReadRevision(extra.Span, out var revision))
        {
            return Fail((byte)TradeAction.Confirm, tradeId, requestId, "Revision incorrecte.");
        }

        if (session.IsDead)
        {
            return Fail((byte)TradeAction.Confirm, tradeId, requestId, "Personnage mort.");
        }

        if (!await ParticipantsFitAsync(tradeId, cancellationToken).ConfigureAwait(false))
        {
            await CancelTradeAsync(tradeId, "Participants hors portee.", cancellationToken).ConfigureAwait(false);
            return Fail((byte)TradeAction.Confirm, tradeId, requestId, "Trop loin.");
        }

        TradeSessionState trade;
        bool bothConfirmed;
        lock (_gate)
        {
            if (!TryGetTradeLocked(tradeId, out var found) || found is null || found.Status != TradeStatus.Open)
            {
                return Fail((byte)TradeAction.Confirm, tradeId, requestId, "Echange introuvable.");
            }

            trade = found;
            if (trade.InitiatorId != actorId && trade.PartnerId != actorId)
            {
                return Fail((byte)TradeAction.Confirm, tradeId, requestId, "Echange introuvable.");
            }

            if (revision != trade.Revision)
            {
                return Fail((byte)TradeAction.Confirm, tradeId, requestId, "Revision incorrecte.");
            }

            if (actorId == trade.InitiatorId)
            {
                trade.InitiatorConfirmed = true;
            }
            else
            {
                trade.PartnerConfirmed = true;
            }

            bothConfirmed = trade.InitiatorConfirmed && trade.PartnerConfirmed;
        }

        if (!bothConfirmed)
        {
            await PushSnapshotAsync(trade, cancellationToken).ConfigureAwait(false);
            return Ok((byte)TradeAction.Confirm, tradeId, requestId, "Confirmation enregistree.");
        }

        TradeCommitResult commit;
        try
        {
            commit = await _commits.TryCommitAsync(
                    trade.Id,
                    requestId,
                    trade.InitiatorId,
                    trade.PartnerId,
                    trade.InitiatorGold,
                    trade.PartnerGold,
                    trade.InitiatorItems,
                    trade.PartnerItems,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Fail((byte)TradeAction.Confirm, tradeId, requestId, "Echec de l'echange.");
        }

        if (!commit.Success)
        {
            return Fail((byte)TradeAction.Confirm, tradeId, requestId, commit.Message);
        }

        lock (_gate)
        {
            trade.Status = TradeStatus.Committed;
            trade.CommitRequestId = requestId;
            DetachLocked(trade);
        }

        _holds.Release(trade.InitiatorId, trade.Id);
        _holds.Release(trade.PartnerId, trade.Id);
        ApplyGoldToSession(trade.InitiatorId, commit.Initiator?.Gold);
        ApplyGoldToSession(trade.PartnerId, commit.Partner?.Gold);
        await PushSnapshotAsync(trade, cancellationToken).ConfigureAwait(false);
        await PushEconomyAsync(trade.InitiatorId, commit.Initiator, cancellationToken).ConfigureAwait(false);
        await PushEconomyAsync(trade.PartnerId, commit.Partner, cancellationToken).ConfigureAwait(false);
        var result = Ok((byte)TradeAction.Confirm, tradeId, requestId, commit.Message);
        Remember(trade.InitiatorId, result);
        Remember(trade.PartnerId, result);
        return result;
    }

    private async Task<TradeResultWire> UnconfirmAsync(
        Guid actorId,
        Guid tradeId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        TradeSessionState? trade;
        lock (_gate)
        {
            if (!TryGetTradeLocked(tradeId, out trade) || trade is null || trade.Status != TradeStatus.Open)
            {
                return Fail((byte)TradeAction.Unconfirm, tradeId, requestId, "Echange introuvable.");
            }

            if (actorId == trade.InitiatorId)
            {
                trade.InitiatorConfirmed = false;
            }
            else if (actorId == trade.PartnerId)
            {
                trade.PartnerConfirmed = false;
            }
            else
            {
                return Fail((byte)TradeAction.Unconfirm, tradeId, requestId, "Echange introuvable.");
            }
        }

        await PushSnapshotAsync(trade, cancellationToken).ConfigureAwait(false);
        return Ok((byte)TradeAction.Unconfirm, tradeId, requestId, "Confirmation retiree.");
    }

    private async Task CancelForCharacterAsync(Guid characterId, string reason, CancellationToken cancellationToken)
    {
        Guid tradeId;
        lock (_gate)
        {
            if (!_byCharacter.TryGetValue(characterId, out tradeId))
            {
                return;
            }
        }

        await CancelTradeAsync(tradeId, reason, cancellationToken).ConfigureAwait(false);
    }

    private async Task CancelTradeAsync(Guid tradeId, string reason, CancellationToken cancellationToken)
    {
        TradeSessionState? trade;
        lock (_gate)
        {
            if (!_trades.TryGetValue(tradeId, out trade)
                || trade.Status is TradeStatus.Committed or TradeStatus.Cancelled)
            {
                return;
            }

            trade.Status = TradeStatus.Cancelled;
            DetachLocked(trade);
        }

        _holds.Release(trade.InitiatorId, trade.Id);
        _holds.Release(trade.PartnerId, trade.Id);
        await PushSnapshotAsync(trade, cancellationToken).ConfigureAwait(false);
        _ = reason;
    }

    private void SweepExpired()
    {
        var now = _clock.GetUtcNow();
        List<Guid> expired = [];
        lock (_gate)
        {
            foreach (var trade in _trades.Values)
            {
                if (trade.Status == TradeStatus.Inviting && now >= trade.InviteExpiresAt)
                {
                    expired.Add(trade.Id);
                }
                else if (trade.Status == TradeStatus.Open
                         && now - trade.LastOfferMutationAt
                         >= TimeSpan.FromSeconds(SocialProtocolLimits.TradeIdleSecondsDefault))
                {
                    expired.Add(trade.Id);
                }
            }
        }

        foreach (var id in expired)
        {
            CancelTradeAsync(id, "Echange expire.", CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    private Task<bool> ParticipantsFitAsync(Guid tradeId, CancellationToken cancellationToken)
    {
        TradeSessionState? trade;
        lock (_gate)
        {
            if (!TryGetTradeLocked(tradeId, out trade) || trade is null)
            {
                return Task.FromResult(false);
            }
        }

        if (!TryGetOnline(trade.InitiatorId, out var a) || a is null
            || !TryGetOnline(trade.PartnerId, out var b) || b is null)
        {
            return Task.FromResult(false);
        }

        if (a.IsDead || b.IsDead || a.CurrentMapId != b.CurrentMapId || !InRange(a, b))
        {
            return Task.FromResult(false);
        }

        _ = cancellationToken;
        return Task.FromResult(true);
    }

    private static bool InRange(Session a, Session b)
    {
        var max = WorldMetrics.TradeRangePixels;
        return WorldMetrics.DistanceSquaredPixels(a.PixelX, a.PixelY, b.PixelX, b.PixelY) <= max * max;
    }

    private int CountPendingOutgoingLocked(Guid characterId)
    {
        if (!_byCharacter.TryGetValue(characterId, out var tradeId)
            || !_trades.TryGetValue(tradeId, out var trade)
            || trade.Status != TradeStatus.Inviting
            || trade.InitiatorId != characterId)
        {
            return 0;
        }

        return 1;
    }

    private int CountPendingIncomingLocked(Guid characterId)
    {
        if (!_byCharacter.TryGetValue(characterId, out var tradeId)
            || !_trades.TryGetValue(tradeId, out var trade)
            || trade.Status != TradeStatus.Inviting
            || trade.PartnerId != characterId)
        {
            return 0;
        }

        return 1;
    }

    private bool TryGetTradeLocked(Guid tradeId, out TradeSessionState? trade)
        => _trades.TryGetValue(tradeId, out trade);

    private void DetachLocked(TradeSessionState trade)
    {
        _byCharacter.TryRemove(trade.InitiatorId, out _);
        _byCharacter.TryRemove(trade.PartnerId, out _);
    }

    private bool TryGetOnline(Guid characterId, out Session? session)
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
        return TryGetOnline(characterId, out var session)
               && session is not null
               && _clients.TryGet(session.Id, out client);
    }

    private void ApplyGoldToSession(Guid characterId, int? gold)
    {
        if (gold is null || !TryGetOnline(characterId, out var session) || session is null)
        {
            return;
        }

        session.Gold = gold.Value;
    }

    private async Task PushSnapshotAsync(TradeSessionState trade, CancellationToken cancellationToken)
    {
        var snapshot = await ToSnapshotAsync(trade, cancellationToken).ConfigureAwait(false);
        var body = TradeWire.BuildSnapshot(snapshot);
        var frame = new byte[1 + body.Length];
        frame[0] = (byte)PacketId.TradeSnapshot;
        body.CopyTo(frame.AsSpan(1));
        foreach (var id in new[] { trade.InitiatorId, trade.PartnerId })
        {
            if (TryGetClient(id, out var client) && client is not null)
            {
                await client.SendFrameAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task PushEconomyAsync(
        Guid characterId,
        TradeCommitPartyState? state,
        CancellationToken cancellationToken)
    {
        if (state is null || !TryGetOnline(characterId, out var session) || session is null)
        {
            return;
        }

        if (!TryGetClient(characterId, out var client) || client is null)
        {
            return;
        }

        var wire = new InventorySnapshotWire
        {
            EquippedWeaponItemId = session.EquippedWeaponItemId,
            EquippedArmorItemId = session.EquippedArmorItemId,
            Slots = state.Inventory.Slots.Select(s => new InventorySlotWire
            {
                SlotIndex = s.SlotIndex,
                ItemId = s.ItemId,
                Quantity = s.Quantity,
            }).ToArray(),
        };
        await _packets.SendInventorySnapshotAsync(client, wire, cancellationToken).ConfigureAwait(false);
        await _packets.SendCombatStateAsync(
                client,
                session.Level,
                session.Experience,
                session.Hp,
                session.MaxHp,
                session.Mp,
                session.MaxMp,
                session.Gold,
                session.IsDead,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TradeSnapshotWire> ToSnapshotAsync(TradeSessionState trade, CancellationToken cancellationToken)
    {
        async Task<IReadOnlyList<TradeStackWire>> Named(IReadOnlyList<TradeStackOffer> items)
        {
            var list = new List<TradeStackWire>(items.Count);
            foreach (var item in items)
            {
                var def = await _items.LoadPublishedByIdAsync(item.ItemId, cancellationToken).ConfigureAwait(false);
                list.Add(new TradeStackWire(item.ItemId, item.Quantity, def?.Name ?? item.ItemId.ToString("N")[..8]));
            }

            return list;
        }

        return new TradeSnapshotWire(
            trade.Id,
            trade.Revision,
            trade.Status,
            trade.InitiatorId,
            trade.PartnerId,
            trade.InitiatorConfirmed,
            trade.PartnerConfirmed,
            trade.InitiatorName,
            trade.PartnerName,
            new TradeOfferWire(trade.InitiatorGold, await Named(trade.InitiatorItems).ConfigureAwait(false)),
            new TradeOfferWire(trade.PartnerGold, await Named(trade.PartnerItems).ConfigureAwait(false)));
    }

    private bool TryGetReplay(Guid characterId, Guid requestId, out TradeResultWire result)
    {
        result = default;
        return requestId != Guid.Empty
               && _replays.TryGetValue(characterId, out var map)
               && map.TryGetValue(requestId, out result);
    }

    private TradeResultWire Remember(Guid characterId, TradeResultWire result)
    {
        if (result.RequestId == Guid.Empty)
        {
            return result;
        }

        var map = _replays.GetOrAdd(characterId, _ => new ConcurrentDictionary<Guid, TradeResultWire>());
        map[result.RequestId] = result;
        return result;
    }

    private static TradeResultWire Fail(byte action, Guid tradeId, Guid requestId, string message)
        => new(action, tradeId, requestId, false, message);

    private static TradeResultWire Ok(byte action, Guid tradeId, Guid requestId, string message)
        => new(action, tradeId, requestId, true, message);

    private sealed class TradeSessionState
    {
        public Guid Id { get; init; }
        public Guid InitiatorId { get; init; }
        public Guid PartnerId { get; init; }
        public string InitiatorName { get; set; } = string.Empty;
        public string PartnerName { get; set; } = string.Empty;
        public TradeStatus Status { get; set; }
        public DateTimeOffset InviteExpiresAt { get; set; }
        public DateTimeOffset LastOfferMutationAt { get; set; }
        public uint Revision { get; set; }
        public int InitiatorGold { get; set; }
        public int PartnerGold { get; set; }
        public IReadOnlyList<TradeStackOffer> InitiatorItems { get; set; } = Array.Empty<TradeStackOffer>();
        public IReadOnlyList<TradeStackOffer> PartnerItems { get; set; } = Array.Empty<TradeStackOffer>();
        public bool InitiatorConfirmed { get; set; }
        public bool PartnerConfirmed { get; set; }
        public Guid? CommitRequestId { get; set; }
    }
}
