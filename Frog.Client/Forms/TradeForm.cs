using System.Windows.Forms;
using Frog.Client.UI;
using Frog.Core.Protocol;
using Frog.Core.Trade;

namespace Frog.Client.Forms;

/// <summary>Échange : invitation, emplacements, confirmation en deux temps.</summary>
public sealed class TradeForm : Form
{
    private readonly ClientTradeSession _session = new();
    private readonly Label _lblNames = new() { AutoSize = true, Text = "Échange" };
    private readonly Label _lblStatus = new() { AutoSize = true, Text = "Aucun échange.", MaximumSize = new Size(600, 0) };
    private readonly Label _lblRevision = new() { AutoSize = true, Text = "Révision : —" };
    private readonly Label _lblConfirmFlags = new() { AutoSize = true, Text = "Confirmations : —" };
    private readonly Label _lblMineGold = new() { AutoSize = true, Text = "Or : 0" };
    private readonly Label _lblTheirsGold = new() { AutoSize = true, Text = "Or : 0" };
    private readonly ListBox _lstMine = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _lstTheirs = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly NumericUpDown _numGold = new() { Minimum = 0, Maximum = 999_999_999, Width = 90 };
    private readonly ComboBox _cmbBag = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly NumericUpDown _numQty = new() { Minimum = 1, Maximum = 999, Value = 1, Width = 56 };
    private readonly Button _btnAdd = new() { Text = "Ajouter", AutoSize = true };
    private readonly Button _btnRemove = new() { Text = "Retirer", AutoSize = true };
    private readonly Button _btnPropose = new() { Text = "Proposer", AutoSize = true };
    private readonly Button _btnAccept = new() { Text = "Accepter", AutoSize = true };
    private readonly Button _btnDecline = new() { Text = "Refuser", AutoSize = true };
    private readonly Button _btnConfirm = new() { Text = "Confirmer", AutoSize = true, Enabled = false };
    private readonly Button _btnUnconfirm = new() { Text = "Retirer la confirmation", AutoSize = true, Enabled = false };
    private readonly Button _btnCancel = new() { Text = "Annuler", AutoSize = true };
    private readonly Panel _offerEditor = new() { AutoSize = true, Dock = DockStyle.Top };
    private bool _forceClose;
    private bool _rendering;
    private Func<Guid, string>? _nameLookup;

    public event Action<Guid>? AcceptRequested;
    public event Action<Guid>? DeclineRequested;
    public event Action<Guid, uint>? ConfirmRequested;
    public event Action<Guid>? UnconfirmRequested;
    public event Action<Guid>? CancelRequested;
    public event Action<Guid, uint, int, IReadOnlyList<TradeStackWire>>? SetOfferRequested;
    public event Action<string>? PlayerNotice;

    public TradeForm()
    {
        Text = "Échange";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 520);
        Size = new Size(720, 560);
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblNames.Font = new Font(Font, FontStyle.Bold);
        root.Controls.Add(_lblNames, 0, 0);
        root.SetColumnSpan(_lblNames, 2);
        var meta = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        meta.Controls.Add(_lblRevision);
        meta.Controls.Add(_lblConfirmFlags);
        meta.Controls.Add(_lblStatus);
        root.Controls.Add(meta, 0, 1);
        root.SetColumnSpan(meta, 2);

        var mine = new Panel { Dock = DockStyle.Fill };
        var mineTitle = new Label { Text = "Votre offre", Dock = DockStyle.Top, AutoSize = true };
        mine.Controls.Add(_lstMine);
        mine.Controls.Add(_lblMineGold);
        mine.Controls.Add(mineTitle);
        _lblMineGold.Dock = DockStyle.Top;
        mineTitle.Dock = DockStyle.Top;
        var theirs = new Panel { Dock = DockStyle.Fill };
        var theirTitle = new Label { Name = "theirTitle", Text = "Offre du partenaire", Dock = DockStyle.Top, AutoSize = true };
        theirs.Controls.Add(_lstTheirs);
        theirs.Controls.Add(_lblTheirsGold);
        theirs.Controls.Add(theirTitle);
        _lblTheirsGold.Dock = DockStyle.Top;
        theirTitle.Dock = DockStyle.Top;
        root.Controls.Add(mine, 0, 2);
        root.Controls.Add(theirs, 1, 2);

