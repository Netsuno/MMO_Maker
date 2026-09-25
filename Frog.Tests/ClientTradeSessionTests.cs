using System;
using System.IO;
using System.Linq;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Core.Trade;
using Xunit;

namespace Frog.Tests;

public sealed class ClientTradeSessionTests
{
    [Fact]
    public void Protocol_HelloStays11_TradeOpcodesUnchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(84, (byte)PacketId.TradeRequest);
        Assert.Equal(85, (byte)PacketId.TradeResult);
        Assert.Equal(86, (byte)PacketId.TradeSnapshot);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    [Fact]
    public void IncomingInvite_EnablesAcceptDecline_OutgoingCancelsOnce()
    {
        var me = Guid.NewGuid();
        var them = Guid.NewGuid();
        var session = new ClientTradeSession();
        session.SetLocalCharacter(me);
        var tradeId = Guid.NewGuid();
        session.ApplySnapshot(Snap(tradeId, TradeStatus.Inviting, them, me, "Alice", "Moi"));
        Assert.Equal(TradeUiPhase.IncomingInvite, session.Phase);
        Assert.True(session.AcceptEnabled);
        Assert.True(session.DeclineEnabled);
        Assert.False(session.ConfirmEnabled);
        Assert.Equal("Alice vous invite à échanger.", session.ConsumeToast());

        session.ApplySnapshot(Snap(tradeId, TradeStatus.Inviting, me, them, "Moi", "Bob"));
        Assert.Equal(TradeUiPhase.OutgoingInvite, session.Phase);
        Assert.False(session.AcceptEnabled);
        Assert.Equal("Annuler l'invitation", session.CancelLabel);
        Assert.True(session.TryCancel(out _));
    }

    [Fact]
    public void Confirm_RequiresSecondClick_AndDisarmsWhenRevisionChanges()
    {
        var me = Guid.NewGuid();
        var session = new ClientTradeSession();
        session.SetLocalCharacter(me);
        var item = Guid.NewGuid();
        session.SetBag([new TradeBagEntry(item, 4, "Potion")]);
        session.SetWallet(20);
        var open = Snap(Guid.NewGuid(), TradeStatus.Open, me, Guid.NewGuid(), "Moi", "Bob") with
        {
            Revision = 3,
            InitiatorOffer = new TradeOfferWire(5, [new TradeStackWire(item, 1, "Potion")]),
        };
        session.ApplySnapshot(open);
        Assert.False(session.TryConfirm(out var first));
        Assert.Contains("confirmez à nouveau", first, StringComparison.OrdinalIgnoreCase);
        Assert.True(session.ConfirmArmed);
        Assert.Equal("Confirmer définitivement", session.ConfirmLabel);
        Assert.True(session.TryConfirm(out _));

        session.ApplySnapshot(open with { Revision = 4, InitiatorConfirmed = false, PartnerConfirmed = false });
        Assert.False(session.ConfirmArmed);
        Assert.False(session.TryConfirm(out _));
        Assert.True(session.ConfirmArmed);
    }

    [Fact]
    public void EmptyOffers_BlockConfirm_FullInventoryAndDistanceToastInFrench()
    {
        var me = Guid.NewGuid();
        var session = new ClientTradeSession();
        session.SetLocalCharacter(me);
        var tradeId = Guid.NewGuid();
        session.ApplySnapshot(Snap(tradeId, TradeStatus.Open, me, Guid.NewGuid(), "Moi", "Bob"));
        Assert.False(session.TryConfirm(out var blocked));
        Assert.Contains("avant de confirmer", blocked, StringComparison.OrdinalIgnoreCase);

        session.ApplySnapshot(Snap(tradeId, TradeStatus.Open, me, Guid.NewGuid(), "Moi", "Bob") with
        {
            Revision = 2,
            Notice = "Inventaire plein.",
            InitiatorOffer = new TradeOfferWire(1, Array.Empty<TradeStackWire>()),
        });
        Assert.Equal("Échange impossible : inventaire plein.", session.ConsumeToast());
        Assert.False(session.SelfConfirmed);

        session.ApplySnapshot(Snap(tradeId, TradeStatus.Cancelled, me, Guid.NewGuid(), "Moi", "Bob") with
        {
            Revision = 3,
            Notice = "Participants hors portee.",
        });
        Assert.Equal(TradeUiPhase.Cancelled, session.Phase);
        Assert.Equal("Échange annulé : trop loin.", session.ConsumeToast());
        Assert.Null(session.NotifyLocalDisconnect());
    }

    [Fact]
    public void DisconnectMidTrade_ToastsAndClearsActions()
    {
        var me = Guid.NewGuid();
        var session = new ClientTradeSession();
        session.SetLocalCharacter(me);
        session.ApplySnapshot(Snap(Guid.NewGuid(), TradeStatus.Open, me, Guid.NewGuid(), "Moi", "Bob") with
        {
            InitiatorOffer = new TradeOfferWire(3, Array.Empty<TradeStackWire>()),
        });
        _ = session.ConsumeToast();
        Assert.Equal("Échange annulé : connexion interrompue.", session.NotifyLocalDisconnect());
        Assert.False(session.ConfirmEnabled);
        Assert.False(session.CancelEnabled);
        Assert.Null(session.NotifyLocalDisconnect());
    }

