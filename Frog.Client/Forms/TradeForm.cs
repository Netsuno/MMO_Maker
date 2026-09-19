using System.Windows.Forms;
using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Client.Forms;

/// <summary>Confirmation visible d'un échange : noms, objets, or, révision.</summary>
public sealed class TradeForm : Form
{
    private readonly ListBox _lstMine = new() { Dock = DockStyle.Fill };
    private readonly ListBox _lstTheirs = new() { Dock = DockStyle.Fill };
    private readonly Label _lblMineGold = new() { AutoSize = true, Text = "Votre or : 0" };
    private readonly Label _lblTheirsGold = new() { AutoSize = true, Text = "Leur or : 0" };
    private readonly Label _lblRevision = new() { AutoSize = true, Text = "Révision : —" };
    private readonly Label _lblNames = new() { AutoSize = true, Text = "Échange" };
    private readonly Label _lblConfirmFlags = new() { AutoSize = true, Text = "Confirmations : —" };
    private readonly Button _btnConfirm = new() { Text = "Confirmer", AutoSize = true, Enabled = false };
    private readonly Button _btnUnconfirm = new() { Text = "Retirer confirm", AutoSize = true, Enabled = false };
    private readonly Button _btnCancel = new() { Text = "Annuler", AutoSize = true };
    private TradeSnapshotWire? _snapshot;
    private uint _displayedRevision;

    public event Action<Guid, uint>? ConfirmRequested;
    public event Action<Guid>? UnconfirmRequested;
    public event Action<Guid>? CancelRequested;

    public TradeForm()
    {
        Text = "Échange";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 360);
        Size = new Size(640, 420);
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblNames.Font = new Font(Font, FontStyle.Bold);
        root.Controls.Add(_lblNames, 0, 0);
        root.SetColumnSpan(_lblNames, 2);
        var meta = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        meta.Controls.Add(_lblRevision);
        meta.Controls.Add(_lblConfirmFlags);
        root.Controls.Add(meta, 0, 1);
        root.SetColumnSpan(meta, 2);

        var mine = new Panel { Dock = DockStyle.Fill };
        mine.Controls.Add(_lstMine);
        mine.Controls.Add(_lblMineGold);
        _lblMineGold.Dock = DockStyle.Top;
        var theirs = new Panel { Dock = DockStyle.Fill };
        theirs.Controls.Add(_lstTheirs);
        theirs.Controls.Add(_lblTheirsGold);
        _lblTheirsGold.Dock = DockStyle.Top;
        root.Controls.Add(mine, 0, 2);
        root.Controls.Add(theirs, 1, 2);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_btnCancel);
        buttons.Controls.Add(_btnUnconfirm);
        buttons.Controls.Add(_btnConfirm);
        root.Controls.Add(buttons, 0, 3);
        root.SetColumnSpan(buttons, 2);
        Controls.Add(root);

        _btnConfirm.Click += (_, _) =>
        {
            if (_snapshot is { } snap)
            {
                ConfirmRequested?.Invoke(snap.TradeId, snap.Revision);
            }
        };
        _btnUnconfirm.Click += (_, _) =>
        {
            if (_snapshot is { } snap)
            {
                UnconfirmRequested?.Invoke(snap.TradeId);
            }
        };
        _btnCancel.Click += (_, _) =>
        {
            if (_snapshot is { } snap)
            {
                CancelRequested?.Invoke(snap.TradeId);
            }
        };
    }

    public void ApplySnapshot(TradeSnapshotWire snapshot, Func<Guid, string>? itemNameLookup = null)
    {
        _snapshot = snapshot;
        _displayedRevision = snapshot.Revision;
        _lblNames.Text = $"{snapshot.InitiatorName}  ↔  {snapshot.PartnerName}";
        _lblRevision.Text = "Révision : " + snapshot.Revision;
        _lblConfirmFlags.Text =
            $"Confirmations : {(snapshot.InitiatorConfirmed ? snapshot.InitiatorName : "—")} / {(snapshot.PartnerConfirmed ? snapshot.PartnerName : "—")}";
        _lblMineGold.Text = $"Offre {snapshot.InitiatorName} — or {snapshot.InitiatorOffer.Gold}";
        _lblTheirsGold.Text = $"Offre {snapshot.PartnerName} — or {snapshot.PartnerOffer.Gold}";
        Fill(_lstMine, snapshot.InitiatorOffer, itemNameLookup);
        Fill(_lstTheirs, snapshot.PartnerOffer, itemNameLookup);
        var open = snapshot.Status == TradeStatus.Open;
        _btnConfirm.Enabled = open;
        _btnUnconfirm.Enabled = open;
        _btnCancel.Enabled = snapshot.Status is TradeStatus.Open or TradeStatus.Inviting;
        if (snapshot.Status is TradeStatus.Committed)
        {
            _lblConfirmFlags.Text = "Échange validé.";
        }
        else if (snapshot.Status is TradeStatus.Cancelled)
        {
            _lblConfirmFlags.Text = "Échange annulé.";
            _btnConfirm.Enabled = false;
            _btnUnconfirm.Enabled = false;
        }
    }

    public uint DisplayedRevision => _displayedRevision;

    public bool ConfirmEnabled => _btnConfirm.Enabled;

    private static void Fill(ListBox list, TradeOfferWire offer, Func<Guid, string>? names)
    {
        list.Items.Clear();
        foreach (var stack in offer.Stacks)
        {
            var name = !string.IsNullOrWhiteSpace(stack.DisplayName)
                ? stack.DisplayName
                : names?.Invoke(stack.ItemId) ?? stack.ItemId.ToString("N")[..8];
            list.Items.Add($"{name} × {stack.Quantity}");
        }

        if (list.Items.Count == 0)
        {
            list.Items.Add("(rien)");
        }
    }
}
