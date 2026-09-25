using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Core.Trade;

public enum TradeUiPhase
{
    Idle = 0,
    OutgoingInvite = 1,
    IncomingInvite = 2,
    Negotiating = 3,
    Completed = 4,
    Cancelled = 5,
}

public sealed record TradeBagEntry(Guid ItemId, int Quantity, string DisplayName);

/// <summary>
/// État d'échange côté client : invitation, emplacements, confirmation en deux temps.
/// Le fil (Hello 11, opcodes 84–86) ne change pas.
/// </summary>
public sealed class ClientTradeSession
{
    private readonly List<TradeStackWire> _draft = [];
    private readonly List<TradeBagEntry> _bag = [];
    private TradeSnapshotWire? _snapshot;
    private bool _draftDirty;
    private bool _walletKnown;
    private int _ownedGold;
    private uint _seenRevision;
    private bool _hasSnapshot;
    private string? _lastToast;
    private string? _pendingToast;

    public Guid LocalCharacterId { get; private set; }

    public TradeUiPhase Phase { get; private set; }

    public string Headline { get; private set; } = "Échange";

    public string StatusLine { get; private set; } = "Aucun échange.";

    public bool AcceptEnabled { get; private set; }

    public bool DeclineEnabled { get; private set; }

    public bool ConfirmEnabled { get; private set; }

    public bool ConfirmArmed { get; private set; }

    public string ConfirmLabel { get; private set; } = "Confirmer";

    public bool UnconfirmEnabled { get; private set; }

    public bool CancelEnabled { get; private set; }

    public bool CancelArmed { get; private set; }

    public string CancelLabel { get; private set; } = "Annuler";

    public bool OfferEditorEnabled { get; private set; }

    public bool ProposeEnabled { get; private set; }

    public int DraftGold { get; private set; }

    public IReadOnlyList<TradeStackWire> DraftStacks => _draft;

    public TradeOfferWire MyOffer { get; private set; }

    public TradeOfferWire TheirOffer { get; private set; }

    public string MyTitle { get; private set; } = "Vous";

    public string TheirTitle { get; private set; } = "Partenaire";

    public string MyGoldLabel { get; private set; } = "Or : 0";

    public string TheirGoldLabel { get; private set; } = "Or : 0";

    public bool SelfConfirmed { get; private set; }

    public bool OtherConfirmed { get; private set; }

    public uint DisplayedRevision { get; private set; }

    public Guid? ActiveTradeId =>
        Phase is TradeUiPhase.OutgoingInvite or TradeUiPhase.IncomingInvite or TradeUiPhase.Negotiating
            && _snapshot is { } snap
            ? snap.TradeId
            : null;

    public void SetLocalCharacter(Guid characterId)
    {
        if (LocalCharacterId == characterId)
        {
            return;
        }

        LocalCharacterId = characterId;
        if (_snapshot is { } snap)
        {
            ApplySnapshot(snap, announce: false);
        }
    }

    public void SetWallet(int gold)
    {
        _walletKnown = true;
        _ownedGold = Math.Max(0, gold);
        RefreshChrome();
    }

    public void SetBag(IReadOnlyList<TradeBagEntry>? bag)
    {
        _bag.Clear();
        if (bag is not null)
        {
            foreach (var entry in bag)
            {
                if (entry.ItemId == Guid.Empty || entry.Quantity <= 0)
                {
                    continue;
                }

                var index = _bag.FindIndex(b => b.ItemId == entry.ItemId);
                if (index >= 0)
                {
                    var prev = _bag[index];
                    _bag[index] = prev with { Quantity = prev.Quantity + entry.Quantity };
                }
                else
                {
                    _bag.Add(entry);
                }
            }
        }

        RefreshChrome();
    }

    public IReadOnlyList<TradeBagEntry> Bag => _bag;

    public void ApplySnapshot(TradeSnapshotWire snapshot)
        => ApplySnapshot(snapshot, announce: true);

    public void ApplyResult(TradeResultWire result)
    {
        if (string.IsNullOrWhiteSpace(result.Message))
        {
            return;
        }

        StatusLine = TradePlayerMessages.Present(result.Message);
        RememberToast(StatusLine);
    }