    [Fact]
    public void OfferSlots_AreNumbered_AndCannotExceedBagOrEight()
    {
        var me = Guid.NewGuid();
        var session = new ClientTradeSession();
        session.SetLocalCharacter(me);
        var item = Guid.NewGuid();
        session.SetBag([new TradeBagEntry(item, 2, "Potion")]);
        session.SetWallet(10);
        session.ApplySnapshot(Snap(Guid.NewGuid(), TradeStatus.Open, me, Guid.NewGuid(), "Moi", "Bob"));
        Assert.False(session.TrySetDraftGold(11, out var goldError));
        Assert.Equal("Or insuffisant.", goldError);
        Assert.True(session.TrySetDraftGold(4, out _));
        Assert.False(session.TryAddDraftStack(item, 3, out var qtyError));
        Assert.Equal("Objets insuffisants.", qtyError);
        Assert.True(session.TryAddDraftStack(item, 2, out _));
        Assert.True(session.ProposeEnabled);
        Assert.False(session.TryConfirm(out var unsent));
        Assert.Contains("Proposez", unsent, StringComparison.Ordinal);
        Assert.True(session.TryBuildSetOffer(out var revision, out var gold, out var stacks, out _));
        Assert.Equal(0u, revision);
        Assert.Equal(4, gold);
        Assert.Equal(2, stacks[0].Quantity);

        var lines = ClientTradeSession.FormatSlots(new TradeOfferWire(4, stacks));
        Assert.Equal(SocialProtocolLimits.TradeMaxStacksPerSide, lines.Count);
        Assert.Contains("1. Potion × 2", lines);
        Assert.Contains("2. — libre", lines);

        session.TryRemoveDraftStack(0);
        for (var i = 0; i < SocialProtocolLimits.TradeMaxStacksPerSide; i++)
        {
            var extra = Guid.NewGuid();
            session.SetBag(session.Bag.Append(new TradeBagEntry(extra, 1, "Objet" + i)).ToArray());
            Assert.True(session.TryAddDraftStack(extra, 1, out _));
        }

        var overflow = Guid.NewGuid();
        session.SetBag(session.Bag.Append(new TradeBagEntry(overflow, 1, "Trop")).ToArray());
        Assert.False(session.TryAddDraftStack(overflow, 1, out var slotError));
        Assert.Contains("Huit", slotError, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_ValuableOffer_RequiresSecondClick_ConfirmedSideCannotEdit()
    {
        var me = Guid.NewGuid();
        var session = new ClientTradeSession();
        session.SetLocalCharacter(me);
        var open = Snap(Guid.NewGuid(), TradeStatus.Open, me, Guid.NewGuid(), "Moi", "Bob") with
        {
            Revision = 1,
            InitiatorOffer = new TradeOfferWire(8, Array.Empty<TradeStackWire>()),
            InitiatorConfirmed = true,
        };
        session.ApplySnapshot(open);
        Assert.True(session.SelfConfirmed);
        Assert.True(session.UnconfirmEnabled);
        Assert.False(session.OfferEditorEnabled);
        Assert.False(session.TrySetDraftGold(1, out var edit));
        Assert.Contains("confirmation", edit, StringComparison.OrdinalIgnoreCase);
        Assert.False(session.TryCancel(out var armed));
        Assert.Contains("Annulez à nouveau", armed, StringComparison.Ordinal);
        Assert.Equal("Annuler vraiment", session.CancelLabel);
        Assert.True(session.TryCancel(out _));
    }

    [Fact]
    public void Shell_WiresTradeWindow_AcceptOfferNoticesAndDisconnect()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var form = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Forms", "TradeForm.cs"));
        Assert.Contains("_tradeForm.AcceptRequested", shell, StringComparison.Ordinal);
        Assert.Contains("_tradeForm.DeclineRequested", shell, StringComparison.Ordinal);
        Assert.Contains("_tradeForm.SetOfferRequested", shell, StringComparison.Ordinal);
        Assert.Contains("_tradeForm.PlayerNotice += ShowPlayerStatus", shell, StringComparison.Ordinal);
        Assert.Contains("NotifyLocalDisconnect", shell, StringComparison.Ordinal);
        Assert.Contains("BuildTradeBag", shell, StringComparison.Ordinal);
        Assert.Contains("TradePlayerMessages.Present", shell, StringComparison.Ordinal);
        Assert.Contains("\"/trade \"", shell, StringComparison.Ordinal);
        Assert.Contains("Text = \"Accepter\"", form, StringComparison.Ordinal);
        Assert.Contains("Text = \"Refuser\"", form, StringComparison.Ordinal);
        Assert.Contains("Text = \"Proposer\"", form, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    private static TradeSnapshotWire Snap(
        Guid tradeId,
        TradeStatus status,
        Guid initiator,
        Guid partner,
        string initiatorName,
        string partnerName)
        => new(
            tradeId,
            0,
            status,
            initiator,
            partner,
            false,
            false,
            initiatorName,
            partnerName,
            new TradeOfferWire(0, Array.Empty<TradeStackWire>()),
            new TradeOfferWire(0, Array.Empty<TradeStackWire>()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
