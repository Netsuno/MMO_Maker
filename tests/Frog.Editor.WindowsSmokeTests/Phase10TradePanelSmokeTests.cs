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

            Guid? confirmed = null;
            uint rev = 0;
            form.ConfirmRequested += (id, r) =>
            {
                confirmed = id;
                rev = r;
            };
            foreach (Control child in Walk(form))
            {
                if (child is Button { Text: "Confirmer" } btn)
                {
                    btn.PerformClick();
                    break;
                }
            }

            Assert.Equal(snap.TradeId, confirmed);
            Assert.Equal(7u, rev);

            form.ApplySnapshot(snap with { Status = TradeStatus.Cancelled, Revision = 8 });
            Assert.False(form.ConfirmEnabled);
            form.Close();
        });
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