    /// <summary>Déconnexion locale : le partenaire verra l'annulation côté serveur.</summary>
    public string? NotifyLocalDisconnect()
    {
        if (Phase is TradeUiPhase.Idle or TradeUiPhase.Completed or TradeUiPhase.Cancelled)
        {
            return null;
        }

        Phase = TradeUiPhase.Cancelled;
        AcceptEnabled = false;
        DeclineEnabled = false;
        ConfirmEnabled = false;
        ConfirmArmed = false;
        UnconfirmEnabled = false;
        CancelEnabled = false;
        CancelArmed = false;
        OfferEditorEnabled = false;
        ProposeEnabled = false;
        StatusLine = "Échange annulé : connexion interrompue.";
        Headline = "Échange annulé";
        RememberToast(StatusLine);
        return StatusLine;
    }

    public string? ConsumeToast()
    {
        var toast = _pendingToast;
        _pendingToast = null;
        return toast;
    }

    public bool TryConfirm(out string? blockedReason)
    {
        blockedReason = null;
        if (Phase != TradeUiPhase.Negotiating || _snapshot is not { } snap)
        {
            blockedReason = "Aucun échange à confirmer.";
            StatusLine = blockedReason;
            RememberToast(blockedReason);
            return false;
        }

        if (LocalConfirmed(snap))
        {
            blockedReason = "Vous avez déjà confirmé. En attente du partenaire.";
            StatusLine = blockedReason;
            RememberToast(blockedReason);
            return false;
        }

        if (_draftDirty)
        {
            blockedReason = "Proposez l'offre avant de confirmer.";
            StatusLine = blockedReason;
            RememberToast(blockedReason);
            return false;
        }

        if (IsEmpty(MyOffer) && IsEmpty(TheirOffer))
        {
            blockedReason = "Ajoutez de l'or ou un objet avant de confirmer.";
            StatusLine = blockedReason;
            RememberToast(blockedReason);
            return false;
        }

        if (!ConfirmArmed)
        {
            ConfirmArmed = true;
            CancelArmed = false;
            blockedReason = "Vérifiez les deux offres, puis confirmez à nouveau.";
            StatusLine = blockedReason;
            RefreshChrome();
            RememberToast(blockedReason);
            return false;
        }

        ConfirmArmed = false;
        StatusLine = "Confirmation envoyée…";
        RefreshChrome();
        return true;
    }

    public bool TryCancel(out string? blockedReason)
    {
        blockedReason = null;
        if (Phase is TradeUiPhase.Idle or TradeUiPhase.Completed or TradeUiPhase.Cancelled
            || _snapshot is null)
        {
            blockedReason = "Aucun échange à annuler.";
            StatusLine = blockedReason;
            return false;
        }

        if (Phase == TradeUiPhase.IncomingInvite)
        {
            blockedReason = "Refusez l'invitation.";
            StatusLine = blockedReason;
            return false;
        }

        var valuable = Phase == TradeUiPhase.Negotiating && (!IsEmpty(MyOffer) || !IsEmpty(TheirOffer));
        if (valuable && !CancelArmed)
        {
            CancelArmed = true;
            ConfirmArmed = false;
            blockedReason = "Les offres ne sont pas vides. Annulez à nouveau pour tout abandonner.";
            StatusLine = blockedReason;
            RefreshChrome();
            RememberToast(blockedReason);
            return false;
        }

        CancelArmed = false;
        StatusLine = "Annulation envoyée…";
        RefreshChrome();
        return true;
    }

    public bool TrySetDraftGold(int gold, out string? error)
    {
        error = null;
        if (!OfferEditorEnabled)
        {
            error = "Retirez votre confirmation pour modifier l'offre.";
            return false;
        }

        if (gold < 0)
        {
            error = "Or invalide.";
            return false;
        }

        if (_walletKnown && gold > _ownedGold)
        {
            error = "Or insuffisant.";
            StatusLine = TradePlayerMessages.Present(error);
            RememberToast(StatusLine);
            return false;
        }

        DraftGold = gold;
        _draftDirty = !DraftMatchesServer();
        ConfirmArmed = false;
        CancelArmed = false;
        RefreshChrome();
        return true;
    }

