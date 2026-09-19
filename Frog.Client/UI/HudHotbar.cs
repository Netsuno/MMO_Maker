#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>Hotbar 10 slots (1–0). Chiffre dans la case ; libellé en tooltip. Slots 4–10 disabled.</summary>
public sealed class HudHotbar : Panel
{
    public const int SlotCount = 10;

    private readonly Button[] _slots = new Button[SlotCount];
    private readonly string[] _tooltips;
    private readonly ToolTip _tips = new();

    public event Action<int>? SlotActivated;

    public HudHotbar()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = UiTheme.BgPanel;
        Size = new Size(392, 52);
        MinimumSize = new Size(360, 48);
        Padding = new Padding(6, 6, 6, 6);
        Paint += DrawChrome;
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(2),
        };
        var digits = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
        _tooltips = new[]
        {
            "Mêlée (1)",
            "Sort (2)",
            "Interagir (3)",
            "Slot 4 — non lié",
            "Slot 5 — non lié",
            "Slot 6 — non lié",
            "Slot 7 — non lié",
            "Slot 8 — non lié",
            "Slot 9 — non lié",
            "Slot 0 — non lié",
        };
        for (var i = 0; i < SlotCount; i++)
        {
            var index = i;
            var wired = i < 3;
            var btn = new Button
            {
                Text = digits[i],
                Width = 36,
                Height = 36,
                Margin = new Padding(1),
                Enabled = wired,
                FlatStyle = FlatStyle.Flat,
                Tag = i,
                Font = UiTheme.UiFont(10f, FontStyle.Bold),
            };
            UiTheme.StyleButton(btn);
            if (!wired)
            {
                btn.ForeColor = UiTheme.TextMuted;
                btn.FlatAppearance.BorderColor = UiTheme.AccentGoldDim;
            }

            _tips.SetToolTip(btn, _tooltips[i]);
            btn.Click += (_, _) =>
            {
                if (btn.Enabled)
                {
                    SlotActivated?.Invoke(index);
                }
            };
            _slots[i] = btn;
            row.Controls.Add(btn);
        }

        Controls.Add(row);
    }

    internal int SlotCountForTest => _slots.Length;

    internal bool SlotEnabledForTest(int index) => (uint)index < SlotCount && _slots[index].Enabled;

    internal string SlotTextForTest(int index) => (uint)index < SlotCount ? _slots[index].Text : string.Empty;

    internal string SlotToolTipForTest(int index) => (uint)index < SlotCount ? _tooltips[index] : string.Empty;

    public void ActivateSlot(int index)
    {
        if ((uint)index >= SlotCount || !_slots[index].Enabled)
        {
            return;
        }

        SlotActivated?.Invoke(index);
    }

    private void DrawChrome(object? sender, PaintEventArgs e)
    {
        if (Width < 4 || Height < 4)
        {
            return;
        }

        using var gold = new Pen(UiTheme.AccentGold);
        e.Graphics.DrawRectangle(gold, 0, 0, Width - 1, Height - 1);
    }
}
