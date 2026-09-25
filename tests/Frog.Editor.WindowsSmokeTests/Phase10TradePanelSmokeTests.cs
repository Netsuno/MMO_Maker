using System;
using System.Windows.Forms;
using Frog.Client.Forms;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class Phase10TradePanelSmokeTests
{
    [Fact]
    public void TradeForm_ShowsNamesQtyGoldRevision_ConfirmUsesDisplayedRevision()
    {
        StaTestRunner.Run(() =>
        {
            using var form = new TradeForm();
            form.Show();
            var item = Guid.NewGuid();
            var snap = new TradeSnapshotWire(
                Guid.NewGuid(),
                7,
                TradeStatus.Open,
                Guid.NewGuid(),
                Guid.NewGuid(),
                false,
                false,
                "Alice",
                "Bob",
                new TradeOfferWire(12, [new TradeStackWire(item, 3, "Potion")]),
                new TradeOfferWire(4, Array.Empty<TradeStackWire>()));
            form.ApplySnapshot(snap);
            Assert.Equal(7u, form.DisplayedRevision);
            Assert.True(form.ConfirmEnabled);
            Assert.False(form.AcceptEnabled);
            Assert.Contains("libre", string.Join('\n', SlotLines(form)), StringComparison.Ordinal);

            Guid? confirmed = null;
            uint rev = 0;
            form.ConfirmRequested += (id, r) =>
            {
                confirmed = id;
                rev = r;
            };
            var confirm = FindButton(form, static text => text.StartsWith("Confirmer", StringComparison.Ordinal));
            Assert.NotNull(confirm);
            confirm!.PerformClick();
            Assert.Null(confirmed);
            Assert.Equal("Confirmer définitivement", form.ConfirmLabel);
            confirm.PerformClick();

            Assert.Equal(snap.TradeId, confirmed);
            Assert.Equal(7u, rev);

            form.ApplySnapshot(snap with { Status = TradeStatus.Cancelled, Revision = 8, Notice = "Joueur deconnecte." });
            Assert.False(form.ConfirmEnabled);
            Assert.Contains("déconnecté", form.StatusText, StringComparison.Ordinal);
            form.Close();
        });
    }

    [Fact]
    public void TradeForm_IncomingInvite_AcceptDecline_OutgoingCancel()
    {
        StaTestRunner.Run(() =>
        {
            using var form = new TradeForm();
            form.Show();
            var me = Guid.NewGuid();
            var them = Guid.NewGuid();
            form.SetLocalCharacter(me);
            var incoming = new TradeSnapshotWire(
                Guid.NewGuid(),
                0,
                TradeStatus.Inviting,
                them,
                me,
                false,
                false,
                "Alice",
                "Bob",
                new TradeOfferWire(0, Array.Empty<TradeStackWire>()),
                new TradeOfferWire(0, Array.Empty<TradeStackWire>()));
            Guid? accepted = null;
            form.AcceptRequested += id => accepted = id;
            form.ApplySnapshot(incoming);
            Assert.True(form.AcceptEnabled);
            Assert.True(form.DeclineEnabled);
            Assert.False(form.ConfirmEnabled);
            FindButton(form, static text => text == "Accepter")!.PerformClick();
            Assert.Equal(incoming.TradeId, accepted);

            var outgoing = incoming with { InitiatorId = me, PartnerId = them, InitiatorName = "Bob", PartnerName = "Alice" };
            form.ApplySnapshot(outgoing);
            Assert.False(form.AcceptEnabled);
            Assert.Equal("Annuler l'invitation", form.CancelLabel);
            form.Close();
        });
    }

    private static Button? FindButton(Control root, Func<string, bool> match)
    {
        foreach (var child in Walk(root))
        {
            if (child is Button button && match(button.Text))
            {
                return button;
            }
        }

        return null;
    }

    private static System.Collections.Generic.IEnumerable<string> SlotLines(Control root)
    {
        foreach (var child in Walk(root))
        {
            if (child is ListBox list)
            {
                foreach (var item in list.Items)
                {
                    yield return item?.ToString() ?? string.Empty;
                }
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<Control> Walk(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (var nested in Walk(child))
            {
                yield return nested;
            }
        }
    }
}