    public bool TryAddDraftStack(Guid itemId, int quantity, out string? error)
    {
        error = null;
        if (!OfferEditorEnabled)
        {
            error = "Retirez votre confirmation pour modifier l'offre.";
            return false;
        }

        if (itemId == Guid.Empty || quantity <= 0)
        {
            error = "Objet invalide.";
            return false;
        }

        var owned = OwnedQuantity(itemId);
        if (owned <= 0)
        {
            error = "Objets insuffisants.";
            StatusLine = TradePlayerMessages.Present(error);
            RememberToast(StatusLine);
            return false;
        }

        var already = DraftQuantity(itemId);
        if (already + quantity > owned)
        {
            error = "Objets insuffisants.";
            StatusLine = TradePlayerMessages.Present(error);
            RememberToast(StatusLine);
            return false;
        }

        var index = _draft.FindIndex(s => s.ItemId == itemId);
        if (index >= 0)
        {
            var prev = _draft[index];
            _draft[index] = prev with { Quantity = prev.Quantity + quantity };
        }
        else
        {
            if (_draft.Count >= SocialProtocolLimits.TradeMaxStacksPerSide)
            {
                error = "Huit emplacements maximum.";
                StatusLine = error;
                RememberToast(error);
                return false;
            }

            var name = _bag.FirstOrDefault(b => b.ItemId == itemId)?.DisplayName ?? string.Empty;
            _draft.Add(new TradeStackWire(itemId, quantity, name));
        }

        _draftDirty = !DraftMatchesServer();
        ConfirmArmed = false;
        CancelArmed = false;
        RefreshChrome();
        return true;
    }

    public bool TryRemoveDraftStack(int index)
    {
        if (!OfferEditorEnabled || index < 0 || index >= _draft.Count)
        {
            return false;
        }

        _draft.RemoveAt(index);
        _draftDirty = !DraftMatchesServer();
        ConfirmArmed = false;
        CancelArmed = false;
        RefreshChrome();
        return true;
    }

    public bool TryBuildSetOffer(
        out uint revision,
        out int gold,
        out IReadOnlyList<TradeStackWire> stacks,
        out string? error)
    {
        revision = _snapshot?.Revision ?? 0;
        gold = DraftGold;
        stacks = _draft.ToArray();
        error = null;
        if (Phase != TradeUiPhase.Negotiating || _snapshot is null)
        {
            error = "Aucun échange ouvert.";
            return false;
        }

        if (!OfferEditorEnabled)
        {
            error = "Retirez votre confirmation pour modifier l'offre.";
            StatusLine = error;
            RememberToast(error);
            return false;
        }

        if (!_draftDirty)
        {
            error = "L'offre est déjà envoyée.";
            StatusLine = error;
            return false;
        }

        if (_walletKnown && DraftGold > _ownedGold)
        {
            error = "Or insuffisant.";
            StatusLine = TradePlayerMessages.Present(error);
            RememberToast(StatusLine);
            return false;
        }

        return true;
    }

    public static IReadOnlyList<string> FormatSlots(TradeOfferWire offer)
    {
        var stacks = offer.Stacks ?? Array.Empty<TradeStackWire>();
        var lines = new string[SocialProtocolLimits.TradeMaxStacksPerSide];
        for (var i = 0; i < lines.Length; i++)
        {
            if (i < stacks.Count)
            {
                var stack = stacks[i];
                var name = string.IsNullOrWhiteSpace(stack.DisplayName)
                    ? stack.ItemId.ToString("N")[..8]
                    : stack.DisplayName;
                lines[i] = $"{i + 1}. {name} × {stack.Quantity}";
            }
            else
            {
                lines[i] = $"{i + 1}. — libre";
            }
        }

        return lines;
    }