        var editor = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
        };
        editor.Controls.Add(new Label { Text = "Or à proposer", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        editor.Controls.Add(_numGold);
        editor.Controls.Add(_cmbBag);
        editor.Controls.Add(_numQty);
        editor.Controls.Add(_btnAdd);
        editor.Controls.Add(_btnRemove);
        editor.Controls.Add(_btnPropose);
        _offerEditor.Controls.Add(editor);
        root.Controls.Add(_offerEditor, 0, 3);
        root.SetColumnSpan(_offerEditor, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        buttons.Controls.Add(_btnAccept);
        buttons.Controls.Add(_btnDecline);
        buttons.Controls.Add(_btnConfirm);
        buttons.Controls.Add(_btnUnconfirm);
        buttons.Controls.Add(_btnCancel);
        root.Controls.Add(buttons, 0, 4);
        root.SetColumnSpan(buttons, 2);
        Controls.Add(root);
        UiTheme.Apply(this);

        _btnAccept.Click += (_, _) =>
        {
            if (_session.ActiveTradeId is Guid id)
            {
                AcceptRequested?.Invoke(id);
            }
        };
        _btnDecline.Click += (_, _) =>
        {
            if (_session.ActiveTradeId is Guid id)
            {
                DeclineRequested?.Invoke(id);
            }
        };
        _btnConfirm.Click += (_, _) =>
        {
            var send = _session.TryConfirm(out _);
            Render();
            if (send && _session.ActiveTradeId is Guid id)
            {
                ConfirmRequested?.Invoke(id, _session.DisplayedRevision);
            }
        };
        _btnUnconfirm.Click += (_, _) =>
        {
            if (_session.ActiveTradeId is Guid id)
            {
                UnconfirmRequested?.Invoke(id);
            }
        };
        _btnCancel.Click += (_, _) => RequestCancel();
        _btnAdd.Click += (_, _) =>
        {
            if (_cmbBag.SelectedItem is BagRow row
                && _session.TryAddDraftStack(row.ItemId, (int)_numQty.Value, out _))
            {
                Render();
            }
            else
            {
                Render();
            }
        };
        _btnRemove.Click += (_, _) =>
        {
            if (_session.TryRemoveDraftStack(_lstMine.SelectedIndex))
            {
                Render();
            }
        };
        _btnPropose.Click += (_, _) =>
        {
            var send = _session.TryBuildSetOffer(out var revision, out var gold, out var stacks, out _);
            Render();
            if (send && _session.ActiveTradeId is Guid id)
            {
                SetOfferRequested?.Invoke(id, revision, gold, stacks);
            }
        };
        _numGold.ValueChanged += (_, _) =>
        {
            if (_rendering)
            {
                return;
            }

            _session.TrySetDraftGold((int)_numGold.Value, out _);
            Render();
        };
        _cmbBag.SelectedIndexChanged += (_, _) => SyncQtyCap();
    }

    public uint DisplayedRevision => _session.DisplayedRevision;

    public bool ConfirmEnabled => _btnConfirm.Enabled;

    public bool AcceptEnabled => _btnAccept.Enabled;

    public bool DeclineEnabled => _btnDecline.Enabled;

    public string ConfirmLabel => _btnConfirm.Text;

    public string CancelLabel => _btnCancel.Text;

    public string StatusText => _lblStatus.Text;

    public Guid? ActiveTradeId => _session.ActiveTradeId;

    public void SetLocalCharacter(Guid characterId) => _session.SetLocalCharacter(characterId);

    public void SetWallet(int gold)
    {
        _session.SetWallet(gold);
        Render();
    }

    public void SetBag(IReadOnlyList<TradeBagEntry> bag)
    {
        _session.SetBag(bag);
        Render();
    }

    public void ApplySnapshot(TradeSnapshotWire snapshot, Func<Guid, string>? itemNameLookup = null)
    {
        _nameLookup = itemNameLookup;
        _session.ApplySnapshot(snapshot);
        Render();
    }

    public void ApplyResult(TradeResultWire result)
    {
        _session.ApplyResult(result);
        Render();
    }

    public string? NotifyLocalDisconnect()
    {
        var note = _session.NotifyLocalDisconnect();
        Render();
        if (IsHandleCreated && Visible)
        {
            Hide();
        }

        return note;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_forceClose || e.CloseReason != CloseReason.UserClosing)
        {
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        if (_session.Phase == TradeUiPhase.IncomingInvite)
        {
            if (_session.ActiveTradeId is Guid id)
            {
                DeclineRequested?.Invoke(id);
            }

            Hide();
            return;
        }

        if (_session.Phase is TradeUiPhase.OutgoingInvite or TradeUiPhase.Negotiating)
        {
            RequestCancel();
            return;
        }

        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _forceClose = true;
        }

        base.Dispose(disposing);
    }

    private void RequestCancel()
    {
        var send = _session.TryCancel(out _);
        Render();
        if (send && _session.ActiveTradeId is Guid id)
        {
            CancelRequested?.Invoke(id);
        }
    }

    private void Render()
    {
        PublishToast();
        _rendering = true;
        try
        {
            _lblNames.Text = _session.Headline;
            _lblStatus.Text = _session.StatusLine;
            _lblRevision.Text = _session.Phase == TradeUiPhase.Idle
                ? "Révision : —"
                : "Révision : " + _session.DisplayedRevision;
            _lblConfirmFlags.Text = _session.Phase switch
            {
                TradeUiPhase.Completed => "Échange validé.",
                TradeUiPhase.Cancelled => "Échange annulé.",
                TradeUiPhase.IncomingInvite => "En attente de votre réponse.",
                TradeUiPhase.OutgoingInvite => "En attente du partenaire.",
                TradeUiPhase.Negotiating =>
                    "Vous : " + (_session.SelfConfirmed ? "confirmé" : "en attente")
                    + " · " + _session.TheirTitle + " : " + (_session.OtherConfirmed ? "confirmé" : "en attente"),
                _ => "Confirmations : —",
            };
            _lblMineGold.Text = _session.MyTitle + " — " + _session.MyGoldLabel;
            _lblTheirsGold.Text = _session.TheirTitle + " — " + _session.TheirGoldLabel;
            Fill(_lstMine, _session.Phase == TradeUiPhase.Negotiating
                ? new TradeOfferWire(_session.DraftGold, _session.DraftStacks.ToArray())
                : _session.MyOffer);
            Fill(_lstTheirs, _session.TheirOffer);
            _btnAccept.Enabled = _session.AcceptEnabled;
            _btnDecline.Enabled = _session.DeclineEnabled;
            _btnConfirm.Enabled = _session.ConfirmEnabled;
            _btnConfirm.Text = _session.ConfirmLabel;
            _btnUnconfirm.Enabled = _session.UnconfirmEnabled;
            _btnCancel.Enabled = _session.CancelEnabled;
            _btnCancel.Text = _session.CancelLabel;
            _offerEditor.Visible = _session.Phase == TradeUiPhase.Negotiating;
            var editor = _session.OfferEditorEnabled;
            _numGold.Enabled = editor;
            _cmbBag.Enabled = editor;
            _numQty.Enabled = editor;
            _btnAdd.Enabled = editor;
            _btnRemove.Enabled = editor && _lstMine.SelectedIndex >= 0 && _lstMine.SelectedIndex < _session.DraftStacks.Count;
            _btnPropose.Enabled = _session.ProposeEnabled;
            var gold = (decimal)Math.Clamp(_session.DraftGold, (int)_numGold.Minimum, (int)_numGold.Maximum);
            if (_numGold.Value != gold)
            {
                _numGold.Value = gold;
            }

            RebuildBag();
        }
        finally
        {
            _rendering = false;
        }
    }

    private void PublishToast()
    {
        var toast = _session.ConsumeToast();
        if (!string.IsNullOrEmpty(toast))
        {
            PlayerNotice?.Invoke(toast);
        }
    }

    private void RebuildBag()
    {
        var selected = (_cmbBag.SelectedItem as BagRow)?.ItemId;
        _cmbBag.Items.Clear();
        foreach (var entry in _session.Bag)
        {
            _cmbBag.Items.Add(new BagRow(entry.ItemId, entry.Quantity, entry.DisplayName));
        }

        if (_cmbBag.Items.Count == 0)
        {
            SyncQtyCap();
            return;
        }

        var index = 0;
        if (selected is Guid id)
        {
            for (var i = 0; i < _cmbBag.Items.Count; i++)
            {
                if (_cmbBag.Items[i] is BagRow row && row.ItemId == id)
                {
                    index = i;
                    break;
                }
            }
        }

        _cmbBag.SelectedIndex = index;
        SyncQtyCap();
    }

    private void SyncQtyCap()
    {
        var max = _cmbBag.SelectedItem is BagRow row ? Math.Max(1, row.Quantity) : 1;
        _numQty.Maximum = max;
        if (_numQty.Value > max)
        {
            _numQty.Value = max;
        }
    }

    private void Fill(ListBox list, TradeOfferWire offer)
    {
        var selected = list.SelectedIndex;
        list.Items.Clear();
        foreach (var line in ClientTradeSession.FormatSlots(NameOffer(offer)))
        {
            list.Items.Add(line);
        }

        if (selected >= 0 && selected < list.Items.Count)
        {
            list.SelectedIndex = selected;
        }
    }

    private TradeOfferWire NameOffer(TradeOfferWire offer)
    {
        if (_nameLookup is null || offer.Stacks is null || offer.Stacks.Count == 0)
        {
            return offer;
        }

        var named = new TradeStackWire[offer.Stacks.Count];
        for (var i = 0; i < offer.Stacks.Count; i++)
        {
            var stack = offer.Stacks[i];
            var name = string.IsNullOrWhiteSpace(stack.DisplayName)
                ? _nameLookup(stack.ItemId)
                : stack.DisplayName;
            named[i] = stack with { DisplayName = name };
        }

        return offer with { Stacks = named };
    }

    private sealed class BagRow(Guid itemId, int quantity, string name)
    {
        public Guid ItemId { get; } = itemId;
        public int Quantity { get; } = quantity;

        public override string ToString()
        {
            var label = string.IsNullOrWhiteSpace(name) ? ItemId.ToString("N")[..8] : name;
            return label + " × " + Quantity;
        }
    }
}
