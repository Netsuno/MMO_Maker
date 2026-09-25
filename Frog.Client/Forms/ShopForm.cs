using System.Windows.Forms;
using Frog.Client.UI;

namespace Frog.Client.Forms;

/// <summary>Fenêtre boutique : liste nom / prix / stock, quantité, fermeture.</summary>
public sealed class ShopForm : Form
{
    private readonly Label _lblTitle = new() { AutoSize = true, Text = "Boutique" };
    private readonly Label _lblStatus = new() { AutoSize = true, Text = "Boutique fermée.", MaximumSize = new Size(520, 0) };
    private readonly ListBox _lst = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly NumericUpDown _numQty = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 56 };
    private readonly Button _btnBuy = new() { Text = "Acheter", AutoSize = true };
    private readonly Button _btnSell = new() { Text = "Vendre", AutoSize = true };
    private readonly Button _btnClose = new() { Text = "Fermer", AutoSize = true };
    private bool _rendering;

    public event Action? BuyClicked;

    public event Action? SellClicked;

    public event Action? CloseClicked;

    public event Action<int>? QuantityChanged;

    public event Action<int>? SelectionChanged;

    public ShopForm()
    {
        Text = "Boutique";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(420, 320);
        Size = new Size(480, 380);
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblTitle.Font = new Font(Font, FontStyle.Bold);
        var header = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        header.Controls.Add(_lblTitle);
        header.Controls.Add(_lblStatus);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_lst, 0, 1);

        var qtyRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        qtyRow.Controls.Add(new Label { Text = "Quantité", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        qtyRow.Controls.Add(_numQty);
        qtyRow.Controls.Add(_btnBuy);
        qtyRow.Controls.Add(_btnSell);
        root.Controls.Add(qtyRow, 0, 2);

        var closeRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        closeRow.Controls.Add(_btnClose);
        root.Controls.Add(closeRow, 0, 3);
        Controls.Add(root);
        UiTheme.Apply(this);

        _btnBuy.Click += (_, _) => BuyClicked?.Invoke();
        _btnSell.Click += (_, _) => SellClicked?.Invoke();
        _btnClose.Click += (_, _) => RequestClose();
        _numQty.ValueChanged += (_, _) =>
        {
            if (_rendering)
            {
                return;
            }

            QuantityChanged?.Invoke((int)_numQty.Value);
        };
        _lst.SelectedIndexChanged += (_, _) =>
        {
            if (_rendering)
            {
                return;
            }

            SelectionChanged?.Invoke(_lst.SelectedIndex);
        };
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                RequestClose();
            }
        };
    }

    public void Bind(
        string title,
        IReadOnlyList<string> lines,
        int selectedIndex,
        string status,
        string buyLabel,
        string sellLabel,
        int quantity,
        bool buyEnabled,
        bool sellEnabled)
    {
        _rendering = true;
        try
        {
            _lblTitle.Text = string.IsNullOrWhiteSpace(title) ? "Boutique" : title;
            Text = _lblTitle.Text;
            _lblStatus.Text = status;
            _btnBuy.Text = buyLabel;
            _btnSell.Text = sellLabel;
            _btnBuy.Enabled = buyEnabled;
            _btnSell.Enabled = sellEnabled;
            var qty = Math.Clamp(quantity, (int)_numQty.Minimum, (int)_numQty.Maximum);
            if (_numQty.Value != qty)
            {
                _numQty.Value = qty;
            }

            var same = _lst.Items.Count == lines.Count;
            if (same)
            {
                for (var i = 0; i < lines.Count; i++)
                {
                    if (!string.Equals(_lst.Items[i]?.ToString(), lines[i], StringComparison.Ordinal))
                    {
                        same = false;
                        break;
                    }
                }
            }

            if (!same)
            {
                _lst.Items.Clear();
                foreach (var line in lines)
                {
                    _lst.Items.Add(line);
                }
            }

            if (_lst.Items.Count == 0)
            {
                return;
            }

            var index = Math.Clamp(selectedIndex, 0, _lst.Items.Count - 1);
            if (_lst.SelectedIndex != index)
            {
                _lst.SelectedIndex = index;
            }
        }
        finally
        {
            _rendering = false;
        }
    }

    public void HideShop()
    {
        if (Visible)
        {
            Hide();
        }
    }

    private void RequestClose()
    {
        HideShop();
        CloseClicked?.Invoke();
    }
}