    private void ApplySnapshot(TradeSnapshotWire snapshot, bool announce)
    {
        var previousPhase = Phase;
        var previousMy = MyOffer;
        _snapshot = snapshot;
        DisplayedRevision = snapshot.Revision;
        AssignSides(snapshot);
        Phase = ResolvePhase(snapshot);
        var revisionChanged = _hasSnapshot && snapshot.Revision != _seenRevision;
        _seenRevision = snapshot.Revision;
        _hasSnapshot = true;
        if (revisionChanged)
        {
            ConfirmArmed = false;
            CancelArmed = false;
        }

        var serverCaughtUp = OffersEqual(previousMy, MyOffer) == false && DraftMatchesServer();
        if (!_draftDirty || serverCaughtUp || OffersEqual(previousMy, MyOffer) == false && Phase != TradeUiPhase.Negotiating)
        {
            CopyDraftFrom(MyOffer);
            _draftDirty = false;
        }
        else if (OffersEqual(previousMy, MyOffer))
        {
            _draftDirty = !DraftMatchesServer();
        }

        if (Phase != TradeUiPhase.Negotiating)
        {
            _draftDirty = false;
            CopyDraftFrom(MyOffer);
        }

        RefreshChrome();
        if (!announce)
        {
            return;
        }

        if (Phase == TradeUiPhase.IncomingInvite && previousPhase != TradeUiPhase.IncomingInvite)
        {
            var who = string.IsNullOrWhiteSpace(TheirTitle) ? "Un joueur" : TheirTitle;
            StatusLine = who + " vous invite à échanger.";
            RememberToast(StatusLine);
        }
        else if (Phase == TradeUiPhase.Negotiating && previousPhase == TradeUiPhase.OutgoingInvite)
        {
            StatusLine = "Échange accepté.";
            RememberToast(StatusLine);
        }
        else if (Phase == TradeUiPhase.Completed)
        {
            StatusLine = "Échange validé.";
            RememberToast(StatusLine);
        }
        else if (Phase == TradeUiPhase.Cancelled)
        {
            StatusLine = string.IsNullOrWhiteSpace(snapshot.Notice)
                ? "Échange annulé."
                : TradePlayerMessages.Present(snapshot.Notice);
            RememberToast(StatusLine);
        }
        else if (!string.IsNullOrWhiteSpace(snapshot.Notice))
        {
            StatusLine = TradePlayerMessages.Present(snapshot.Notice);
            RememberToast(StatusLine);
        }
        else if (revisionChanged && Phase == TradeUiPhase.Negotiating && previousPhase == TradeUiPhase.Negotiating)
        {
            StatusLine = ConfirmArmed
                ? StatusLine
                : "L'offre a changé. Les confirmations sont retirées.";
        }
    }

    private void AssignSides(TradeSnapshotWire snapshot)
    {
        var legacy = LocalCharacterId == Guid.Empty
                     || (LocalCharacterId != snapshot.InitiatorId && LocalCharacterId != snapshot.PartnerId);
        var mineIsInitiator = legacy || LocalCharacterId == snapshot.InitiatorId;
        if (mineIsInitiator)
        {
            MyOffer = snapshot.InitiatorOffer;
            TheirOffer = snapshot.PartnerOffer;
            MyTitle = legacy ? snapshot.InitiatorName : "Vous";
            TheirTitle = snapshot.PartnerName;
        }
        else
        {
            MyOffer = snapshot.PartnerOffer;
            TheirOffer = snapshot.InitiatorOffer;
            MyTitle = "Vous";
            TheirTitle = snapshot.InitiatorName;
        }

        if (string.IsNullOrWhiteSpace(TheirTitle))
        {
            TheirTitle = "Partenaire";
        }
    }

    private TradeUiPhase ResolvePhase(TradeSnapshotWire snapshot)
    {
        switch (snapshot.Status)
        {
            case TradeStatus.Committed:
                return TradeUiPhase.Completed;
            case TradeStatus.Cancelled:
                return TradeUiPhase.Cancelled;
            case TradeStatus.Inviting:
                if (LocalCharacterId != Guid.Empty && LocalCharacterId == snapshot.PartnerId)
                {
                    return TradeUiPhase.IncomingInvite;
                }

                return TradeUiPhase.OutgoingInvite;
            case TradeStatus.Open:
                return TradeUiPhase.Negotiating;
            default:
                return TradeUiPhase.Idle;
        }
    }

    private void RefreshChrome()
    {
        var snap = _snapshot;
        AcceptEnabled = Phase == TradeUiPhase.IncomingInvite;
        DeclineEnabled = Phase == TradeUiPhase.IncomingInvite;
        var localConfirmed = snap is { } s && LocalConfirmed(s);
        ConfirmEnabled = Phase == TradeUiPhase.Negotiating && !localConfirmed;
        UnconfirmEnabled = Phase == TradeUiPhase.Negotiating && localConfirmed;
        CancelEnabled = Phase is TradeUiPhase.OutgoingInvite or TradeUiPhase.Negotiating;
        OfferEditorEnabled = Phase == TradeUiPhase.Negotiating && !localConfirmed;
        ProposeEnabled = OfferEditorEnabled && _draftDirty;
        SelfConfirmed = snap is { } confirmed && LocalConfirmed(confirmed);
        OtherConfirmed = snap is { } remote && PartnerConfirmed(remote);
        ConfirmLabel = ConfirmArmed ? "Confirmer définitivement" : "Confirmer";
        CancelLabel = Phase == TradeUiPhase.OutgoingInvite
            ? "Annuler l'invitation"
            : CancelArmed ? "Annuler vraiment" : "Annuler";
        MyGoldLabel = "Or : " + (Phase == TradeUiPhase.Negotiating && _draftDirty ? DraftGold : MyOffer.Gold);
        TheirGoldLabel = "Or : " + TheirOffer.Gold;
        Headline = Phase switch
        {
            TradeUiPhase.IncomingInvite => "Invitation reçue",
            TradeUiPhase.OutgoingInvite => "Invitation envoyée",
            TradeUiPhase.Negotiating => MyTitle + "  ↔  " + TheirTitle,
            TradeUiPhase.Completed => "Échange validé",
            TradeUiPhase.Cancelled => "Échange annulé",
            _ => "Échange",
        };
        if (Phase == TradeUiPhase.Negotiating && snap is { } open)
        {
            var mine = localConfirmed ? "confirmé" : "en attente";
            var theirs = PartnerConfirmed(open) ? "confirmé" : "en attente";
            if (string.IsNullOrWhiteSpace(StatusLine) || StatusLine == "Aucun échange.")
            {
                StatusLine = $"Vous : {mine}. {TheirTitle} : {theirs}. Révision {open.Revision}.";
            }
        }
    }

    private bool LocalConfirmed(TradeSnapshotWire snapshot)
    {
        if (LocalCharacterId == snapshot.PartnerId && LocalCharacterId != Guid.Empty)
        {
            return snapshot.PartnerConfirmed;
        }

        return snapshot.InitiatorConfirmed;
    }

    private bool PartnerConfirmed(TradeSnapshotWire snapshot)
    {
        if (LocalCharacterId == snapshot.PartnerId && LocalCharacterId != Guid.Empty)
        {
            return snapshot.InitiatorConfirmed;
        }

        return snapshot.PartnerConfirmed;
    }

    private void CopyDraftFrom(TradeOfferWire offer)
    {
        DraftGold = Math.Max(0, offer.Gold);
        _draft.Clear();
        if (offer.Stacks is null)
        {
            return;
        }

        foreach (var stack in offer.Stacks)
        {
            if (stack.ItemId != Guid.Empty && stack.Quantity > 0)
            {
                _draft.Add(stack);
            }
        }
    }

    private bool DraftMatchesServer()
    {
        if (DraftGold != MyOffer.Gold)
        {
            return false;
        }

        var stacks = MyOffer.Stacks ?? Array.Empty<TradeStackWire>();
        if (stacks.Count != _draft.Count)
        {
            return false;
        }

        for (var i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].ItemId != _draft[i].ItemId || stacks[i].Quantity != _draft[i].Quantity)
            {
                return false;
            }
        }

        return true;
    }

    private int OwnedQuantity(Guid itemId)
    {
        var total = 0;
        foreach (var entry in _bag)
        {
            if (entry.ItemId == itemId)
            {
                total += entry.Quantity;
            }
        }

        return total;
    }

    private int DraftQuantity(Guid itemId)
    {
        var total = 0;
        foreach (var stack in _draft)
        {
            if (stack.ItemId == itemId)
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    private static bool IsEmpty(TradeOfferWire offer)
        => offer.Gold <= 0 && (offer.Stacks is null || offer.Stacks.Count == 0);

    private static bool OffersEqual(TradeOfferWire left, TradeOfferWire right)
    {
        if (left.Gold != right.Gold)
        {
            return false;
        }

        var a = left.Stacks ?? Array.Empty<TradeStackWire>();
        var b = right.Stacks ?? Array.Empty<TradeStackWire>();
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].ItemId != b[i].ItemId || a[i].Quantity != b[i].Quantity)
            {
                return false;
            }
        }

        return true;
    }

    private void RememberToast(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == _lastToast)
        {
            return;
        }

        _lastToast = text;
        _pendingToast = text;
    }
}
